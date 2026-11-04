using Duofy.ApiClient;
using Duofy.Push;
using Duofy.Schemas;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duofy.Push.Tests;

internal sealed class FakeReminderApi : IReminderApi
{
    public Dictionary<Guid, ReminderResponse> Reminders { get; } = [];

    /// <summary>Wenn gesetzt, scheitert das Holen eines Textes mit diesem Code (zum Beispiel ServerUnreachable).</summary>
    public string? GetFailure { get; set; }

    public string? RegisterFailure { get; set; }

    public List<(string InstallationId, string Platform, string Token)> Registered { get; } = [];
    public List<string> Removed { get; } = [];
    public List<Guid> Postponed { get; } = [];

    public Task<ApiResult> RegisterDeviceAsync(string installationId, string platform, string pushToken, CancellationToken cancellationToken = default)
    {
        if (RegisterFailure is not null)
        {
            return Task.FromResult(ApiResult.Failure(RegisterFailure));
        }
        Registered.Add((installationId, platform, pushToken));
        return Task.FromResult(ApiResult.Success());
    }

    public Task<ApiResult> RemoveDeviceAsync(string installationId, CancellationToken cancellationToken = default)
    {
        Removed.Add(installationId);
        return Task.FromResult(ApiResult.Success());
    }

    public Task<ApiResult<ReminderSettingsResponse>> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult<ReminderSettingsResponse>.Success(new ReminderSettingsResponse(true, "09:00")));

    public Task<ApiResult<ReminderSettingsResponse>> UpdateSettingsAsync(UpdateReminderSettingsRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult<ReminderSettingsResponse>.Success(new ReminderSettingsResponse(true, "09:00")));

    public Task<ApiResult<ReminderResponse>> GetAsync(Guid reminderId, CancellationToken cancellationToken = default)
    {
        if (GetFailure is not null)
        {
            return Task.FromResult(ApiResult<ReminderResponse>.Failure(GetFailure));
        }
        return Task.FromResult(Reminders.TryGetValue(reminderId, out var reminder)
            ? ApiResult<ReminderResponse>.Success(reminder)
            : ApiResult<ReminderResponse>.Failure(ClientErrorCodes.NotFound));
    }

    public Task<ApiResult> LaterAsync(Guid reminderId, CancellationToken cancellationToken = default)
    {
        if (!Reminders.ContainsKey(reminderId))
        {
            return Task.FromResult(ApiResult.Failure(ClientErrorCodes.NotFound));
        }
        Postponed.Add(reminderId);
        return Task.FromResult(ApiResult.Success());
    }
}

internal sealed class FakeNotifier : IReminderNotifier
{
    public List<ReminderNotification> Shown { get; } = [];
    public List<(Guid Id, string Message, string Path)> Messages { get; } = [];
    public List<Guid> Cancelled { get; } = [];

    public Task ShowAsync(ReminderNotification notification, CancellationToken cancellationToken = default)
    {
        Shown.Add(notification);
        return Task.CompletedTask;
    }

    public Task ReplaceWithMessageAsync(Guid reminderId, string message, string path, CancellationToken cancellationToken = default)
    {
        Messages.Add((reminderId, message, path));
        return Task.CompletedTask;
    }

    public Task CancelAsync(Guid reminderId, CancellationToken cancellationToken = default)
    {
        Cancelled.Add(reminderId);
        return Task.CompletedTask;
    }
}

internal sealed class FakeTicker : IPositionTicker
{
    /// <summary>Wenn gesetzt, wird das Abhaken mit diesem Code abgelehnt.</summary>
    public string? Refusal { get; set; }

    public List<(int Year, int Month, Guid PositionId, Guid OwnerId, decimal Amount)> Ticked { get; } = [];

    public Task<ApiResult> TickAsync(int year, int month, Guid positionId, Guid ownerId, decimal amount, CancellationToken cancellationToken = default)
    {
        if (Refusal is not null)
        {
            return Task.FromResult(ApiResult.Failure(Refusal));
        }
        Ticked.Add((year, month, positionId, ownerId, amount));
        return Task.FromResult(ApiResult.Success());
    }
}

internal sealed class FakeTokenSource(string? token, string platform = DevicePlatforms.Android) : IPushTokenSource
{
    public string Platform => platform;

    public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult(token);
}

internal sealed class FixedInstallation(string id = "install-1") : IInstallationIdStore
{
    public string GetOrCreate() => id;
}

internal static class Samples
{
    public static ReminderResponse Reminder(Guid? id = null, string label = "GEZ", decimal amount = 55.08m) =>
        new(id ?? Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), label, amount, 2026, 11, 4, "sent");

    public static ReminderSignalHandler Signals(FakeReminderApi api, FakeNotifier notifier) =>
        new(api, notifier, NullLogger<ReminderSignalHandler>.Instance);

    public static ReminderActionHandler Actions(FakeReminderApi api, FakeTicker ticker, FakeNotifier notifier) =>
        new(api, ticker, notifier, NullLogger<ReminderActionHandler>.Instance);
}
