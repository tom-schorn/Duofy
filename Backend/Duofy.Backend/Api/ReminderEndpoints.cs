using System.Globalization;
using Duofy.Data;
using Duofy.Domain;
using Duofy.Schemas;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api;

/// <summary>
/// Geräte, Einstellung und Erinnerungen (Spec 84). Alle Wege gibt es nur, wenn Erinnerungen eingeschaltet sind
/// (<see cref="ReminderSwitch"/>); sonst antworten sie 404, als gäbe es sie nicht. Jeder arbeitet mit der Person aus
/// dem Token, nie mit einer Kennung aus der Anfrage. Ein Push-Token steht in keiner Antwort und in keinem Log.
/// </summary>
internal static class ReminderEndpoints
{
    private const string LogCategory = "Duofy.Api.Reminders";

    public static void MapReminderEndpoints(this IEndpointRouteBuilder v1)
    {
        var group = v1.MapGroup("").RequireAuthorization();
        group.AddEndpointFilter(async (context, next) =>
            context.HttpContext.RequestServices.GetRequiredService<ReminderSwitch>().Enabled
                ? await next(context)
                : Results.NotFound());

        group.MapPut("/me/devices/{installationId}", PutDeviceAsync);
        group.MapDelete("/me/devices/{installationId}", DeleteDeviceAsync);
        group.MapGet("/me/reminder-settings", GetSettingsAsync);
        group.MapPut("/me/reminder-settings", PutSettingsAsync);
        group.MapGet("/reminders/{id:guid}", GetReminderAsync);
        group.MapPost("/reminders/{id:guid}/later", LaterAsync);
    }

    private static async Task<IResult> PutDeviceAsync(
        string installationId,
        RegisterDeviceRequest request,
        IAccess access,
        DuofyDbContext db,
        TimeProvider time,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (access.CallerId is not { } userId)
        {
            return Results.Unauthorized();
        }
        if (installationId.Length is 0 or > ReminderLimits.InstallationIdMaxLength)
        {
            return ApiErrors.BadRequest(ReminderErrorCodes.DeviceInstallationInvalid);
        }
        if (!TryParsePlatform(request.Platform, out var platform))
        {
            return ApiErrors.BadRequest(ReminderErrorCodes.DevicePlatformInvalid);
        }
        var token = request.PushToken?.Trim();
        if (string.IsNullOrEmpty(token) || token.Length > ReminderLimits.PushTokenMaxLength)
        {
            return ApiErrors.BadRequest(ReminderErrorCodes.DeviceTokenInvalid);
        }

        // Ein Windows-Kanal ist eine Adresse, an die der Server später sendet: Nur eine von notify.windows.com zählt.
        if (platform == DevicePlatform.Windows && !WnsClient.IsWnsChannel(token))
        {
            return ApiErrors.BadRequest(ReminderErrorCodes.DeviceTokenInvalid);
        }

        // Ein Gerät hat immer nur eine Person: Dasselbe Token oder dieselbe Installation bei anderen Personen ist ein Gerät,
        // an dem jemand anderes angemeldet war; dort sollen keine Signale mehr ankommen.
        await db.ReminderDevices.Where(d => d.UserId != userId && (d.PushToken == token || d.InstallationId == installationId))
            .ExecuteDeleteAsync(cancellationToken);

        var device = await db.ReminderDevices.FirstOrDefaultAsync(d => d.UserId == userId && d.InstallationId == installationId, cancellationToken);
        if (device is null)
        {
            if (await db.ReminderDevices.CountAsync(d => d.UserId == userId, cancellationToken) >= ReminderLimits.MaxDevices)
            {
                return ApiErrors.BadRequest(ReminderErrorCodes.TooManyDevices);
            }
            device = new ReminderDevice { UserId = userId, InstallationId = installationId, PushToken = token };
            db.ReminderDevices.Add(device);
        }
        device.Platform = platform;
        device.PushToken = token;
        device.LastSeenAt = time.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        loggers.CreateLogger(LogCategory).LogInformation("Gerät {InstallationId} ({Platform}) von {UserId} angemeldet.", installationId, platform, userId);
        return Results.NoContent();
    }

    /// <summary>Meldet das Gerät ab; gibt es keins, ist das auch gut (204), damit die Abmeldung der App nie scheitert.</summary>
    private static async Task<IResult> DeleteDeviceAsync(
        string installationId,
        IAccess access,
        DuofyDbContext db,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (access.CallerId is not { } userId)
        {
            return Results.Unauthorized();
        }
        var removed = await db.ReminderDevices.Where(d => d.UserId == userId && d.InstallationId == installationId).ExecuteDeleteAsync(cancellationToken);
        loggers.CreateLogger(LogCategory).LogInformation("Gerät {InstallationId} von {UserId} abgemeldet ({Removed}).", installationId, userId, removed);
        return Results.NoContent();
    }

    private static async Task<IResult> GetSettingsAsync(IAccess access, DuofyDbContext db, CancellationToken cancellationToken)
    {
        if (access.CallerId is not { } userId)
        {
            return Results.Unauthorized();
        }
        var settings = await db.ReminderSettings.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        return Results.Ok(ToResponse(settings ?? new ReminderSettings { UserId = userId }));
    }

