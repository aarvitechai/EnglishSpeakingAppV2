using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        services.AddSingleton(TimeProvider.System);

        services.AddHealthChecks()
            .AddDbContextCheck<SpeakMateDbContext>("database", tags: [ReadinessTag]);

        return services;
    }
}
