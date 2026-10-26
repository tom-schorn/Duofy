namespace Duofy.Schemas;

/// <summary>
/// Die Registrierung eines neuen Benutzers (POST /api/v1/auth/register).
///
/// Vor- und Nachname haben nach dem Kürzen der Leerzeichen 1 bis 100 Zeichen,
/// das Passwort mindestens 10 Zeichen. <see cref="AcceptedTerms"/> muss true sein:
/// Die Person stimmt der Datenschutzerklärung und den Nutzungsbedingungen zu.
/// </summary>
/// <param name="SetupCode">
/// Der Einrichtungscode aus der Umgebung des Servers (Spec 70, R1). Solange es keinen Administrator gibt, macht der
/// richtige Code die Person zum Administrator, in jedem Registrierungsmodus.
/// </param>
/// <param name="InvitationToken">Das Token einer Einladung zur Registrierung; im Modus "invite" nötig.</param>
public sealed record RegisterRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    bool AcceptedTerms,
    string? SetupCode = null,
    string? InvitationToken = null);

/// <summary>Die Anmeldung mit E-Mail und Passwort (POST /api/v1/auth/login).</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>
/// Der Tausch eines Refresh-Tokens gegen ein neues Token-Paar
/// (POST /api/v1/auth/refresh).
/// </summary>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>
/// Die Antwort auf Anmeldung und Refresh.
///
/// <see cref="AccessToken"/> gilt <see cref="ExpiresIn"/> Sekunden (15 Minuten) und
/// geht bei jeder Anfrage im Header "Authorization: Bearer ..." mit.
/// <see cref="RefreshToken"/> gilt 30 Tage und wird nur gegen ein neues Paar getauscht.
/// </summary>
public sealed record TokenResponse(
    string TokenType,
    string AccessToken,
    long ExpiresIn,
    string RefreshToken);

/// <summary>
/// Bestätigt die Adresse eines neuen Kontos mit dem Link aus der Mail (POST /api/v1/auth/confirm-email, Spec 53,
/// nur auf der gehosteten Version). Ein benutztes oder abgelaufenes Token ergibt 400 token_invalid.
/// </summary>
public sealed record ConfirmEmailRequest(Guid UserId, string Token);

/// <summary>
/// Schickt den Bestätigungslink noch einmal (POST /api/v1/auth/confirm-email/resend). Die Antwort ist immer 202,
/// auch für unbekannte oder schon bestätigte Adressen; höchstens eine Mail alle fünf Minuten.
/// </summary>
public sealed record ResendConfirmationRequest(string Email);

/// <summary>
/// "Passwort vergessen" auf der gehosteten Version (POST /api/v1/auth/password/forgot): Die Antwort ist immer 202;
/// nur eine Adresse mit Konto bekommt einen Link, eine Stunde gültig.
/// </summary>
public sealed record ForgotPasswordRequest(string Email);

/// <summary>Setzt mit dem Link aus der Mail ein neues Passwort (POST /api/v1/auth/password/reset); meldet überall ab.</summary>
public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);

/// <summary>
/// Setzt mit dem Link des Administrators ein neues Passwort (POST /api/v1/auth/password/reset-by-link, nur auf einem
/// selbst betriebenen Server). Der Link gilt einmal und 24 Stunden.
/// </summary>
public sealed record ResetPasswordByLinkRequest(string Token, string NewPassword);

/// <summary>Übernimmt die neue Adresse mit dem Link aus der Mail an sie (POST /api/v1/auth/confirm-email-change).</summary>
public sealed record ConfirmEmailChangeRequest(Guid UserId, string NewEmail, string Token);
