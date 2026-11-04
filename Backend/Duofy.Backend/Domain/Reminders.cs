namespace Duofy.Domain;

/// <summary>Auf welchem System ein Gerät Erinnerungen bekommt (Spec 84).</summary>
public enum DevicePlatform
{
    /// <summary>Android über Firebase Cloud Messaging (FCM).</summary>
    Android,

    /// <summary>Windows über die Windows Push Notification Services (WNS).</summary>
    Windows,
}

/// <summary>
/// Ein Gerät, das Erinnerungen bekommt (Spec 84): die App meldet nach der Anmeldung ihr Push-Token an. Das Token
/// ist ein Geheimnis zwischen Gerät, Anbieter und Server und steht nie in einer Antwort oder im Log.
/// </summary>
public class ReminderDevice
{
    /// <summary>Die Person, der das Gerät gehört; mit ihr verschwindet die Zeile.</summary>
    public Guid UserId { get; set; }

    /// <summary>Von der App vergebene Kennung dieser Installation; je Person einmal.</summary>
    public required string InstallationId { get; set; }

    public DevicePlatform Platform { get; set; }

    /// <summary>Das Token bei FCM oder der Kanal bei WNS.</summary>
    public required string PushToken { get; set; }

    /// <summary>Wann die App sich zuletzt gemeldet hat.</summary>
    public DateTimeOffset LastSeenAt { get; set; }
}

/// <summary>
/// Die Einstellung einer Person (Spec 84): Erinnerungen an oder aus und die Uhrzeit (Europe/Berlin). Gibt es keine
/// Zeile, gilt <see cref="DefaultTime"/> mit Erinnerungen an.
/// </summary>
public class ReminderSettings
{
    /// <summary>9 Uhr, solange die Person nichts anderes wählt.</summary>
    public static readonly TimeOnly DefaultTime = new(9, 0);

    public Guid UserId { get; set; }

    public bool Enabled { get; set; } = true;

    /// <summary>Die Uhrzeit in Berlin, auf die Minute genau.</summary>
    public TimeOnly Time { get; set; } = DefaultTime;
}

/// <summary>Wo eine Erinnerung gerade steht.</summary>
public enum ReminderState
{
    /// <summary>Das Signal ist an die Geräte gegangen.</summary>
    Sent,

    /// <summary>Die Person hat "Später" gewählt; die Erinnerung kommt an <see cref="Reminder.Day"/> wieder.</summary>
    Later,

    /// <summary>Der Posten ist abgehakt; die Erinnerung ist erledigt und zurückgezogen.</summary>
    Done,
}

/// <summary>
/// Eine Erinnerung an einen Posten für eine Person (Spec 84). Die Kennung ist zufällig und steht allein im
/// Signal an den Anbieter; Namen und Betrag holt die App danach mit Anmeldung vom Server.
/// </summary>
public class Reminder
{
    public Guid Id { get; set; }

    public Guid PositionId { get; set; }

    /// <summary>Die Person, die erinnert wird.</summary>
    public Guid RecipientId { get; set; }

    /// <summary>Der Tag (Berlin), an dem die Erinnerung kommt; nach "Später" der nächste.</summary>
    public DateOnly Day { get; set; }

    public ReminderState State { get; set; }
}

/// <summary>Die Regeln, welche Posten erinnert werden.</summary>
public static class ReminderRules
{
    /// <summary>Lastschrift und Dauerauftrag laufen von allein; nur was die Person selbst anstößt, wird erinnert.</summary>
    public static bool NeedsManualPayment(PaymentMethod method) =>
        method is PaymentMethod.Transfer or PaymentMethod.Withdrawal or PaymentMethod.Other;

    /// <summary>Einnahmen werden nicht bezahlt, also auch nicht erinnert.</summary>
    public static bool IsPayable(CommitmentType type) => type != CommitmentType.Income;
}
