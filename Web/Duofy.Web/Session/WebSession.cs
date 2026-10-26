using Duofy.ApiClient;

namespace Duofy.Web.Session;

/// <summary>
/// Die Kennung der Sitzung für einen Scope: eine Anfrage oder eine Verbindung
/// (Circuit) einer interaktiven Seite.
///
/// Gesetzt wird sie von <see cref="WebAuthenticationStateProvider"/>, dem Blazor
/// beim Start den Benutzer der Anfrage übergibt. Wo das nicht geschieht, etwa
/// außerhalb von Blazor, wird sie aus dem Cookie der laufenden Anfrage gelesen.
/// </summary>
public sealed class WebSession
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private string? id;

    public WebSession(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
    }

    public string? Id
    {
        get => id ??= httpContextAccessor.HttpContext is { } context ? SessionCookie.Read(context) : null;
        set => id = value;
    }
}

/// <summary>
/// Die Token-Ablage der Web-Variante: die Tokens der eigenen Sitzung im Speicher des
/// Web-Servers.
/// </summary>
public sealed class ServerSessionTokenStore : ITokenStore
{
    private readonly WebSession session;
    private readonly SessionTokenCache cache;
    private readonly ILogger<ServerSessionTokenStore> logger;

    public ServerSessionTokenStore(WebSession session, SessionTokenCache cache, ILogger<ServerSessionTokenStore> logger)
    {
        this.session = session;
        this.cache = cache;
        this.logger = logger;
    }

    public Task<AuthTokens?> GetAsync(CancellationToken cancellationToken = default) =>
        session.Id is { } id ? cache.GetAsync(id, cancellationToken) : Task.FromResult<AuthTokens?>(null);

    public Task SaveAsync(AuthTokens tokens, CancellationToken cancellationToken = default) =>
        cache.SaveAsync(RequireId(), tokens, cancellationToken);

    public Task ClearAsync(CancellationToken cancellationToken = default) =>
        session.Id is { } id ? cache.ClearAsync(id, cancellationToken) : Task.CompletedTask;

    // Ohne Kennung hat der Browser das Cookie der Sitzung nicht angenommen.
    private string RequireId()
    {
        if (session.Id is { } id)
        {
            return id;
        }
        logger.LogWarning("Keine Sitzung: Der Browser hat kein Sitzungs-Cookie geschickt, die Tokens werden nicht gespeichert.");
        throw new TokenStoreUnavailableException(
            ClientErrorCodes.CookiesRequired,
            "Keine Sitzung: Das Cookie " + SessionCookie.Name + " fehlt, die Tokens können nicht gespeichert werden.");
    }
}
