using Microsoft.AspNetCore.Identity;

namespace Duofy.Data;

/// <summary>
/// Ein Benutzer, der sich bei Duofy anmelden kann.
///
/// Die Klasse ist absichtlich leer. Alles, was zur Anmeldung gehört, kommt aus der
/// Basisklasse <see cref="IdentityUser{TKey}"/> von ASP.NET Core Identity, dem
/// fertigen Baustein von Microsoft: die E-Mail-Adresse, das verschlüsselte
/// Passwort, die Sperre nach zu vielen Fehlversuchen und so weiter. Passwort-Logik
/// selbst zu schreiben wäre eine unnötige Quelle für Sicherheitslücken.
///
/// Das Guid in den spitzen Klammern legt fest, dass die Kennung eines Benutzers
/// ein Guid ist, also derselbe Typ wie die Kennung eines Vertrags. Diese Kennung
/// steht bei allem, was einem Benutzer gehört, im Feld OwnerId.
///
/// Darunter stehen die Felder, die nur Duofy braucht.
///
/// <see cref="IdentityUser{TKey}.EmailConfirmed"/> zählt nur auf der gehosteten
/// Version (Spec 53): Dort meldet sich erst an, wer den Link aus der Mail geöffnet
/// hat. Konten von vor dieser Regel hat die Migration ConfirmExistingEmailsAndAddPasswordResetLinks als
/// bestätigt markiert.
/// </summary>
public class AppUser : IdentityUser<Guid>
{
    /// <summary>Der Vorname, 1 bis 100 Zeichen.</summary>
    public string FirstName { get; set; } = "";

    /// <summary>Der Nachname, 1 bis 100 Zeichen.</summary>
    public string LastName { get; set; } = "";

    /// <summary>
    /// Der Zeitpunkt, zu dem die Person bei der Registrierung der
    /// Datenschutzerklärung und den Nutzungsbedingungen zugestimmt hat.
    /// </summary>
    public DateTimeOffset TermsAcceptedAt { get; set; }

    /// <summary>
    /// Die Zielquote für Grundbedarf in ganzen Prozent; zusammen mit Wünschen und Sparen immer 100.
    /// Ein neuer Monat übernimmt die drei Werte, ältere Monate behalten ihre.
    /// </summary>
    public int QuotaNeeds { get; set; } = Duofy.Domain.Quotas.Default.Needs;

    /// <summary>Die Zielquote für Wünsche in ganzen Prozent.</summary>
    public int QuotaWants { get; set; } = Duofy.Domain.Quotas.Default.Wants;

    /// <summary>Die Zielquote für Sparen in ganzen Prozent.</summary>
    public int QuotaSavings { get; set; } = Duofy.Domain.Quotas.Default.Savings;

    /// <summary>
    /// True für einen Administrator des Servers (Spec 70): Er verwaltet die Einladungen zur Registrierung und
    /// sieht sonst nichts von anderen. Gesetzt wird das nur über den Einrichtungscode, solange es noch keinen
    /// Administrator gibt; einen Endpunkt, der es ändert, gibt es nicht.
    /// </summary>
    public bool IsInstanceAdmin { get; set; }
}
