using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SpeakMate.Application.Account;
using SpeakMate.Application.Auth;
using SpeakMate.Application.Usage;
using SpeakMate.Infrastructure.Ai;
using SpeakMate.Infrastructure.Identity;
using SpeakMate.Application.Abstractions;
using SpeakMate.Infrastructure.Persistence;
using SpeakMate.Infrastructure.Persistence.Repositories;
using SpeakMate.Infrastructure.Retention;

namespace SpeakMate.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "SpeakMateDb";
    public const string ReadinessTag = "ready";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured.");

        services.AddDbContext<SpeakMateDbContext>(options => options.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsHistoryTable("__EFMigrationsHistory", SpeakMateDbContext.Schema);
            // Retries transient faults (failover, brief network loss) with exponential back-off.
            sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
            sql.CommandTimeout(30);
        }));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<SpeakMateDbContext>());
        services.AddScoped<ILearnerProfileRepository, LearnerProfileRepository>();
        services.AddScoped<IPracticeSessionRepository, PracticeSessionRepository>();
        services.AddScoped<IUsageEventRepository, UsageEventRepository>();
        services.AddScoped<IConsentRepository, ConsentRepository>();
        services.AddSingleton<IUsageRecorder, IsolatedUsageRecorder>();

        services.TryAddSingleton(TimeProvider.System);

        AddIdentity(services, configuration);
        AddAi(services, configuration);

        services.AddOptions<RetentionOptions>().Bind(configuration.GetSection(RetentionOptions.SectionName));
        services.AddScoped<RetentionService>();
        services.AddHostedService<RetentionWorker>();

        services.AddHealthChecks()
            .AddDbContextCheck<SpeakMateDbContext>("database", tags: [ReadinessTag]);

        return services;
    }

    private static void AddIdentity(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => o.HasStrongSigningKey,
                $"Jwt:SigningKey must be at least {JwtOptions.MinimumKeyBytes} bytes. Set it with user-secrets or an environment variable.")
            .Validate(o => o.AccessTokenMinutes is > 0 and <= 60, "Jwt:AccessTokenMinutes must be between 1 and 60.")
            .Validate(o => o.RefreshTokenDays is > 0 and <= 90, "Jwt:RefreshTokenDays must be between 1 and 90.")
            .ValidateOnStart();

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                // NIST SP 800-63B: favour length over composition rules.
                options.Password.RequiredLength = AuthRules.PasswordMinLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 4;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<SpeakMateDbContext>();

        services.AddSingleton<TokenFactory>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccountService, AccountService>();
    }

    private static void AddAi(IServiceCollection services, IConfiguration configuration)
    {
        var usageLimits = configuration.GetSection(UsageLimits.SectionName).Get<UsageLimits>() ?? new UsageLimits();
        services.AddSingleton(usageLimits);

        services.AddOptions<GeminiOptions>()
            .Bind(configuration.GetSection(GeminiOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Model), "Gemini:Model is required.")
            .ValidateOnStart();

        var provider = configuration.GetValue(AiOptions.SectionName + ":Provider", AiProvider.Gemini);
        var hasKey = !string.IsNullOrWhiteSpace(configuration[GeminiOptions.SectionName + ":ApiKey"]);
        var resolved = provider switch
        {
            AiProvider.Auto => hasKey ? AiProvider.Gemini : AiProvider.Fake,
            AiProvider.Gemini when !hasKey => throw new InvalidOperationException(
                "Ai:Provider is Gemini but Gemini:ApiKey is not set. Set it with user-secrets or the Gemini__ApiKey environment variable."),
            _ => provider,
        };
        services.AddSingleton(new AiRuntimeInfo(resolved));

        if (resolved == AiProvider.Fake)
        {
            services.AddSingleton<IAiCoach, FakeAiCoach>();
            return;
        }

        var gemini = configuration.GetSection(GeminiOptions.SectionName).Get<GeminiOptions>() ?? new GeminiOptions();
        services.AddHttpClient<IAiCoach, GeminiCoach>(client => client.BaseAddress = gemini.BaseUrl)
            .AddStandardResilienceHandler(o =>
            {
                // Retries 429/5xx/timeouts with jittered back-off; the breaker stops hammering a failing provider.
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(gemini.AttemptTimeoutSeconds);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(gemini.AttemptTimeoutSeconds * 3);
                o.Retry.MaxRetryAttempts = 2;
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(gemini.AttemptTimeoutSeconds * 3);
            });
    }
}

/// <summary>Which AI provider is active in this process (logged at startup, shown in diagnostics).</summary>
public sealed record AiRuntimeInfo(AiProvider Provider);
