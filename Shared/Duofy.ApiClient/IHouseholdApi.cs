using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>
/// Der Haushalt über die Duofy-API (/api/v1/households und /api/v1/invitations, Spec 67). Ohne
/// Anmeldung ist der Fehlercode <see cref="ClientErrorCodes.NotSignedIn"/>; kein eigener Haushalt, ein
/// unbekannter oder fremder Haushalt, ein unbekanntes Mitglied und ein unbekannter Link ergeben
/// <see cref="ClientErrorCodes.NotFound"/>. Bei 400 und 409 ist der Fehlercode der Code der API, zum
/// Beispiel <c>founder_cannot_leave</c>. Verwalten darf nur der Gründer; eine Rolle ändern lässt sich nicht.
/// </summary>
public interface IHouseholdApi
{
    /// <summary>Gründet einen Haushalt; ohne Namen heißt er "Haushalt von Vorname".</summary>
    Task<ApiResult<HouseholdResponse>> CreateAsync(string? name, CancellationToken cancellationToken = default);

    /// <summary>Der Haushalt der angemeldeten Person mit seinen Mitgliedern; ohne Haushalt <see cref="ClientErrorCodes.NotFound"/>.</summary>
    Task<ApiResult<HouseholdResponse>> GetMineAsync(CancellationToken cancellationToken = default);

    /// <summary>Benennt um oder ändert die Quoten; nur der Gründer.</summary>
    Task<ApiResult<HouseholdResponse>> UpdateAsync(Guid householdId, UpdateHouseholdRequest request, CancellationToken cancellationToken = default);

    /// <summary>Entfernt ein anderes Mitglied; es ist danach in keinem Haushalt. Nur der Gründer.</summary>
    Task<ApiResult> RemoveMemberAsync(Guid householdId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Tritt aus; die Person ist danach in keinem Haushalt. Der Gründer löst ihn so auf, wenn er allein ist.</summary>
    Task<ApiResult> LeaveAsync(Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Setzt, was die angemeldete Person dem Mitglied mit ihren Daten erlaubt (Spec 68); nur die geschickten
    /// Bereiche ändern sich. Liefert das Mitglied mit seinen neuen Stufen.
    /// </summary>
    Task<ApiResult<MemberResponse>> SetGrantsAsync(Guid householdId, Guid granteeId, UpdateGrantsRequest request, CancellationToken cancellationToken = default);

    /// <summary>Die offenen Einladungen; nur der Gründer.</summary>
    Task<ApiResult<IReadOnlyList<InvitationResponse>>> ListInvitationsAsync(Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>Legt eine Einladung an, 14 Tage gültig und einmal benutzbar; nur der Gründer.</summary>
    /// <remarks>
    /// Mit <paramref name="email"/> geht der Link auf der gehosteten Version per Mail an die Adresse, wenn sie zu einem
    /// bestätigten Konto gehört (Spec 53, R6); die Antwort verrät nicht, ob.
    /// </remarks>
    Task<ApiResult<InvitationResponse>> CreateInvitationAsync(Guid householdId, string? email = null, CancellationToken cancellationToken = default);

    /// <summary>Zieht eine Einladung zurück; nur der Gründer.</summary>
    Task<ApiResult> RevokeInvitationAsync(Guid householdId, Guid invitationId, CancellationToken cancellationToken = default);

    /// <summary>Der Haushaltsplan eines Monats (Spec 69), nur zum Lesen; legt die fehlenden Monate der Mitglieder an.</summary>
    Task<ApiResult<HouseholdPlanResponse>> GetPlanAsync(Guid householdId, int year, int month, CancellationToken cancellationToken = default);

    /// <summary>Die Monate, für die es einen Haushaltsplan gibt, der neueste zuerst.</summary>
    Task<ApiResult<IReadOnlyList<HouseholdPlanMonth>>> ListPlansAsync(Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>Was hinter einem Einladungslink steckt; geht auch ohne Anmeldung.</summary>
    Task<ApiResult<InvitationPreview>> PreviewInvitationAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>Tritt mit der Einladung bei und liefert den Haushalt; nur, wer in keinem Haushalt ist.</summary>
    Task<ApiResult<HouseholdResponse>> AcceptInvitationAsync(string token, CancellationToken cancellationToken = default);
}
