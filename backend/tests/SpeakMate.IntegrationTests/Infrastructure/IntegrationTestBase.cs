namespace SpeakMate.IntegrationTests.Infrastructure;

/// <summary>Starts every test from an empty database.</summary>
public abstract class IntegrationTestBase(TestDatabase database) : IAsyncLifetime
{
    protected TestDatabase Database { get; } = database;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public virtual async ValueTask InitializeAsync() => await Database.ResetAsync();

    public virtual ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
