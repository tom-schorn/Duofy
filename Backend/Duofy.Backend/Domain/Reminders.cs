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
/// Die Einstellungen einer Person für Benachrichtigungen (Spec 118, löst Spec 84 ab): vier Schalter, eine Uhrzeit am
/// Morgen, ein Abend (ein- und ausschaltbar) und die Vorwarnung in Tagen. Gibt es keine Zeile, gelten die Vorgaben:
/// alles an, morgens 09:00, abends 19:00, zwei Tage.
/// </summary>
public class ReminderSettings
{
    public static readonly TimeOnly DefaultMorning = new(9, 0);
    public static readonly TimeOnly DefaultEvening = new(19, 0);

    public const int DefaultWarningDays = 2;
    public const int MinWarningDays = 1;
    public const int MaxWarningDays = 7;

    public Guid UserId { get; set; }

    /// <summary>"Heute fällig": am Morgen für jeden Posten von heute, am Abend noch einmal für die offenen.</summary>
    public bool DueToday { get; set; } = true;

    /// <summary>"Überfällig": jeden Morgen für jeden Posten, dessen Tag vorbei ist und der nicht abgehakt ist.</summary>
    public bool Overdue { get; set; } = true;

    /// <summary>"Vorwarnung": am Morgen, <see cref="WarningDays"/> Tage vor dem Fälligkeitstag.</summary>
    public bool AdvanceWarning { get; set; } = true;

    /// <summary>"Haushalt": am Abend eine Zusammenfassung dessen, was im Haushalt geschah.</summary>
    public bool Household { get; set; } = true;

    /// <summary>Die Uhrzeit am Morgen in Berlin, auf die Minute genau.</summary>
    public TimeOnly MorningTime { get; set; } = DefaultMorning;

    public bool EveningEnabled { get; set; } = true;

    /// <summary>Die Uhrzeit am Abend in Berlin, auf die Minute genau.</summary>
    public TimeOnly EveningTime { get; set; } = DefaultEvening;

    /// <summary>Wie viele Tage vor dem Fälligkeitstag die Vorwarnung kommt (1 bis 7).</summary>
    public int WarningDays { get; set; } = DefaultWarningDays;
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

/// <summary>Wozu eine Erinnerung kommt (Spec 118).</summary>
public enum ReminderKind
{
    /// <summary>Am Morgen: Der Posten ist heute fällig.</summary>
    Due,

    /// <summary>Am Abend: Der Posten von heute ist noch nicht abgehakt.</summary>
    Evening,

    /// <summary>Am Morgen, einige Tage vor dem Fälligkeitstag.</summary>
    Advance,

    /// <summary>Am Morgen: Der Fälligkeitstag ist vorbei, der Posten nicht abgehakt.</summary>
    Overdue,

    /// <summary>Am Abend: Was im Haushalt geschah. Gehört zu keinem Posten.</summary>
    Household,
}

/// <summary>
/// Eine Erinnerung an einen Posten für eine Person (Spec 84, 118). Die Kennung ist zufällig und steht allein im
/// Signal an den Anbieter; Namen und Betrag holt die App danach mit Anmeldung vom Server. Je Posten, Person, Tag und Art
/// gibt es höchstens eine. Die Zusammenfassung des Haushalts hat keinen Posten, dafür den Zeitraum, den sie abdeckt.
/// </summary>
public class Reminder
{
    public Guid Id { get; set; }

    /// <summary>Der Posten; bei der Zusammenfassung des Haushalts leer.</summary>
    public Guid? PositionId { get; set; }

    /// <summary>Die Person, die erinnert wird.</summary>
    public Guid RecipientId { get; set; }

    /// <summary>Der Tag (Berlin), an dem die Erinnerung kommt; nach "Später" der nächste.</summary>
    public DateOnly Day { get; set; }

    public ReminderState State { get; set; }

    public ReminderKind Kind { get; set; }

    /// <summary>Nur bei der Zusammenfassung: Ereignisse ab diesem Zeitpunkt (ausschließlich).</summary>
    public DateTimeOffset? CoversFrom { get; set; }

    /// <summary>Nur bei der Zusammenfassung: Ereignisse bis zu diesem Zeitpunkt (einschließlich).</summary>
    public DateTimeOffset? CoversUntil { get; set; }
}

/// <summary>
/// Wie oft ein Posten zu einem Fälligkeitstag mit "Später" verschoben wurde (Spec 118, R6). Nach
/// <see cref="ReminderLimits.MaxPostpones"/> Malen gibt es kein "Später" mehr, und der Plan zeigt den Posten als überfällig.
/// </summary>
public class PostponeCount
{
    public Guid PositionId { get; set; }

    /// <summary>Der Fälligkeitstag, zu dem gezählt wird; ändert sich der Tag des Postens, beginnt die Zählung neu.</summary>
    public DateOnly DueDate { get; set; }

    public int Count { get; set; }
}

/// <summary>Was im Haushalt geschah und in der Zusammenfassung vorkommen kann (Spec 118, R8).</summary>
public enum HouseholdEventKind
{
    /// <summary>Jemand hat einen Posten abgehakt.</summary>
    Ticked,

    /// <summary>Jemand hat gebucht.</summary>
    Booked,

    /// <summary>Jemand wurde eingeladen.</summary>
    Invited,

    /// <summary>Jemand ist dem Haushalt beigetreten.</summary>
    Joined,

    /// <summary>Eine Freigabe an <see cref="HouseholdEvent.SubjectId"/> hat sich geändert.</summary>
    GrantChanged,
}

/// <summary>
/// Ein Ereignis im Haushalt, das in der Abendzusammenfassung vorkommen kann (Spec 118, R8). Die Zeile hält nur, was
/// die Zusammenfassung nennt; wer sie sehen darf, entscheiden beim Zusammenstellen die Freigaben.
/// </summary>
public class HouseholdEvent
{
    public Guid Id { get; set; }

    public Guid HouseholdId { get; set; }

    public HouseholdEventKind Kind { get; set; }

    /// <summary>Wer es getan hat; diese Person liest es nicht in ihrer eigenen Zusammenfassung.</summary>
    public Guid ActorId { get; set; }

    /// <summary>Wessen Daten es betrifft (Posten, Buchung); darüber entscheiden die Freigaben, wer es sieht.</summary>
    public Guid? OwnerId { get; set; }

    /// <summary>Wen es persönlich betrifft (eingeladen, beigetreten, Freigabe geändert).</summary>
    public Guid? SubjectId { get; set; }

    /// <summary>Name des Postens oder der Buchung.</summary>
    public string? Label { get; set; }

    public decimal? Amount { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}

/// <summary>Die Regeln, welche Posten erinnert werden.</summary>
public static class ReminderRules
{
    /// <summary>Lastschrift und Dauerauftrag laufen von allein; nur was die Person selbst anstößt, wird erinnert.</summary>
    public static bool NeedsManualPayment(PaymentMethod method) =>
        method is PaymentMethod.Transfer or PaymentMethod.Withdrawal or PaymentMethod.Other;

    /// <summary>
    /// Der Fälligkeitstag eines Postens im Plan-Monat; hat der Monat weniger Tage (31 im Februar), gilt sein letzter Tag.
    /// </summary>
    public static DateOnly DueDate(int year, int month, int dueDay) =>
        new(year, month, Math.Min(dueDay, DateTime.DaysInMonth(year, month)));

    /// <summary>Einnahmen werden nicht bezahlt, also auch nicht erinnert.</summary>
    public static bool IsPayable(CommitmentType type) => type != CommitmentType.Income;
}
