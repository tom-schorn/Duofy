using System.Net;
using Duofy.ApiClient;
using Duofy.Push;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duofy.Push.Tests;

/// <summary>Spec 84, M2: Eine abgelaufene Anmeldung darf sich beim Abmelden des Geräts nicht im Kreis drehen.</summary>
public class ExpiredSessionTests
{
    private sealed class MemoryTokenStore : ITokenStore
    {
        public AuthTokens? Tokens { get; set; }
        public Task<AuthTokens?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Tokens);
        public Task SaveAsync(AuthTokens tokens, CancellationToken cancellationToken = default) { Tokens = tokens; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default) { Tokens = null; return Task.CompletedTask; }
    }

    /// <summary>Der Server, bei dem alles abgelaufen ist: jede Anfrage und auch der Refresh bekommen 401.</summary>
    private sealed class ExpiredServer : HttpMessageHandler
    {
        private readonly List<string> requests = [];

        public IReadOnlyList<string> Requests { get { lock (requests) { return requests.ToList(); } } }

        public int Count(HttpMethod method, string path) => Requests.Count(r => r == $"{method} {path}");

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (requests)
            {
                requests.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        }
    }

    [Fact]
    public async Task An_expired_session_ends_with_one_refresh_attempt_and_one_device_delete()
    {
        var server = new ExpiredServer();
        var inner = new MemoryTokenStore { Tokens = new AuthTokens("old-access", "old-refresh") };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDuofyApiClient(new Uri("https://duofy.example/"));
        services.AddHttpClient(DuofyApiClientServiceCollectionExtensions.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => server);
        services.AddSingleton<IPushTokenSource>(new FakeTokenSource("fcm-token"));
        services.AddSingleton<IInstallationIdStore>(new FixedInstallation("inst"));
        services.AddScoped<DeviceRegistrar>();
        services.AddSingleton<ITokenStore>(sp => new DeviceRegisteringTokenStore(inner, sp, NullLogger<DeviceRegisteringTokenStore>.Instance));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IUserApi>().GetMeAsync().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(1, server.Count(HttpMethod.Post, "/api/v1/auth/refresh"));
        Assert.Equal(1, server.Count(HttpMethod.Delete, "/api/v1/me/devices/inst"));
        Assert.Null(inner.Tokens);
    }
}
