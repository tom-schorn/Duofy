using Duofy.Data;
using Duofy.Domain;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api;

/// <summary>
/// Ein Durchlauf des Erinnerungs-Jobs (Spec 84): wählt aus, welche Posten heute fällig sind und wen sie angehen,
/// verschickt die stillen Signale und zieht die Erinnerungen an abgehakte Posten zurück. Der Job ruft ihn jede Minute
/// auf (<see cref="ReminderJob"/>); die Tests rufen ihn mit einem festen Zeitpunkt auf.
///
/// Gerechnet wird in Berlin. Eine Erinnerung wird zuerst gespeichert und dann verschickt: Stürzt der Server dazwischen
/// ab, kommt sie lieber nicht, als dass sie doppelt kommt. Hat die Person ihre Uhrzeit verpasst (der Server stand),
/// kommt sie noch am selben Tag.
/// </summary>
internal sealed class ReminderRunner(DuofyDbContext db, ReminderSwitch reminders, PushSender push, ILoggerFactory loggers)
{
    public const string LogCategory = "Duofy.Api.Reminders";

    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private readonly ILogger logger = loggers.CreateLogger(LogCategory);

    /// <summary>Führt einen Durchlauf aus; gibt zurück, wie viele Signale (fällig oder zurückgezogen) verschickt wurden.</summary>
    public async Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!reminders.Enabled)
        {
            return 0;
        }
        var local = TimeZoneInfo.ConvertTime(now, Zone);
        var today = DateOnly.FromDateTime(local.DateTime);
        var time = TimeOnly.FromDateTime(local.DateTime);

        // Der Job läuft ohne angemeldete Person und sieht darum die Daten aller.
        using var bypass = db.Owners.Bypass("Erinnerungen: Job ohne angemeldete Person");
        var sent = await WithdrawAsync(cancellationToken);
        sent += await SendDueAsync(today, time, cancellationToken);
        sent += await SendPostponedAsync(today, time, cancellationToken);
        return sent;
    }

    /// <summary>Zieht die Erinnerungen an abgehakte Posten zurück: ein Signal an alle Geräte der Person, dann ist sie erledigt.</summary>
    private async Task<int> WithdrawAsync(CancellationToken cancellationToken)
    {
        var ticked = await db.Reminders
            .Where(r => r.State != ReminderState.Done)
            .Where(r => db.PlanPositions.Any(p => p.Id == r.PositionId && p.DoneAt != null))
            .ToListAsync(cancellationToken);
        var sent = 0;
        foreach (var reminder in ticked)
        {
            reminder.State = ReminderState.Done;
            await db.SaveChangesAsync(cancellationToken);
            sent += await SendToAsync(reminder, PushPayload.Withdraw, cancellationToken);
        }
        return sent;
    }

    /// <summary>Die Posten, die heute fällig sind und noch keine Erinnerung an die Person haben.</summary>
    private async Task<int> SendDueAsync(DateOnly today, TimeOnly time, CancellationToken cancellationToken)
    {
        var due = await db.Plans
            .Where(plan => plan.Year == today.Year && plan.Month == today.Month)
            .SelectMany(plan => plan.Positions
                .Where(position => position.DueDay == today.Day && position.DoneAt == null)
                .Select(position => new { position.Id, position.Type, position.PaymentMethod, plan.OwnerId }))
            .ToListAsync(cancellationToken);
        due = due.Where(p => ReminderRules.IsPayable(p.Type) && ReminderRules.NeedsManualPayment(p.PaymentMethod)).ToList();
        if (due.Count == 0)
        {
            return 0;
        }

        var sent = 0;
        foreach (var owner in due.GroupBy(p => p.OwnerId))
        {
            foreach (var recipientId in await ReminderRecipients.OfOwnerAsync(db, owner.Key, cancellationToken))
            {
                if (!await IsTimeAsync(recipientId, time, cancellationToken))
                {
                    continue;
                }
                var devices = await DevicesOfAsync(recipientId, cancellationToken);
                if (devices.Count == 0)
                {
                    continue;
                }
                foreach (var position in owner)
                {
                    if (await db.Reminders.AnyAsync(r => r.PositionId == position.Id && r.RecipientId == recipientId, cancellationToken))
                    {
                        continue;
                    }
                    var reminder = new Reminder
                    {
                        Id = Guid.NewGuid(),
                        PositionId = position.Id,
                        RecipientId = recipientId,
                        Day = today,
                        State = ReminderState.Sent,
                    };
                    db.Reminders.Add(reminder);
                    await db.SaveChangesAsync(cancellationToken);
                    sent += await push.SendAsync(devices, new PushPayload(reminder.Id, PushPayload.Due), cancellationToken);
                }
            }
        }
        return sent;
    }

    /// <summary>Die Erinnerungen, die mit "Später" auf heute oder früher verschoben wurden und deren Uhrzeit da ist.</summary>
    private async Task<int> SendPostponedAsync(DateOnly today, TimeOnly time, CancellationToken cancellationToken)
    {
        var later = await db.Reminders
            .Where(r => r.State == ReminderState.Later && r.Day <= today)
            .ToListAsync(cancellationToken);
        var sent = 0;
        foreach (var reminder in later)
        {
            if (!await IsTimeAsync(reminder.RecipientId, time, cancellationToken))
            {
                continue;
            }
            var ownerId = await db.Plans
                .Where(plan => plan.Positions.Any(p => p.Id == reminder.PositionId))
                .Select(plan => plan.OwnerId)
                .SingleAsync(cancellationToken);
            if (!(await ReminderRecipients.OfOwnerAsync(db, ownerId, cancellationToken)).Contains(reminder.RecipientId))
            {
                // Die Freigabe ist weg oder die Person nicht mehr im Haushalt: Die Erinnerung endet hier.
                reminder.State = ReminderState.Done;
                await db.SaveChangesAsync(cancellationToken);
                continue;
            }
            reminder.State = ReminderState.Sent;
            reminder.Day = today;
            await db.SaveChangesAsync(cancellationToken);
            sent += await SendToAsync(reminder, PushPayload.Due, cancellationToken);
        }
        return sent;
    }

    /// <summary>True, wenn die Person Erinnerungen an hat und ihre Uhrzeit erreicht ist.</summary>
    private async Task<bool> IsTimeAsync(Guid userId, TimeOnly time, CancellationToken cancellationToken)
    {
        var settings = await db.ReminderSettings.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        return (settings?.Enabled ?? true) && (settings?.Time ?? ReminderSettings.DefaultTime) <= time;
    }

    private Task<List<ReminderDevice>> DevicesOfAsync(Guid userId, CancellationToken cancellationToken) =>
        db.ReminderDevices.AsNoTracking().Where(d => d.UserId == userId).ToListAsync(cancellationToken);

    private async Task<int> SendToAsync(Reminder reminder, string type, CancellationToken cancellationToken)
    {
        var devices = await DevicesOfAsync(reminder.RecipientId, cancellationToken);
        var delivered = await push.SendAsync(devices, new PushPayload(reminder.Id, type), cancellationToken);
        logger.LogInformation("Signal {Type} für Erinnerung {ReminderId} an {Delivered} von {Devices} Geräten.", type, reminder.Id, delivered, devices.Count);
        return delivered;
    }
}

