using System.Net;
using Duofy.ApiClient;
using Duofy.Web.Session;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;

namespace Duofy.Web.Tests;

/// <summary>Wie lange die Anmeldung im Web hält: sieben Tage (Spec 97).</summary>
public class SessionCookieTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const int SevenDaysInSeconds = 7 * 24 * 60 * 60;

    private readonly WebApplicationFactory<Program> factory;

    public SessionCookieTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    // A3
    [Fact]
    public async Task A3_the_session_cookie_lives_seven_days()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

        var response = await client.GetAsync("/not-found");

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(SessionCookie.DevelopmentName + "="));
        Assert.Contains($"max-age={SevenDaysInSeconds}", cookie, StringComparison.OrdinalIgnoreCase);
    }

    // A3: Der Token-Speicher des Servers hält genauso lange wie das Cookie.
    [Fact]
    public async Task A3_the_token_cache_keeps_the_tokens_seven_days_from_each_save()
    {
        var cache = new RecordingCache();
        var tokens = new SessionTokenCache(cache);

        await tokens.SaveAsync(SessionCookie.NewId(), new AuthTokens("access", "refresh"), CancellationToken.None);

        Assert.Equal(TimeSpan.FromDays(7), cache.LastOptions?.AbsoluteExpirationRelativeToNow);
    }

    private sealed class RecordingCache : IDistributedCache
    {
        public DistributedCacheEntryOptions? LastOptions { get; private set; }

        public byte[]? Get(string key) => null;
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]?>(null);
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) { }
        public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => LastOptions = options;

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            LastOptions = options;
            return Task.CompletedTask;
        }
    }
}
