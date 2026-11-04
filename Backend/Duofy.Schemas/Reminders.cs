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
}

/// <summary>Die Grenzen der Erinnerungen.</summary>
public static class ReminderLimits
{
    /// <summary>So viele Geräte hat eine Person höchstens (Schutz vor einer Flut von Anmeldungen).</summary>
    public const int MaxDevices = 10;

    public const int InstallationIdMaxLength = 100;

    public const int PushTokenMaxLength = 4096;
}

/// <summary>Meldet ein Gerät an (PUT /api/v1/me/devices/{installationId}); die App wiederholt es bei jedem Start.</summary>
public sealed record RegisterDeviceRequest(string Platform, string PushToken);

/// <summary>Die Einstellung "Erinnerungen" der Person: an oder aus und die Uhrzeit "HH:mm" in Berlin.</summary>
public sealed record ReminderSettingsResponse(bool Enabled, string Time);

/// <summary>Ändert die Einstellung (PUT /api/v1/me/reminder-settings); nur die geschickten Felder ändern sich.</summary>
public sealed record UpdateReminderSettingsRequest(bool? Enabled = null, string? Time = null);

/// <summary>
/// Der Text einer Erinnerung (GET /api/v1/reminders/{id}), nur für die Person, die erinnert wird. Mit Besitzer,
/// Jahr, Monat und Posten findet die App den Haken-Weg des Plans. Der geplante Betrag steht nur da, wenn die Person den
/// Plan des Besitzers ansehen darf; sonst ist er null.
/// </summary>
public sealed record ReminderResponse(
    Guid Id,
    Guid PositionId,
    Guid OwnerId,
    string Label,
    decimal? AmountPlanned,
    int Year,
    int Month,
    int DueDay,
    string State);
