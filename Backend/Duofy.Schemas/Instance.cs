namespace Duofy.Schemas;

/// <summary>
/// Die Antwort von GET /api/v1/instance: was der Server anbietet. Die Abfrage braucht
/// keine Anmeldung, weil die App sie vor der Anmeldung stellt.
/// </summary>
/// <param name="SignInMethods">Die Anmeldewege des Servers, heute nur <see cref="SignInMethodNames.Password"/>.</param>
/// <param name="RegistrationMode">
/// Wer sich registrieren darf (<see cref="RegistrationModes"/>); null bei einem älteren Server, der das Feld noch
/// nicht kennt.
/// </param>
/// <param name="SetupPending">
/// True, solange es keinen Administrator gibt und ein Einrichtungscode eingestellt ist: Dann zeigen Registrierung und
/// Benutzerkonto das Feld Einrichtungscode.
/// </param>
/// <param name="LegalDocuments">
/// Die Namen der Rechtstexte, die dieser Server anbietet (<see cref="LegalDocumentNames"/>); null bei einem
/// älteren Server, der das Feld noch nicht kennt.
/// </param>
/// <param name="Hosted">
/// True auf der gehosteten Version (Duofy:Hosted, Spec 53): Dann gibt es Mails, "Passwort vergessen" schickt einen
/// Link, und eine neue Adresse gilt erst nach Bestätigung. False auf einem selbst betriebenen Server: Dort setzt der
/// Administrator vergessene Passwörter mit einem Link zurück.
/// </param>
public sealed record InstanceResponse(
    IReadOnlyList<string> SignInMethods,
    string? RegistrationMode = null,
    bool SetupPending = false,
    IReadOnlyList<string>? LegalDocuments = null,
    bool Hosted = false);

/// <summary>Die Registrierungsmodi in <see cref="InstanceResponse.RegistrationMode"/>, aus Duofy:RegistrationMode.</summary>
public static class RegistrationModes
{
    /// <summary>Jede Person kann sich registrieren.</summary>
    public const string Open = "open";

    /// <summary>Nur mit einer Einladung zur Registrierung; der Standard.</summary>
    public const string Invite = "invite";

    /// <summary>Niemand kann sich registrieren, außer mit dem Einrichtungscode, solange es keinen Administrator gibt.</summary>
    public const string Closed = "closed";

    /// <summary>Alle drei, von offen nach geschlossen.</summary>
    public static readonly IReadOnlyList<string> All = [Open, Invite, Closed];
}

/// <summary>Die Namen der Anmeldewege in <see cref="InstanceResponse.SignInMethods"/>.</summary>
public static class SignInMethodNames
{
    /// <summary>E-Mail und Passwort.</summary>
    public const string Password = "password";
}
