using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Duofy.ApiClient;

/// <summary>
/// Beantwortet für die Seiten die Frage "ist jemand angemeldet?": Angemeldet ist,
/// wer Tokens im <see cref="ITokenStore"/> liegen hat.
///
/// Die Clients dieses Projekts melden jede Änderung an den Tokens hierher, damit
/// zum Beispiel die Navigation nach dem Anmelden sofort umschaltet. Das
/// Access-Token selbst ist für den Client unlesbar; Name und E-Mail holt eine
/// Seite deshalb über <see cref="IUserApi.GetMeAsync"/>.
/// </summary>
public class TokenAuthenticationStateProvider : AuthenticationStateProvider
{
    /// <summary>Der Name, unter dem die Anmeldung im ClaimsIdentity steht.</summary>
    public const string AuthenticationType = "Duofy";

    private static readonly AuthenticationState SignedOut = new(new ClaimsPrincipal(new ClaimsIdentity()));
    private static readonly AuthenticationState SignedIn =
        new(new ClaimsPrincipal(new ClaimsIdentity(authenticationType: AuthenticationType)));

    private readonly ITokenStore tokenStore;

    // Die zuletzt ermittelte Antwort. Seiten fragen oft; die Ablage wird nur nach
    // einer Änderung neu gelesen.
    private Task<AuthenticationState>? current;

    public TokenAuthenticationStateProvider(ITokenStore tokenStore)
    {
        this.tokenStore = tokenStore;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // Ist das Lesen beim letzten Mal schiefgegangen, wird es neu versucht,
        // statt den Fehler für immer zu behalten.
        if (current is null || current.IsFaulted || current.IsCanceled)
        {
            current = LoadAsync();
        }
        return current;
    }

    /// <summary>
    /// Liest die Ablage neu und sagt allen Seiten Bescheid. Die Clients rufen das
    /// nach jedem Speichern oder Löschen der Tokens auf; ein Host ruft es auf, wenn
    /// er die Ablage selbst geändert hat.
    /// </summary>
    public void NotifyTokensChanged()
    {
        current = LoadAsync();
        NotifyAuthenticationStateChanged(current);
    }

    private async Task<AuthenticationState> LoadAsync()
    {
        var tokens = await tokenStore.GetAsync();
        return tokens is null ? SignedOut : SignedIn;
    }
}
