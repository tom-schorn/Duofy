using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.ApiClient.Tests;

/// <summary>Eine Token-Ablage im Speicher, wie sie die Hosts mit SecureStorage oder Sitzung bauen.</summary>
internal sealed class InMemoryTokenStore : ITokenStore
{
    public AuthTokens? Tokens { get; set; }

    /// <summary>Wenn gesetzt, scheitert das Speichern mit dieser Ausnahme.</summary>
    public Exception? SaveFailure { get; set; }

    public Task<AuthTokens?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Tokens);

    public Task SaveAsync(AuthTokens tokens, CancellationToken cancellationToken = default)
    {
        if (SaveFailure is not null)
        {
            throw SaveFailure;
        }
        Tokens = tokens;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        Tokens = null;
        return Task.CompletedTask;
    }
}

/// <summary>Eine Anfrage, wie die gespielte API sie gesehen hat.</summary>
internal sealed record SeenRequest(HttpMethod Method, string Path, string? Authorization, string? Body, string Query = "");

/// <summary>
/// Spielt die Duofy-API: Jede Anfrage geht an <see cref="Respond"/>, und jede Anfrage
/// wird in <see cref="Requests"/> festgehalten.
/// </summary>
internal sealed class FakeApi : HttpMessageHandler
{
    public List<SeenRequest> Requests { get; } = [];

    /// <summary>Die Rechner der Anfragen in der Reihenfolge, in der sie ankamen.</summary>
    public List<string> Hosts { get; } = [];

    public Func<SeenRequest, HttpResponseMessage> Respond { get; set; } =
        _ => new HttpResponseMessage(HttpStatusCode.NotFound);

    public int CountOf(string path) => Requests.Count(r => r.Path == path);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var seen = new SeenRequest(
            request.Method,
            request.RequestUri!.AbsolutePath,
            request.Headers.Authorization?.ToString(),
            body,
            request.RequestUri.Query);
        Requests.Add(seen);
        Hosts.Add(request.RequestUri.Host);
        return Respond(seen);
    }

    public static HttpResponseMessage Json<T>(HttpStatusCode status, T value) =>
        new(status) { Content = JsonContent.Create(value) };
}

/// <summary>Baut einen Container wie ein Host: AddDuofyApiClient mit gespielter API und Ablage.</summary>
internal static class TestHost
{
    public static ServiceProvider Create(FakeApi api, InMemoryTokenStore store) =>
        Create(api, store, null, null);

    public static ServiceProvider Create(HttpMessageHandler api, InMemoryTokenStore store, IServerStore? servers, ServerSelectorOptions? options = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDuofyApiClient(new Uri("https://api.test"));
        services.AddHttpClient(DuofyApiClientServiceCollectionExtensions.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => api);
        services.AddSingleton<ITokenStore>(store);
        if (servers is not null)
        {
            services.AddDuofyServerChoice(servers, options);
        }
        return services.BuildServiceProvider(validateScopes: true);
    }
}
