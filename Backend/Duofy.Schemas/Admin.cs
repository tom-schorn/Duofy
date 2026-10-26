namespace Duofy.Schemas;

/// <summary>
/// Eine offene Einladung zur Registrierung auf diesem Server (GET und POST /api/v1/admin/invitations, Spec 70, R4).
/// Nur Administratoren bekommen sie; sie nennt niemanden außer der Adresse, für die sie gilt.
/// </summary>
/// <param name="Email">Die Adresse, für die sie allein gilt; null heißt: für jede Adresse.</param>
/// <param name="Url">
/// Der ganze Link <c>&lt;PublicUrl&gt;/registrieren?einladung=&lt;Token&gt;</c> mit der öffentlichen Adresse des Servers;
/// null, wenn sie fehlt.
/// </param>
public sealed record RegistrationInvitationResponse(Guid Id, string Token, string? Email, DateTimeOffset ExpiresAt, string? Url);

/// <summary>Eine neue Einladung zur Registrierung (POST /api/v1/admin/invitations); ohne Adresse gilt sie für jede.</summary>
public sealed record CreateRegistrationInvitationRequest(string? Email = null);

/// <summary>
/// Ein Link zum Zurücksetzen des Passworts für die Person mit dieser Adresse (POST /api/v1/admin/password-reset-links,
/// Spec 53, R4, nur auf einem selbst betriebenen Server).
/// </summary>
public sealed record CreatePasswordResetLinkRequest(string Email);

/// <summary>Der Link zum Zurücksetzen: <c>&lt;PublicUrl&gt;/passwort-neu?link=&lt;Token&gt;</c>, einmal benutzbar, bis <paramref name="ExpiresAt"/>.</summary>
public sealed record PasswordResetLinkResponse(string Url, DateTimeOffset ExpiresAt);
