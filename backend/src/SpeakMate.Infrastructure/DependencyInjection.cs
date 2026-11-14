using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SpeakMate.Application.Auth;
using SpeakMate.Infrastructure.Identity;
using SpeakMate.Application.Abstractions;
using SpeakMate.Infrastructure.Persistence;
using SpeakMate.Infrastructure.Persistence.Repositories;

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

        services.TryAddSingleton(TimeProvider.System);

        AddIdentity(services, configuration);

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
    }
}
