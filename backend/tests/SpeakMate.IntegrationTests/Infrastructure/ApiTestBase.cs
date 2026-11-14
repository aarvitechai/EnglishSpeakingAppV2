using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpeakMate.Api.Endpoints;

namespace SpeakMate.IntegrationTests.Infrastructure;

/// <summary>Base for HTTP-level tests: an in-memory API plus helpers to create signed-in users.</summary>
public abstract class ApiTestBase(TestDatabase database) : IntegrationTestBase(database)
{
    public const string DefaultPassword = "correct-horse-battery";

    /// <summary>Same JSON settings as the API (camelCase, enums as strings).</summary>
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private SpeakMateApiFactory? _factory;

    protected SpeakMateApiFactory Factory => _factory ??= CreateFactory();

    protected virtual SpeakMateApiFactory CreateFactory() => new(Database);

    protected HttpClient NewClient() => Factory.CreateBrowserLikeClient();

    protected static string UniqueEmail() => $"learner-{Guid.NewGuid():N}@example.test";

    protected static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email,
        string password = DefaultPassword, string displayName = "Test Learner", bool acceptTerms = true) =>
        client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password, displayName, acceptTerms }, Json, Ct);

    /// <summary>Registers a new learner and returns a client authorised as them.</summary>
    protected async Task<(HttpClient Client, AuthResponse Auth, string Email)> SignedInClientAsync(
        string displayName = "Test Learner")
    {
        var client = NewClient();
        var email = UniqueEmail();
        var response = await RegisterAsync(client, email, displayName: displayName);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json, Ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth, email);
    }

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Json, Ct);

    public override async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
