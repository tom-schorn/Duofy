namespace Duofy.Schemas;

/// <summary>Der angemeldete Benutzer (GET und PATCH /api/v1/users/me).</summary>
/// <param name="IsInstanceAdmin">
/// True, wenn die Person diesen Server verwaltet (Spec 70): Unter "Mehr" gibt es dann den Eintrag Admin, und das Konto
/// lässt sich nicht löschen, solange sie der einzige Administrator ist.
/// </param>
public sealed record MeResponse(Guid Id, string Email, string FirstName, string LastName, bool IsInstanceAdmin = false);

/// <summary>Neue Namen für den angemeldeten Benutzer (PATCH /api/v1/users/me).</summary>
public sealed record UpdateMeRequest(string FirstName, string LastName);

/// <summary>Ein neues Passwort (POST /api/v1/users/me/password).</summary>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>
/// Das Löschen des eigenen Kontos (DELETE /api/v1/users/me). Das Passwort wird
/// zur Sicherheit noch einmal abgefragt.
/// </summary>
public sealed record DeleteMeRequest(string Password);

/// <summary>
/// Ein bestehendes Konto wird mit dem Einrichtungscode Administrator (POST /api/v1/users/me/claim-admin, Spec 70, R1);
/// das geht nur, solange es keinen Administrator gibt.
/// </summary>
public sealed record ClaimAdminRequest(string SetupCode);

/// <summary>
/// Die Regeln für Namen und Passwörter. Die API prüft sie, die Oberfläche kann
/// sie zusätzlich schon beim Tippen anzeigen.
/// </summary>
public static class AccountRules
{
    /// <summary>Die größte erlaubte Länge eines Vor- oder Nachnamens.</summary>
    public const int NameMaxLength = 100;

    /// <summary>Die kleinste erlaubte Länge eines Passworts.</summary>
    public const int PasswordMinLength = 10;
}

/// <summary>
/// Eine neue E-Mail-Adresse für das eigene Konto (POST /api/v1/users/me/email, Spec 53, R5), mit dem aktuellen
/// Passwort. Auf der gehosteten Version antwortet die API 202 und schickt der neuen Adresse einen Link; erst damit
/// gilt sie. Auf einem selbst betriebenen Server gilt sie sofort (200 mit dem Konto).
/// </summary>
public sealed record ChangeEmailRequest(string NewEmail, string Password);
