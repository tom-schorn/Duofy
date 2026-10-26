namespace Duofy.Schemas;

/// <summary>Die Rollen in einem Haushalt, so wie die API sie schickt. Ändern lässt sich keine.</summary>
public static class HouseholdRoles
{
    public const string Founder = "founder";
    public const string Member = "member";
}

/// <summary>Der Haushalt der angemeldeten Person (GET /api/v1/households/mine und nach dem Beitreten).</summary>
/// <param name="Members">Die Mitglieder in der Reihenfolge, in der sie dazukamen.</param>
public sealed record HouseholdResponse(
    Guid Id,
    string Name,
    int QuotaNeeds,
    int QuotaWants,
    int QuotaSavings,
    IReadOnlyList<MemberResponse> Members);

/// <summary>Ein Mitglied des Haushalts.</summary>
/// <param name="Role">Siehe <see cref="HouseholdRoles"/>.</param>
/// <param name="IsMe">True für die angemeldete Person selbst ("du").</param>
/// <param name="GrantsToMe">Was dieses Mitglied der angemeldeten Person erlaubt (Spec 68); bei "du" überall Nichts.</param>
/// <param name="MyGrants">Was die angemeldete Person diesem Mitglied erlaubt; bei "du" überall Nichts.</param>
public sealed record MemberResponse(
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    string Role,
    bool IsMe,
    GrantLevels GrantsToMe,
    GrantLevels MyGrants);

/// <summary>
/// Gründet einen Haushalt (POST /api/v1/households). Ohne Namen heißt er "Haushalt von Vorname".
/// </summary>
public sealed record CreateHouseholdRequest(string? Name = null);

/// <summary>
/// Ändert Namen und Quoten (PATCH /api/v1/households/{id}); nur der Gründer. Was null ist, bleibt; die
/// Quoten kommen zusammen oder gar nicht.
/// </summary>
public sealed record UpdateHouseholdRequest(string? Name = null, int? QuotaNeeds = null, int? QuotaWants = null, int? QuotaSavings = null);

/// <summary>Eine offene Einladung, nur für den Gründer.</summary>
/// <param name="Token">Das Geheimnis im Link.</param>
/// <param name="Url">
/// Der ganze Link <c>&lt;PublicUrl&gt;/einladung/&lt;Token&gt;</c> mit der öffentlichen Adresse des Servers
/// (Duofy:PublicUrl), nie mit der Adresse, über die ein Client gerade verbunden ist. Null nur in der Liste,
/// wenn dem Server die öffentliche Adresse inzwischen fehlt; neue Einladungen gibt es dann nicht.
/// </param>
public sealed record InvitationResponse(Guid Id, string Token, DateTimeOffset ExpiresAt, string? Url);

/// <summary>
/// Eine neue Einladung in den Haushalt (POST /api/v1/households/{id}/invitations); der Inhalt darf fehlen.
/// </summary>
/// <param name="Email">
/// Nur auf der gehosteten Version (Spec 53, R6): Gehört die Adresse zu einem bestätigten Konto, geht der Link per Mail
/// dorthin. Die Antwort ist dieselbe, ob eine Mail hinausging oder nicht.
/// </param>
public sealed record CreateHouseholdInvitationRequest(string? Email = null);

/// <summary>
/// Was ein Einladungslink vor dem Beitreten zeigt (GET /api/v1/invitations/{token}, ohne Anmeldung):
/// nur Name des Haushalts, wer einlädt und bis wann; nie die Mitglieder.
/// </summary>
/// <param name="InvitedBy">Vor- und Nachname der einladenden Person.</param>
public sealed record InvitationPreview(string HouseholdName, string InvitedBy, DateTimeOffset ExpiresAt);
