using System.Text.Json;
using Duofy.ApiClient;
using Microsoft.Extensions.Caching.Distributed;

namespace Duofy.Web.Session;

/// <summary>
/// Die Tokens aller Sitzungen im Speicher des Web-Servers, gefunden über die
/// Kennung aus dem Cookie.
///
/// Heute ist das der Arbeitsspeicher (AddDistributedMemoryCache): Nach einem
/// Neustart des Web-Servers muss man sich neu anmelden. Für mehrere Server kann
/// später ein gemeinsamer Speicher wie Redis eingesetzt werden, ohne diese Klasse
/// zu ändern.
/// </summary>
public sealed class SessionTokenCache
{
    private static readonly DistributedCacheEntryOptions Lifetime = new()
    {
        AbsoluteExpirationRelativeToNow = SessionCookie.Lifetime,
    };

    private readonly IDistributedCache cache;

    public SessionTokenCache(IDistributedCache cache)
    {
        this.cache = cache;
    }

    public async Task<AuthTokens?> GetAsync(string sessionId, CancellationToken cancellationToken)
    {
        var json = await cache.GetStringAsync(Key(sessionId), cancellationToken);
        return json is null ? null : JsonSerializer.Deserialize<AuthTokens>(json);
    }

    public Task SaveAsync(string sessionId, AuthTokens tokens, CancellationToken cancellationToken) =>
        cache.SetStringAsync(Key(sessionId), JsonSerializer.Serialize(tokens), Lifetime, cancellationToken);

    public Task ClearAsync(string sessionId, CancellationToken cancellationToken) =>
        cache.RemoveAsync(Key(sessionId), cancellationToken);

    private static string Key(string sessionId) => $"duofy:tokens:{sessionId}";
}
