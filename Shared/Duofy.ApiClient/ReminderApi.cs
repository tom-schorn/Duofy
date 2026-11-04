using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>Der Client für die Erinnerungen, siehe <see cref="IReminderApi"/>.</summary>
internal sealed class ReminderApi(ApiConnection api) : IReminderApi
{
    private static string Device(string installationId) => $"api/v1/me/devices/{Uri.EscapeDataString(installationId)}";

    public Task<ApiResult> RegisterDeviceAsync(string installationId, string platform, string pushToken, CancellationToken cancellationToken = default) =>
        api.SendAsync(HttpMethod.Put, Device(installationId), new RegisterDeviceRequest(platform, pushToken), anonymous: false, cancellationToken);

    public Task<ApiResult> RemoveDeviceAsync(string installationId, CancellationToken cancellationToken = default) =>
        api.SendAsync(HttpMethod.Delete, Device(installationId), body: null, anonymous: false, cancellationToken);

    public Task<ApiResult<ReminderSettingsResponse>> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        api.SendAsync<ReminderSettingsResponse>(HttpMethod.Get, "api/v1/me/reminder-settings", body: null, anonymous: false, cancellationToken);

    public Task<ApiResult<ReminderSettingsResponse>> UpdateSettingsAsync(UpdateReminderSettingsRequest request, CancellationToken cancellationToken = default) =>
        api.SendAsync<ReminderSettingsResponse>(HttpMethod.Put, "api/v1/me/reminder-settings", request, anonymous: false, cancellationToken);

    public Task<ApiResult<ReminderResponse>> GetAsync(Guid reminderId, CancellationToken cancellationToken = default) =>
        api.SendAsync<ReminderResponse>(HttpMethod.Get, $"api/v1/reminders/{reminderId}", body: null, anonymous: false, cancellationToken);

    public Task<ApiResult> LaterAsync(Guid reminderId, CancellationToken cancellationToken = default) =>
        api.SendAsync(HttpMethod.Post, $"api/v1/reminders/{reminderId}/later", body: null, anonymous: false, cancellationToken);
}
