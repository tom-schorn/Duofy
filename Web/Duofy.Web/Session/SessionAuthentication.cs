using System.Security.Claims;
using System.Text.Encodings.Web;
using Duofy.ApiClient;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;

namespace Duofy.Web.Session;

/// <summary>
/// Die Anmeldung der Web-Variante für normale HTTP-Anfragen: Angemeldet ist, wessen
/// Sitzung Tokens hat. Wer eine geschützte Seite ohne Anmeldung öffnet, wird zur
/// Anmeldeseite geschickt und kommt danach zurück.
///
/// Auch ohne Anmeldung trägt der Benutzer der Anfrage die Kennung der Sitzung als
/// Claim. So erfährt die interaktive Verbindung, zu welcher Sitzung sie gehört.
/// </summary>
public sealed class SessionAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "DuofySession";

    /// <summary>Die Anmeldeseite aus Duofy.Shared.</summary>
    public const string SignInPath = "/anmelden";

    private readonly SessionTokenCache cache;

    public SessionAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        SessionTokenCache cache)
        : base(options, logger, encoder)
    {
        this.cache = cache;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (SessionCookie.Read(Context) is not { } id)
        {
            return AuthenticateResult.NoResult();
        }

        var claims = new[] { new Claim(SessionCookie.ClaimType, id) };
        var signedIn = await cache.GetAsync(id, Context.RequestAborted) is not null;
        // Ohne Anmeldeart (zweiter Parameter) gilt die Identität als nicht angemeldet.
        var identity = signedIn ? new ClaimsIdentity(claims, SchemeName) : new ClaimsIdentity(claims);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var returnUrl = Request.PathBase + Request.Path + Request.QueryString;
        Response.Redirect($"{Request.PathBase}{SignInPath}?returnUrl={Uri.EscapeDataString(returnUrl)}");
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        // Weil auch Besucher ohne Anmeldung ein "erfolgreiches" Ergebnis mit der
        // Kennung ihrer Sitzung bekommen, meldet ASP.NET Core bei ihnen "verboten"
        // statt "nicht angemeldet". Sie gehören aber zur Anmeldeseite.
        if (Context.User.Identity?.IsAuthenticated != true)
        {
            return HandleChallengeAsync(properties);
        }
        return base.HandleForbiddenAsync(properties);
    }
}

/// <summary>
/// Der Anmeldezustand für die Seiten der Web-Variante. Er arbeitet wie in der App
/// mit der Token-Ablage; zusätzlich nimmt er von Blazor den Benutzer der Anfrage
/// entgegen und merkt sich daraus die Kennung der Sitzung.
/// </summary>
public sealed class WebAuthenticationStateProvider : TokenAuthenticationStateProvider, IHostEnvironmentAuthenticationStateProvider
{
    private readonly WebSession session;

    public WebAuthenticationStateProvider(ITokenStore tokenStore, WebSession session) : base(tokenStore)
    {
        this.session = session;
    }

    /// <summary>
    /// Ruft Blazor beim Vorab-Rendern einer Seite und beim Aufbau der interaktiven
    /// Verbindung auf, jeweils mit dem Benutzer aus <see cref="SessionAuthenticationHandler"/>.
    /// </summary>
    public void SetAuthenticationState(Task<AuthenticationState> authenticationStateTask)
    {
        if (authenticationStateTask.IsCompletedSuccessfully
            && authenticationStateTask.Result.User.FindFirst(SessionCookie.ClaimType)?.Value is { } id)
        {
            session.Id = id;
        }
        NotifyTokensChanged();
    }
}

public static class WebSessionServiceCollectionExtensions
{
    /// <summary>
    /// Registriert Sitzung, Token-Ablage und Anmeldung der Web-Variante. Muss nach
    /// AddDuofyApiClient aufgerufen werden, weil es dessen Anmeldezustand ersetzt.
    /// </summary>
    public static IServiceCollection AddWebSession(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddDistributedMemoryCache();
        services.AddSingleton<SessionTokenCache>();
        services.AddScoped<WebSession>();
        services.AddScoped<ITokenStore, ServerSessionTokenStore>();
        services.AddScoped<TokenAuthenticationStateProvider, WebAuthenticationStateProvider>();

        services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, null);
        services.AddAuthorization();
        return services;
    }
}
