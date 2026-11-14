using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Respawn;
using SpeakMate.Infrastructure.Persistence;

[assembly: AssemblyFixture(typeof(SpeakMate.IntegrationTests.Infrastructure.TestDatabase))]

namespace SpeakMate.IntegrationTests.Infrastructure;

/// <summary>
/// A real SQL Server database dedicated to tests. Built from the migrations once per run
/// (which also proves the migrations work) and wiped between tests with Respawn.
/// </summary>
public sealed class TestDatabase : IAsyncLifetime
{
    private const string DefaultConnectionString =
        @"Server=DESKTOP-2ERCDGB\MSSQLSERVER01;Database=EnglishSpeakingAppV2DB_Test;Trusted_Connection=True;" +
        "TrustServerCertificate=True;Encrypt=True;Application Name=SpeakMate.IntegrationTests";

    private Respawner? _respawner;

    /// <summary>CI overrides this with the ConnectionStrings__TestDatabase environment variable.</summary>
    public string ConnectionString { get; } =
        Environment.GetEnvironmentVariable("ConnectionStrings__TestDatabase") ?? DefaultConnectionString;

    public async ValueTask InitializeAsync()
    {
        var builder = new SqlConnectionStringBuilder(ConnectionString);
        if (!builder.InitialCatalog.EndsWith("_Test", StringComparison.OrdinalIgnoreCase))
        {
            // Safety net: never drop a database that is not clearly a test database.
            throw new InvalidOperationException("Integration tests only run against a database whose name ends in '_Test'.");
        }

        await using (var db = CreateDbContext())
        {
            await db.Database.EnsureDeletedAsync();
            await db.Database.MigrateAsync();
        }

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            SchemasToInclude = [SpeakMateDbContext.Schema],
            TablesToIgnore = ["__EFMigrationsHistory"],
        });
    }

    public async Task ResetAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner!.ResetAsync(connection);
    }

    public SpeakMateDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<SpeakMateDbContext>()
            .UseSqlServer(ConnectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", SpeakMateDbContext.Schema))
            .Options);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
