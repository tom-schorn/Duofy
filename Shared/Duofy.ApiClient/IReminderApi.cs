using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>
/// Die Erinnerungen über die Duofy-API (Spec 84): Geräte anmelden, die Einstellung und der Text einer Erinnerung.
/// Gibt es auf dem Server keine Erinnerungen (selbst betrieben oder ausgeschaltet), antwortet jeder Weg mit
/// <see cref="ClientErrorCodes.NotFound"/>; die Oberfläche blendet den Abschnitt dann aus. Eine Erinnerung, die
/// nicht (mehr) der angemeldeten Person gehört, ergibt ebenfalls <see cref="ClientErrorCodes.NotFound"/>.
/// </summary>
public interface IReminderApi
{
    /// <summary>Meldet das Gerät mit seinem Push-Token an; wiederholbar, ein bekanntes Gerät bekommt das neue Token.</summary>
    Task<ApiResult> RegisterDeviceAsync(string installationId, string platform, string pushToken, CancellationToken cancellationToken = default);

    /// <summary>Meldet das Gerät ab; ein unbekanntes Gerät ist auch gut.</summary>
    Task<ApiResult> RemoveDeviceAsync(string installationId, CancellationToken cancellationToken = default);

    /// <summary>Die Einstellung der Person: an oder aus und die Uhrzeit in Berlin.</summary>
    Task<ApiResult<ReminderSettingsResponse>> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>Ändert die Einstellung; nur die geschickten Felder ändern sich.</summary>
    Task<ApiResult<ReminderSettingsResponse>> UpdateSettingsAsync(UpdateReminderSettingsRequest request, CancellationToken cancellationToken = default);

    /// <summary>Der Text einer Erinnerung, den die App nach dem stillen Signal holt.</summary>
    Task<ApiResult<ReminderResponse>> GetAsync(Guid reminderId, CancellationToken cancellationToken = default);

    /// <summary>"Später": Die Erinnerung kommt morgen zur Uhrzeit der Person wieder.</summary>
    Task<ApiResult> LaterAsync(Guid reminderId, CancellationToken cancellationToken = default);
}
