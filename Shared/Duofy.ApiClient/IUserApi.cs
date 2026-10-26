using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>
/// Das eigene Konto des angemeldeten Benutzers über die Duofy-API (/api/v1/users/me).
/// Ohne Anmeldung ist der Fehlercode <see cref="ClientErrorCodes.NotSignedIn"/>.
/// </summary>
public interface IUserApi
{
    /// <summary>Liefert E-Mail, Vor- und Nachname des angemeldeten Benutzers.</summary>
    Task<ApiResult<MeResponse>> GetMeAsync(CancellationToken cancellationToken = default);

    /// <summary>Ändert Vor- und Nachname und liefert den neuen Stand.</summary>
    Task<ApiResult<MeResponse>> UpdateMeAsync(UpdateMeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Ändert das Passwort; das alte gilt danach nicht mehr.</summary>
    Task<ApiResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Löscht das Konto mit allen Daten. Bei Erfolg sind auch die Tokens auf diesem
    /// Gerät gelöscht und der Anmeldezustand ist "nicht angemeldet".
    /// </summary>
    Task<ApiResult> DeleteMeAsync(DeleteMeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ändert die E-Mail-Adresse mit dem aktuellen Passwort (Spec 53, R5). Auf der gehosteten Version wartet die
    /// Änderung auf die Bestätigung über die neue Adresse: Dann ist der Wert null. Auf einem selbst betriebenen Server
    /// gilt sie sofort: Dann ist der Wert das Konto mit der neuen Adresse, und dieses Gerät ist damit neu angemeldet.
    /// </summary>
    Task<ApiResult<MeResponse?>> ChangeEmailAsync(ChangeEmailRequest request, CancellationToken cancellationToken = default);

    /// <summary>Liefert die Zielquoten (ohne eigene Einstellung 50/30/20).</summary>
    Task<ApiResult<QuotaSettings>> GetQuotasAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Macht das Konto mit dem Einrichtungscode zum Administrator des Servers (Spec 70, R1); das geht nur, solange es
    /// keinen gibt. Sonst ist der Fehlercode <see cref="ErrorCodes.SetupCodeInvalid"/>.
    /// </summary>
    Task<ApiResult<MeResponse>> ClaimAdminAsync(ClaimAdminRequest request, CancellationToken cancellationToken = default);

    /// <summary>Ändert die Zielquoten; sie gelten für Monate, die danach angelegt werden.</summary>
    Task<ApiResult<QuotaSettings>> UpdateQuotasAsync(QuotaSettings request, CancellationToken cancellationToken = default);
}
