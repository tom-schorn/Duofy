using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>
/// Die Verwaltung des Servers über die Duofy-API (/api/v1/admin, Spec 70): Einladungen zur Registrierung. Nur für
/// Administratoren; alle anderen bekommen den Fehlercode <see cref="ErrorCodes.AdminRequired"/>. Ohne öffentliche
/// Adresse des Servers entsteht keine Einladung (<see cref="ErrorCodes.PublicUrlMissing"/>).
/// </summary>
public interface IAdminApi
{
    /// <summary>Die offenen Einladungen, die neueste zuerst.</summary>
    Task<ApiResult<IReadOnlyList<RegistrationInvitationResponse>>> ListInvitationsAsync(CancellationToken cancellationToken = default);

    /// <summary>Legt eine Einladung an, sieben Tage gültig und einmal benutzbar; mit Adresse nur für diese.</summary>
    Task<ApiResult<RegistrationInvitationResponse>> CreateInvitationAsync(string? email, CancellationToken cancellationToken = default);

    /// <summary>Zieht eine Einladung zurück; der Link gilt danach nicht mehr.</summary>
    Task<ApiResult> RevokeInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ein Link zum Zurücksetzen des Passworts für die Person mit dieser Adresse (Spec 53, R4, nur auf einem selbst
    /// betriebenen Server), 24 Stunden gültig und einmal benutzbar. Gibt es zur Adresse kein Konto, ist der Fehlercode
    /// <see cref="ErrorCodes.UserNotFound"/>.
    /// </summary>
    Task<ApiResult<PasswordResetLinkResponse>> CreatePasswordResetLinkAsync(string email, CancellationToken cancellationToken = default);
}
