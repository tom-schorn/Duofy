using System.Globalization;
using System.Text.Json;
using Duofy.ApiClient;
using Duofy.Schemas;

namespace Duofy.Push;

/// <summary>
/// Das stille Signal (Spec 84, R4): nur die zufällige Kennung der Erinnerung und die Art, nie ein Name oder Betrag.
/// So kommt es von FCM (Datenfelder) und von WNS (eine Raw-Benachrichtigung mit demselben Inhalt als JSON).
/// </summary>
public sealed record PushSignal(Guid ReminderId, string Type)
{
    /// <summary>Ein Posten ist fällig: Die App holt den Text und zeigt die Benachrichtigung.</summary>
    public const string Due = "due";

    /// <summary>Die Abendzusammenfassung des Haushalts liegt bereit (Spec 118): Die App holt den Text und zeigt die Benachrichtigung.</summary>
    public const string Household = "household";

    /// <summary>Der Posten ist abgehakt: Die App nimmt die Benachrichtigung weg.</summary>
    public const string Withdraw = "withdraw";

    /// <summary>Die Namen der Felder, so wie der Server sie schickt.</summary>
    public const string ReminderIdKey = "reminderId";

    public const string TypeKey = "type";

    /// <summary>Liest die Datenfelder einer FCM-Nachricht; false bei allem, was nicht genau so aussieht.</summary>
    public static bool TryParse(IReadOnlyDictionary<string, string> data, out PushSignal? signal)
    {
        signal = null;
        if (!data.TryGetValue(ReminderIdKey, out var id) || !Guid.TryParse(id, out var reminderId)
            || !data.TryGetValue(TypeKey, out var type) || type is not (Due or Household or Withdraw))
        {
            return false;
        }
        signal = new PushSignal(reminderId, type);
        return true;
    }

    /// <summary>Liest den Inhalt einer WNS-Raw-Benachrichtigung ({"reminderId":"...","type":"due"}).</summary>
    public static bool TryParseJson(string? json, out PushSignal? signal)
    {
        signal = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }
            var data = document.RootElement.EnumerateObject()
                .Where(p => p.Value.ValueKind == JsonValueKind.String)
                .ToDictionary(p => p.Name, p => p.Value.GetString()!);
            return TryParse(data, out signal);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

/// <summary>
/// Die Texte der Benachrichtigung und ihrer Knöpfe (Spec 84, R4, R5; Spec 118, R9). Die sichtbaren Texte entstehen erst hier in
/// der App aus dem, was der Server nach der Anmeldung liefert: Name und Betrag stehen nie im Signal an den Anbieter.
/// </summary>
public static class ReminderTexts
{
    public const string PaidAction = "Bezahlt";

    public const string LaterAction = "Später";

    /// <summary>Der Text, wenn der Server nicht erreichbar ist: Er nennt keinen Posten, ein Tipp öffnet den Monat.</summary>
    public const string FallbackTitle = "Ein Posten ist heute fällig";

    public const string BodyWithLater = "Bezahlt hakt den Posten ab, Später erinnert dich morgen wieder.";

    /// <summary>Nach dem dritten Verschieben gibt es kein "Später" mehr (Spec 118, R6).</summary>
    public const string BodyWithoutLater = "Bezahlt hakt den Posten ab.";

    public const string FallbackBody = "Tippe, um den Monat zu öffnen.";

    /// <summary>Der Name und die Beschreibung des Benachrichtigungskanals auf Android.</summary>
    public const string ChannelName = "Erinnerungen";

    public const string ChannelDescription = "Erinnert dich an Posten, die fällig sind, und fasst abends zusammen, was im Haushalt geschah.";

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Ein Betrag im Format der App: deutsches Zahlenformat mit Euro-Zeichen, ganze Beträge ohne Cent ("850 €", "18,36 €").</summary>
    public static string Money(decimal amount) =>
        amount == decimal.Truncate(amount) ? amount.ToString("N0", German) + " €" : amount.ToString("N2", German) + " €";

    /// <summary>Wie das Holen ohne Namen: "Miete 850 € ist heute fällig". Ohne Betrag (kein Recht auf den Plan) steht nur der Name da.</summary>
    public static string Title(ReminderResponse reminder)
    {
        var name = reminder.AmountPlanned is { } amount ? $"{reminder.Label} {Money(amount)}" : reminder.Label;
        return reminder.Kind switch
        {
            ReminderKinds.Advance => reminder.DaysUntilDue == 1 ? $"{name} ist morgen fällig" : $"{name} ist in {reminder.DaysUntilDue} Tagen fällig",
            ReminderKinds.Overdue => reminder.DaysUntilDue switch
            {
                -1 => $"{name} ist seit gestern überfällig",
                < -1 => $"{name} ist seit {-reminder.DaysUntilDue} Tagen überfällig",
                _ => $"{name} ist überfällig",
            },
            _ => $"{name} ist heute fällig",
        };
    }

    /// <summary>Der Text unter dem Titel: was die Knöpfe tun und, ab dem ersten Verschieben, wie oft es schon geschah.</summary>
    public static string BodyOf(ReminderResponse reminder)
    {
        var buttons = reminder.LaterAllowed ? BodyWithLater : BodyWithoutLater;
        return reminder.PostponeCount > 0 ? $"{reminder.PostponeCount}× verschoben. {buttons}" : buttons;
    }

    /// <summary>So viele Ereignisse nennt die Zusammenfassung des Haushalts einzeln; der Rest zählt nur.</summary>
    public const int HouseholdLines = 5;

    public static string HouseholdTitle(IReadOnlyList<HouseholdEventLine> events) =>
        events.Count == 1 ? $"Haushalt heute: {HouseholdLine(events[0])}" : $"Haushalt heute: {events.Count} Ereignisse";

    /// <summary>Die Liste, eine Zeile je Ereignis; mehr als <see cref="HouseholdLines"/> werden zu "und n weitere".</summary>
    public static string HouseholdBody(IReadOnlyList<HouseholdEventLine> events)
    {
        var lines = events.Take(HouseholdLines).Select(HouseholdLine).ToList();
        if (events.Count > HouseholdLines)
        {
            lines.Add($"und {events.Count - HouseholdLines} weitere");
        }
        return string.Join("\n", lines);
    }

    public static string HouseholdLine(HouseholdEventLine e)
    {
        var what = e.Label is { Length: > 0 } label ? (e.Amount is { } amount ? $"{label} {Money(amount)}" : label) : "";
        return e.Kind switch
        {
            "ticked" => $"{e.ActorName} hat {what} abgehakt",
            "booked" => $"{e.ActorName} hat {what} gebucht",
            "invited" => $"{e.ActorName} hat jemanden in den Haushalt eingeladen",
            "joined" => $"{e.ActorName} ist dem Haushalt beigetreten",
            "grantchanged" => $"{e.ActorName} hat deine Freigaben geändert",
            _ => $"{e.ActorName}: {what}".Trim(),
        };
    }

    /// <summary>Die kurze Meldung, wenn "Bezahlt" abgelehnt wurde (Spec 84, R5).</summary>
    public static string RefusedMessage(string? errorCode) => errorCode switch
    {
        ErrorCodes.NoEditGranted or ErrorCodes.NoCreateGranted or ErrorCodes.NoDeleteGranted or ErrorCodes.OwnerNotFound =>
            "Du darfst diesen Posten nicht abhaken. Öffne die App, um mehr zu sehen.",
        ClientErrorCodes.NotFound => "Der Posten ist schon erledigt. Öffne die App, um mehr zu sehen.",
        _ => "Das Abhaken hat nicht geklappt. Öffne die App und versuche es dort.",
    };
}