/// <summary>Ruft den Erinnerungs-Job jede volle Minute auf, solange Erinnerungen eingeschaltet sind (Spec 84).</summary>
internal sealed class ReminderJob(IServiceScopeFactory scopes, ReminderSwitch reminders, TimeProvider time, ILoggerFactory loggers) : BackgroundService
{
    private readonly ILogger logger = loggers.CreateLogger(ReminderRunner.LogCategory);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!reminders.Enabled)
        {
            logger.LogInformation("Erinnerungen sind aus (nur gehostet und mit Duofy:Reminders:Enabled).");
            return;
        }
        logger.LogInformation("Erinnerungen laufen: jede Minute eine Prüfung.");
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = time.GetUtcNow();
            await Task.Delay(TimeSpan.FromSeconds(60 - now.Second), time, stoppingToken);
            if (!await RunOnceAsync(stoppingToken))
            {
                return;
            }
        }
    }

    /// <summary>
    /// Ein Durchlauf des Jobs. Jeder Fehler außer dem Abbruch beim Herunterfahren landet mit Kontext im Log und hält den
    /// nächsten Durchlauf nicht auf (wie beim <see cref="MailDispatcher"/>). Gibt false zurück, wenn der Job enden soll.
    /// </summary>
    internal async Task<bool> RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ReminderRunner>().RunAsync(time.GetUtcNow(), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Durchlauf der Erinnerungen gescheitert ({ErrorType}); der nächste Durchlauf läuft trotzdem.", exception.GetType().Name);
        }
        return true;
    }
}

/// <summary>Wen die Posten eines Besitzers angehen (Spec 84, R6).</summary>
internal static class ReminderRecipients
{
    /// <summary>
    /// Den Besitzer selbst und jedes Mitglied seines Haushalts, dem er den Bereich Erinnerungen freigegeben hat. Eine
    /// Freigabe zählt nur, solange beide im selben Haushalt sind (wie bei <see cref="OwnerContext"/>).
    /// </summary>
    public static async Task<List<Guid>> OfOwnerAsync(DuofyDbContext db, Guid ownerId, CancellationToken cancellationToken)
    {
        var members = await db.Grants
            .Where(g => g.GranterId == ownerId && g.Area == Area.Reminders && g.Level >= AccessLevel.View)
            .Where(g => db.HouseholdMembers.Any(owner => owner.UserId == ownerId
                && db.HouseholdMembers.Any(member => member.UserId == g.GranteeId && member.HouseholdId == owner.HouseholdId)))
            .Select(g => g.GranteeId)
            .ToListAsync(cancellationToken);
        return members.Prepend(ownerId).ToList();
    }
}
