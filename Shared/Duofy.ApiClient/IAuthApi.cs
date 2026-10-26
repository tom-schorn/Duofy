using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>
/// Registrierung, Anmeldung und Abmeldung über die Duofy-API (/api/v1/auth).
/// </summary>
public interface IAuthApi
{
    /// <summary>
    /// Legt ein neues Konto an. Meldet nicht an; dafür danach <see cref="LoginAsync"/> aufrufen.
    /// </summary>
    Task<ApiResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Meldet mit E-Mail und Passwort an. Bei Erfolg liegen die Tokens im
    /// <see cref="ITokenStore"/> und der Anmeldezustand ist "angemeldet".
    /// Bei falschem Passwort, unbekannter E-Mail oder gesperrtem Konto ist der
    /// Fehlercode immer <see cref="ErrorCodes.LoginFailed"/>.
    /// </summary>
    Task<ApiResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Meldet nur auf diesem Gerät ab: löscht die Tokens aus dem <see cref="ITokenStore"/>.
    /// Fragt die API nicht.
    /// </summary>
    Task LogoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Meldet auf allen Geräten ab. Bei Erfolg sind auch auf diesem Gerät die Tokens gelöscht.
    /// </summary>
    Task<ApiResult> LogoutEverywhereAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Bestätigt die Adresse mit dem Link aus der Mail (Spec 53, nur auf der gehosteten Version). Ein benutzter oder
    /// abgelaufener Link ergibt <see cref="ErrorCodes.TokenInvalid"/>.
    /// </summary>
    Task<ApiResult> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken cancellationToken = default);

    /// <summary>Schickt den Link zum Bestätigen noch einmal; klappt immer, auch wenn keine Mail hinausgeht.</summary>
    Task<ApiResult> ResendConfirmationAsync(ResendConfirmationRequest request, CancellationToken cancellationToken = default);

    /// <summary>"Passwort vergessen" auf der gehosteten Version; klappt immer, auch für eine unbekannte Adresse.</summary>
    Task<ApiResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>Setzt mit dem Link aus der Mail ein neues Passwort; danach ist die Person überall abgemeldet.</summary>
    Task<ApiResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>Setzt mit dem Link des Administrators ein neues Passwort (selbst betriebener Server).</summary>
    Task<ApiResult> ResetPasswordByLinkAsync(ResetPasswordByLinkRequest request, CancellationToken cancellationToken = default);

    /// <summary>Übernimmt eine neue Adresse mit dem Link, der an sie ging.</summary>
    Task<ApiResult> ConfirmEmailChangeAsync(ConfirmEmailChangeRequest request, CancellationToken cancellationToken = default);
}
