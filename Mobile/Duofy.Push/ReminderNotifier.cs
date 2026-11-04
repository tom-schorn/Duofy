namespace Duofy.Push;

/// <summary>
/// Was die Benachrichtigung zeigt und wohin ein Tipp führt. <see cref="Path"/> ist die Adresse in der App
/// (der Monat des Postens); <see cref="WithActions"/> ist false, wenn der Text nicht geholt werden konnte: Dann gibt es
/// weder Namen noch Knöpfe, nur den Weg in die App.
/// </summary>
public sealed record ReminderNotification(Guid ReminderId, string Title, string Body, string Path, bool WithActions);

/// <summary>
/// Die Benachrichtigungen des Systems (Spec 84): Auf Android der NotificationManager, auf Windows die App-Benachrichtigung.
/// Die Kennung ist die der Erinnerung, damit dieselbe Erinnerung nie zweimal erscheint und ein Signal "withdraw"
/// genau diese Benachrichtigung findet.
/// </summary>
public interface IReminderNotifier
{
    /// <summary>Zeigt die Benachrichtigung, oder ersetzt die mit derselben Erinnerung.</summary>
    Task ShowAsync(ReminderNotification notification, CancellationToken cancellationToken = default);

    /// <summary>Zeigt eine kurze Meldung anstelle der Benachrichtigung, zum Beispiel wenn "Bezahlt" abgelehnt wurde.</summary>
    Task ReplaceWithMessageAsync(Guid reminderId, string message, string path, CancellationToken cancellationToken = default);

    /// <summary>Nimmt die Benachrichtigung dieser Erinnerung weg; gibt es sie nicht, geschieht nichts.</summary>
    Task CancelAsync(Guid reminderId, CancellationToken cancellationToken = default);
}

/// <summary>Die Adressen, die eine Benachrichtigung in der App öffnet.</summary>
public static class ReminderPaths
{
    /// <summary>
    /// Der Monat des Postens; Posten und Besitzer hängen als Abfrage daran, damit die Monatsseite den Haken-Dialog des
    /// Postens öffnen kann (Spec 84, R5).
    /// </summary>
    public static string Month(int year, int month, Guid positionId, Guid ownerId) =>
        $"/plan/{year}/{month}?haken={positionId}&besitzer={ownerId}";

    /// <summary>Ohne Text weiß die App den Monat nicht und öffnet den laufenden.</summary>
    public const string CurrentMonth = "/plan";

    private static readonly System.Text.RegularExpressions.Regex Allowed = new(
        @"^/plan(/\d{4}/([1-9]|1[0-2])(\?haken=[0-9a-fA-F-]{36}&besitzer=[0-9a-fA-F-]{36})?)?$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Ob eine Adresse eine ist, die eine Benachrichtigung dieser App öffnen darf: genau die Formen von <see cref="Month"/>
    /// und <see cref="CurrentMonth"/>. Die Hauptaktivität ist für das Starten durch andere Apps offen (sie ist der
    /// Launcher); eine fremde App darf der Oberfläche darüber keine beliebige Seite aufdrängen.
    /// </summary>
    public static bool IsAllowed(string? path) => path is not null && path.Length <= 120 && Allowed.IsMatch(path);
}
