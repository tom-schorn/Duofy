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
        TimeOnly? morning = null;
        TimeOnly? evening = null;
        if (request.MorningTime is not null)
        {
            if (!TryParseTime(request.MorningTime, out var parsed))
            {
                LogRefusedTime(loggers, request.MorningTime);
                return ApiErrors.BadRequest(ReminderErrorCodes.ReminderTimeInvalid);
            }
            morning = parsed;
        }
        if (request.EveningTime is not null)
        {
            if (!TryParseTime(request.EveningTime, out var parsed))
            {
                LogRefusedTime(loggers, request.EveningTime);
                return ApiErrors.BadRequest(ReminderErrorCodes.ReminderTimeInvalid);
            }
            evening = parsed;
        }
        if (request.WarningDays is { } days && (days < ReminderSettings.MinWarningDays || days > ReminderSettings.MaxWarningDays))
        {
            return ApiErrors.BadRequest(ReminderErrorCodes.ReminderWarningDaysInvalid);
        }

        var settings = await db.ReminderSettings.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        if (settings is null)
        {
            settings = new ReminderSettings { UserId = userId };
            db.ReminderSettings.Add(settings);
        }
        settings.DueToday = request.DueToday ?? settings.DueToday;
        settings.Overdue = request.Overdue ?? settings.Overdue;
        settings.AdvanceWarning = request.AdvanceWarning ?? settings.AdvanceWarning;
        settings.Household = request.Household ?? settings.Household;
        settings.MorningTime = morning ?? settings.MorningTime;
        settings.EveningEnabled = request.EveningEnabled ?? settings.EveningEnabled;
        settings.EveningTime = evening ?? settings.EveningTime;
        settings.WarningDays = request.WarningDays ?? settings.WarningDays;
        await db.SaveChangesAsync(cancellationToken);

        loggers.CreateLogger(LogCategory).LogInformation(
            "Benachrichtigungen von {UserId}: heute fällig {DueToday}, überfällig {Overdue}, Vorwarnung {Advance} ({Days} Tage), Haushalt {Household}, morgens {Morning}, abends {Evening}.",
            userId, settings.DueToday, settings.Overdue, settings.AdvanceWarning, settings.WarningDays, settings.Household,
            settings.MorningTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            settings.EveningEnabled ? settings.EveningTime.ToString("HH:mm", CultureInfo.InvariantCulture) : "aus");
        return Results.Ok(ToResponse(settings));
    }

    /// <summary>
    /// Der Text einer Erinnerung, nur für die Person, die erinnert wird, und nur solange sie es noch ist. Den Posten
    /// liest sie am Besitzer-Filter vorbei, weil die Freigabe der Erinnerungen genau das erlaubt: den Namen dieses
    /// einen Postens, sonst nichts; den Betrag nur mit dem Recht, den Plan des Besitzers anzusehen.
    /// </summary>
    private static async Task<IResult> GetReminderAsync(Guid id, IAccess access, DuofyDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        if (access.CallerId is not { } userId)
        {
            return Results.Unauthorized();
        }
        using var bypass = db.Owners.Bypass("Erinnerung lesen");
        var own = await db.Reminders.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && r.RecipientId == userId, cancellationToken);
        if (own is { Kind: ReminderKind.Household })
        {
            var events = await HouseholdSummary.VisibleAsync(db, userId, own.CoversFrom ?? DateTimeOffset.MinValue, own.CoversUntil ?? DateTimeOffset.MaxValue, cancellationToken);
            return Results.Ok(new ReminderResponse(
                own.Id, null, userId, "", null, own.Day.Year, own.Day.Month, own.Day.Day, own.State.ToString().ToLowerInvariant(),
                ReminderKinds.Household, 0, 0, false, events));
        }
        if (await FindAsync(db, id, userId, cancellationToken) is not { } found)
        {
            return ApiErrors.NotFound(ReminderErrorCodes.ReminderNotFound);
        }
        var (reminder, plan, position) = found;
        // Die Freigabe der Erinnerungen erlaubt den Namen, nicht den Betrag: den sieht nur, wer den Plan ansehen darf.
        var mayViewPlan = (await access.LevelAsync(plan.OwnerId, Area.Plan, cancellationToken)).Includes(AccessLevel.View);
        var dueDate = ReminderRules.DueDate(plan.Year, plan.Month, position.DueDay);
        var daysUntilDue = dueDate.DayNumber - Berlin.Today(time).DayNumber;
        var postponed = await PostponeCountOfAsync(db, position.Id, dueDate, cancellationToken);
        var kind = daysUntilDue switch { > 0 => ReminderKinds.Advance, 0 => ReminderKinds.Due, _ => ReminderKinds.Overdue };
        return Results.Ok(new ReminderResponse(
            reminder.Id, position.Id, plan.OwnerId, position.Label, mayViewPlan ? position.AmountPlanned : null, plan.Year, plan.Month, position.DueDay,
            reminder.State.ToString().ToLowerInvariant(), kind, daysUntilDue, postponed, postponed < ReminderLimits.MaxPostpones));
    }

    private static async Task<int> PostponeCountOfAsync(DuofyDbContext db, Guid positionId, DateOnly dueDate, CancellationToken cancellationToken) =>
        await db.PostponeCounts.AsNoTracking().Where(c => c.PositionId == positionId && c.DueDate == dueDate).Select(c => c.Count).FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// "Später": Die Erinnerung kommt morgen am Morgen der Person wieder, nicht mehr heute. Das zählt je Posten und Fälligkeitstag;
    /// nach dem dritten Mal gibt es kein "Später" mehr (Spec 118, R6).
    /// </summary>
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
        var (reminder, plan, position) = found;
        if (reminder.State == ReminderState.Done)
        {
            return ApiErrors.NotFound(ReminderErrorCodes.ReminderNotFound);
        }
        var dueDate = ReminderRules.DueDate(plan.Year, plan.Month, position.DueDay);
        var today = Berlin.Today(time);
        var count = await db.PostponeCounts.FirstOrDefaultAsync(c => c.PositionId == position.Id && c.DueDate == dueDate, cancellationToken);
        // Eine Vorwarnung zählt nicht: Nur "heute fällig" und "überfällig" verbrauchen die drei Male (Spec 118, R6).
        var counts = dueDate <= today;
        if (counts && count is { Count: >= ReminderLimits.MaxPostpones })
        {
            return ApiErrors.BadRequest(ReminderErrorCodes.ReminderLaterLimitReached);
        }
        if (counts)
        {
            if (count is null)
            {
                count = new PostponeCount { PositionId = position.Id, DueDate = dueDate };
                db.PostponeCounts.Add(count);
            }
            count.Count++;
        }
        reminder.State = ReminderState.Later;
        reminder.Day = today.AddDays(1);
        await db.SaveChangesAsync(cancellationToken);

        loggers.CreateLogger(LogCategory).LogInformation(
            "Erinnerung {ReminderId} von {UserId} auf {Day} verschoben ({Count} von {Max} Malen verbraucht).", id, userId, reminder.Day, count?.Count ?? 0, ReminderLimits.MaxPostpones);
        return Results.NoContent();
    }

    /// <summary>Die Erinnerung, ihr Plan und ihr Posten, wenn sie dieser Person gehört und sie noch erinnert werden darf.</summary>
    private static async Task<(Reminder Reminder, Plan Plan, PlanPosition Position)?> FindAsync(
        DuofyDbContext db, Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var reminder = await db.Reminders.FirstOrDefaultAsync(r => r.Id == id && r.RecipientId == userId && r.PositionId != null, cancellationToken);
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

    // Der Zeitwähler von Android und Windows liefert "HH:mm", manchmal "HH:mm:ss" oder ohne führende Null; die Sekunden zählen nicht.
    private static readonly string[] TimeFormats = ["HH:mm", "H:mm", "HH:mm:ss", "H:mm:ss", "HH:mm:ss.FFFFFFF", "H:mm:ss.FFFFFFF"];

    /// <summary>Wie viele Zeichen eines abgelehnten Werts ins Log kommen (Eingabe von außen: kurz halten, keine Zeilenumbrüche).</summary>
    private const int LoggedTimeLength = 20;

    /// <summary>Vermerkt den abgelehnten Wert gekürzt als Warnung, sonst nichts über die Anfrage.</summary>
    private static void LogRefusedTime(ILoggerFactory loggers, string? raw)
    {
        var shortened = new string((raw ?? "").Take(LoggedTimeLength).Select(c => char.IsControl(c) ? '?' : c).ToArray());
        loggers.CreateLogger(LogCategory).LogWarning("Uhrzeit für die Benachrichtigungen abgelehnt: \"{Value}\"", shortened);
    }

    internal static bool TryParseTime(string? text, out TimeOnly time)
    {
        if (TimeOnly.TryParseExact(text?.Trim(), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            time = new TimeOnly(parsed.Hour, parsed.Minute);
            return true;
        }
        time = default;
        return false;
    }

    private static ReminderSettingsResponse ToResponse(ReminderSettings settings) =>
        new(settings.DueToday, settings.Overdue, settings.AdvanceWarning, settings.Household,
            settings.MorningTime.ToString("HH:mm", CultureInfo.InvariantCulture), settings.EveningEnabled,
            settings.EveningTime.ToString("HH:mm", CultureInfo.InvariantCulture), settings.WarningDays);

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
