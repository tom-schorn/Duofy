namespace Duofy.Schemas;

/// <summary>Die Systeme, auf denen Geräte Erinnerungen bekommen (Spec 84), so wie die API sie schickt.</summary>
public static class DevicePlatforms
{
    public const string Android = "android";
    public const string Windows = "windows";

    public static readonly IReadOnlyList<string> All = [Android, Windows];
}

/// <summary>Die Fehlercodes der Erinnerungen (Spec 84).</summary>
public static class ReminderErrorCodes
{
    public const string DevicePlatformInvalid = "device_platform_invalid";
    public const string DeviceTokenInvalid = "device_token_invalid";
    public const string DeviceInstallationInvalid = "device_installation_invalid";
    public const string TooManyDevices = "too_many_devices";
    public const string ReminderTimeInvalid = "reminder_time_invalid";
    public const string ReminderNotFound = "reminder_not_found";
    public const string ReminderWarningDaysInvalid = "reminder_warning_days_invalid";
    public const string ReminderLaterLimitReached = "reminder_later_limit_reached";
}

/// <summary>Die Grenzen der Erinnerungen.</summary>
public static class ReminderLimits
{
    /// <summary>So viele Geräte hat eine Person höchstens (Schutz vor einer Flut von Anmeldungen).</summary>
    public const int MaxDevices = 10;

    public const int InstallationIdMaxLength = 100;

    public const int PushTokenMaxLength = 4096;

    /// <summary>So oft darf eine Erinnerung mit "Später" verschoben werden, je Posten und Fälligkeitstag (Spec 118, R6).</summary>
    public const int MaxPostpones = 3;
}

/// <summary>Meldet ein Gerät an (PUT /api/v1/me/devices/{installationId}); die App wiederholt es bei jedem Start.</summary>
public sealed record RegisterDeviceRequest(string Platform, string PushToken);

/// <summary>
/// Die Benachrichtigungen der Person (Spec 118): vier Schalter, Uhrzeiten "HH:mm" in Berlin am Morgen und am Abend, der Abend
/// ein- oder ausgeschaltet und die Vorwarnung in Tagen (1 bis 7).
/// </summary>
public sealed record ReminderSettingsResponse(
    bool DueToday, bool Overdue, bool AdvanceWarning, bool Household, string MorningTime, bool EveningEnabled, string EveningTime, int WarningDays);

/// <summary>
/// Ändert die Einstellungen (PUT /api/v1/me/reminder-settings); nur die geschickten Felder ändern sich. Eine Zeit darf
/// "HH:mm" oder "HH:mm:ss" sein, die Sekunden zählen nicht.
/// </summary>
public sealed record UpdateReminderSettingsRequest(
    bool? DueToday = null,
    bool? Overdue = null,
    bool? AdvanceWarning = null,
    bool? Household = null,
    string? MorningTime = null,
    bool? EveningEnabled = null,
    string? EveningTime = null,
    int? WarningDays = null);

/// <summary>Die Arten, in denen eine Erinnerung beim Abrufen erscheint (ReminderResponse.Kind), so wie die API sie schickt.</summary>
public static class ReminderKinds
{
    /// <summary>Der Posten ist heute fällig (am Morgen oder am Abend).</summary>
    public const string Due = "due";

    /// <summary>Der Posten wird in <see cref="ReminderResponse.DaysUntilDue"/> Tagen fällig.</summary>
    public const string Advance = "advance";

    /// <summary>Der Fälligkeitstag ist vorbei.</summary>
    public const string Overdue = "overdue";

    /// <summary>Die Abendzusammenfassung des Haushalts; die Ereignisse stehen in <see cref="ReminderResponse.Events"/>.</summary>
    public const string Household = "household";
}

/// <summary>Ein Ereignis der Haushaltszusammenfassung; die App baut daraus ihre Zeile ("Jasmin hat Miete 850 € abgehakt").</summary>
public sealed record HouseholdEventLine(string Kind, string ActorName, string? Label, decimal? Amount);

/// <summary>
/// Der Text einer Erinnerung (GET /api/v1/reminders/{id}), nur für die Person, die erinnert wird. Mit Besitzer,
/// Jahr, Monat und Posten findet die App den Haken-Weg des Plans. Der geplante Betrag steht nur da, wenn die Person den
/// Plan des Besitzers ansehen darf; sonst ist er null. Kind sagt, was der Text meldet; DaysUntilDue ist negativ, wenn der
/// Posten überfällig ist. PostponeCount zählt die Verschiebungen; ohne LaterAllowed bietet die Benachrichtigung kein
/// "Später". Bei der Haushaltszusammenfassung gibt es keinen Posten (PositionId leer), dafür Events.
/// </summary>
public sealed record ReminderResponse(
    Guid Id,
    Guid? PositionId,
    Guid OwnerId,
    string Label,
    decimal? AmountPlanned,
    int Year,
    int Month,
    int DueDay,
    string State,
    string Kind = ReminderKinds.Due,
    int DaysUntilDue = 0,
    int PostponeCount = 0,
    bool LaterAllowed = true,
    IReadOnlyList<HouseholdEventLine>? Events = null);
