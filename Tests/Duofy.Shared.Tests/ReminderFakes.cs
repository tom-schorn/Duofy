using Duofy.ApiClient;
using Duofy.Schemas;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spielt die Erinnerungen des Servers (Spec 84). Ohne Vorgabe verhält sich der Server wie ein selbst betriebener:
/// Alle Wege antworten 404, und die Oberfläche zeigt nichts davon.
/// </summary>
public sealed class FakeReminderApi : IReminderApi
{
    /// <summary>Ob der gespielte Server Erinnerungen hat; sonst antwortet jeder Weg mit 404.</summary>
    public bool Available { get; set; }

    public ReminderSettingsResponse Settings { get; set; } = new(true, true, true, true, "09:00", true, "19:00", 2);

    /// <summary>Wenn gesetzt, scheitert das Ändern der Einstellung mit diesem Fehlercode.</summary>
    public string? UpdateFailure { get; set; }

    /// <summary>Wenn gesetzt, wartet das Ändern der Einstellung auf diese Aufgabe, bevor es antwortet (für sich überlagernde Antworten).</summary>
    public Func<UpdateReminderSettingsRequest, Task>? UpdateGate { get; set; }

    public List<UpdateReminderSettingsRequest> Updates { get; } = [];
    public List<(string InstallationId, string Platform, string Token)> Registered { get; } = [];
    public List<string> Removed { get; } = [];
    public Dictionary<Guid, ReminderResponse> Reminders { get; } = [];
    public List<Guid> Postponed { get; } = [];

    private ApiResult NotFound() => ApiResult.Failure(ClientErrorCodes.NotFound);

    public Task<ApiResult> RegisterDeviceAsync(string installationId, string platform, string pushToken, CancellationToken cancellationToken = default)
    {
        if (!Available)
        {
            return Task.FromResult(NotFound());
        }
        Registered.Add((installationId, platform, pushToken));
        return Task.FromResult(ApiResult.Success());
    }

    public Task<ApiResult> RemoveDeviceAsync(string installationId, CancellationToken cancellationToken = default)
    {
        Removed.Add(installationId);
        return Task.FromResult(Available ? ApiResult.Success() : NotFound());
    }

    public Task<ApiResult<ReminderSettingsResponse>> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Available
            ? ApiResult<ReminderSettingsResponse>.Success(Settings)
            : ApiResult<ReminderSettingsResponse>.Failure(ClientErrorCodes.NotFound));

    public async Task<ApiResult<ReminderSettingsResponse>> UpdateSettingsAsync(UpdateReminderSettingsRequest request, CancellationToken cancellationToken = default)
    {
        Updates.Add(request);
        if (UpdateGate is not null)
        {
            await UpdateGate(request);
        }
        if (!Available)
        {
            return ApiResult<ReminderSettingsResponse>.Failure(ClientErrorCodes.NotFound);
        }
        if (UpdateFailure is not null)
        {
            return ApiResult<ReminderSettingsResponse>.Failure(UpdateFailure);
        }
        Settings = new ReminderSettingsResponse(
            request.DueToday ?? Settings.DueToday, request.Overdue ?? Settings.Overdue, request.AdvanceWarning ?? Settings.AdvanceWarning,
            request.Household ?? Settings.Household, request.MorningTime ?? Settings.MorningTime, request.EveningEnabled ?? Settings.EveningEnabled,
            request.EveningTime ?? Settings.EveningTime, request.WarningDays ?? Settings.WarningDays);
        return ApiResult<ReminderSettingsResponse>.Success(Settings);
    }

    public Task<ApiResult<ReminderResponse>> GetAsync(Guid reminderId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Available && Reminders.TryGetValue(reminderId, out var reminder)
            ? ApiResult<ReminderResponse>.Success(reminder)
            : ApiResult<ReminderResponse>.Failure(ClientErrorCodes.NotFound));

    public Task<ApiResult> LaterAsync(Guid reminderId, CancellationToken cancellationToken = default)
    {
        if (!Available || !Reminders.ContainsKey(reminderId))
        {
            return Task.FromResult(NotFound());
        }
        Postponed.Add(reminderId);
        return Task.FromResult(ApiResult.Success());
    }
}
