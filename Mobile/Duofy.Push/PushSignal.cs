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
            || !data.TryGetValue(TypeKey, out var type) || type is not (Due or Withdraw))
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

/// <summary>Die Texte der Benachrichtigung und ihrer Knöpfe (Spec 84, R4, R5).</summary>
public static class ReminderTexts
{
    public const string PaidAction = "Bezahlt";

    public const string LaterAction = "Später";

    /// <summary>Der Text, wenn der Server nicht erreichbar ist: Er nennt keinen Posten, ein Tipp öffnet den Monat.</summary>
    public const string FallbackTitle = "Ein Posten ist heute fällig";

    public const string Body = "Bezahlt hakt den Posten ab, Später erinnert dich morgen wieder.";

    public const string FallbackBody = "Tippe, um den Monat zu öffnen.";

    /// <summary>Der Name und die Beschreibung des Benachrichtigungskanals auf Android.</summary>
    public const string ChannelName = "Erinnerungen";

    public const string ChannelDescription = "Erinnert dich an Posten, die heute fällig sind.";

    public static string Title(string label) => $"{label} ist heute fällig";

    /// <summary>Die kurze Meldung, wenn "Bezahlt" abgelehnt wurde (Spec 84, R5).</summary>
    public static string RefusedMessage(string? errorCode) => errorCode switch
    {
        ErrorCodes.NoEditGranted or ErrorCodes.NoCreateGranted or ErrorCodes.NoDeleteGranted or ErrorCodes.OwnerNotFound =>
            "Du darfst diesen Posten nicht abhaken. Öffne die App, um mehr zu sehen.",
        ClientErrorCodes.NotFound => "Der Posten ist schon erledigt. Öffne die App, um mehr zu sehen.",
        _ => "Das Abhaken hat nicht geklappt. Öffne die App und versuche es dort.",
    };
}