    private static async Task<IResult> PutSettingsAsync(
        UpdateReminderSettingsRequest request,
        IAccess access,
        DuofyDbContext db,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (access.CallerId is not { } userId)
        {
            return Results.Unauthorized();
        }
        TimeOnly? time = null;
        if (request.Time is not null)
        {
            if (!TimeOnly.TryParseExact(request.Time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return ApiErrors.BadRequest(ReminderErrorCodes.ReminderTimeInvalid);
            }
            time = parsed;
        }

        var settings = await db.ReminderSettings.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        if (settings is null)
        {
            settings = new ReminderSettings { UserId = userId };
            db.ReminderSettings.Add(settings);
        }
        settings.Enabled = request.Enabled ?? settings.Enabled;
        settings.Time = time ?? settings.Time;
        await db.SaveChangesAsync(cancellationToken);

        loggers.CreateLogger(LogCategory).LogInformation(
            "Erinnerungen von {UserId}: {State} um {Time}.", userId, settings.Enabled ? "an" : "aus", settings.Time.ToString("HH:mm", CultureInfo.InvariantCulture));
        return Results.Ok(ToResponse(settings));
    }

    /// <summary>
    /// Der Text einer Erinnerung, nur für die Person, die erinnert wird, und nur solange sie es noch ist. Den Posten
    /// liest sie am Besitzer-Filter vorbei, weil die Freigabe der Erinnerungen genau das erlaubt: den Namen dieses
    /// einen Postens, sonst nichts; den Betrag nur mit dem Recht, den Plan des Besitzers anzusehen.
    /// </summary>
    private static async Task<IResult> GetReminderAsync(Guid id, IAccess access, DuofyDbContext db, CancellationToken cancellationToken)
    {
        if (access.CallerId is not { } userId)
        {
            return Results.Unauthorized();
        }
        using var bypass = db.Owners.Bypass("Erinnerung lesen");
        if (await FindAsync(db, id, userId, cancellationToken) is not { } found)
        {
            return ApiErrors.NotFound(ReminderErrorCodes.ReminderNotFound);
        }
        var (reminder, plan, position) = found;
        // Die Freigabe der Erinnerungen erlaubt den Namen, nicht den Betrag: den sieht nur, wer den Plan ansehen darf.
        var mayViewPlan = (await access.LevelAsync(plan.OwnerId, Area.Plan, cancellationToken)).Includes(AccessLevel.View);
        return Results.Ok(new ReminderResponse(
            reminder.Id, position.Id, plan.OwnerId, position.Label, mayViewPlan ? position.AmountPlanned : null, plan.Year, plan.Month, position.DueDay,
            reminder.State.ToString().ToLowerInvariant()));
    }

    /// <summary>"Später": Die Erinnerung kommt morgen zur Uhrzeit der Person wieder, nicht mehr heute.</summary>
    private static async Task<IResult> LaterAsync(
        Guid id, IAccess access, DuofyDbContext db, TimeProvider time, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        if (access.CallerId is not { } userId)
        {
            return Results.Unauthorized();
        }
        using var bypass = db.Owners.Bypass("Erinnerung lesen");
        if (await FindAsync(db, id, userId, cancellationToken) is not { } found)
        {
            return ApiErrors.NotFound(ReminderErrorCodes.ReminderNotFound);
        }
        var reminder = found.Reminder;
        if (reminder.State == ReminderState.Done)
        {
            return ApiErrors.NotFound(ReminderErrorCodes.ReminderNotFound);
        }
        reminder.State = ReminderState.Later;
        reminder.Day = Berlin.Today(time).AddDays(1);
        await db.SaveChangesAsync(cancellationToken);

        loggers.CreateLogger(LogCategory).LogInformation("Erinnerung {ReminderId} von {UserId} auf {Day} verschoben.", id, userId, reminder.Day);
        return Results.NoContent();
    }

    /// <summary>Die Erinnerung, ihr Plan und ihr Posten, wenn sie dieser Person gehört und sie noch erinnert werden darf.</summary>
    private static async Task<(Reminder Reminder, Plan Plan, PlanPosition Position)?> FindAsync(
        DuofyDbContext db, Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var reminder = await db.Reminders.FirstOrDefaultAsync(r => r.Id == id && r.RecipientId == userId, cancellationToken);
        if (reminder is null)
        {
            return null;
        }
        var plan = await db.Plans.Include(p => p.Positions.Where(position => position.Id == reminder.PositionId))
            .FirstOrDefaultAsync(p => p.Positions.Any(position => position.Id == reminder.PositionId), cancellationToken);
        if (plan is null || plan.Positions.Count == 0)
        {
            return null;
        }
        if (!(await ReminderRecipients.OfOwnerAsync(db, plan.OwnerId, cancellationToken)).Contains(userId))
        {
            return null;
        }
        return (reminder, plan, plan.Positions[0]);
    }

    private static ReminderSettingsResponse ToResponse(ReminderSettings settings) =>
        new(settings.Enabled, settings.Time.ToString("HH:mm", CultureInfo.InvariantCulture));

    private static bool TryParsePlatform(string? key, out DevicePlatform platform)
    {
        switch (key)
        {
            case DevicePlatforms.Android:
                platform = DevicePlatform.Android;
                return true;
            case DevicePlatforms.Windows:
                platform = DevicePlatform.Windows;
                return true;
            default:
                platform = default;
                return false;
        }
    }
}
