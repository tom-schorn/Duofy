using Duofy.Schemas;
using Duofy.Data;
using Duofy.Domain;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api;

/// <summary>
/// Ein Durchlauf des Erinnerungs-Jobs (Spec 84, 118): wählt aus, was heute zu melden ist und wen es angeht, verschickt die
/// stillen Signale und zieht die Erinnerungen an abgehakte Posten zurück. Am Morgen einer Person (ihre Uhrzeit) kommen
/// "heute fällig", "Vorwarnung" und "überfällig", am Abend (wenn an) noch einmal die offenen Posten von heute und die
/// Zusammenfassung des Haushalts. Der Job ruft ihn jede Minute auf (<see cref="ReminderJob"/>); die Tests rufen ihn mit
/// einem festen Zeitpunkt auf.
///
/// Gerechnet wird in Berlin. Eine Erinnerung wird zuerst gespeichert und dann verschickt: Stürzt der Server dazwischen
/// ab, kommt sie lieber nicht, als dass sie doppelt kommt. Hat die Person ihre Uhrzeit verpasst (der Server stand),
/// kommt sie noch am selben Tag. Je Posten, Person, Tag und Art gibt es höchstens eine Erinnerung.
/// </summary>
internal sealed class ReminderRunner(DuofyDbContext db, ReminderSwitch reminders, PushSender push, ILoggerFactory loggers)
{
    public const string LogCategory = "Duofy.Api.Reminders";

    /// <summary>So viele Monate zurück sucht "überfällig" (der laufende und die beiden davor); ältere offene Posten melden sich nicht mehr.</summary>
    private const int OverdueMonths = 3;

    /// <summary>Nach der Abendzeit holt der Job die Zusammenfassung noch so lange nach, falls der Server stand (Spec 118, R8); danach wartet sie auf morgen.</summary>
    private static readonly TimeSpan SummaryCatchUp = TimeSpan.FromHours(1);

    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private readonly ILogger logger = loggers.CreateLogger(LogCategory);

    private readonly Dictionary<Guid, ReminderSettings> settingsCache = [];

    /// <summary>Führt einen Durchlauf aus; gibt zurück, wie viele Signale (fällig oder zurückgezogen) verschickt wurden.</summary>
    public async Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!reminders.Enabled)
        {
            return 0;
        }
        now = now.ToUniversalTime();
        var local = TimeZoneInfo.ConvertTime(now, Zone);
        var today = DateOnly.FromDateTime(local.DateTime);
        var time = TimeOnly.FromDateTime(local.DateTime);
        settingsCache.Clear();

        // Der Job läuft ohne angemeldete Person und sieht darum die Daten aller.
        using var bypass = db.Owners.Bypass("Erinnerungen: Job ohne angemeldete Person");
        var sent = await WithdrawAsync(cancellationToken);
        sent += await SendPostponedAsync(today, time, cancellationToken);
        sent += await SendPositionRemindersAsync(today, time, cancellationToken);
        sent += await SendHouseholdSummariesAsync(today, time, cancellationToken);
        await HouseholdSummary.PruneAsync(db, now, cancellationToken);
        return sent;
    }

    /// <summary>Zieht die Erinnerungen an abgehakte Posten zurück: ein Signal an alle Geräte der Person, dann ist sie erledigt.</summary>
    private async Task<int> WithdrawAsync(CancellationToken cancellationToken)
    {
        var ticked = await db.Reminders
            .Where(r => r.State != ReminderState.Done && r.PositionId != null)
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

    private sealed record Candidate(Guid Id, CommitmentType Type, PaymentMethod PaymentMethod, DateOnly DueDate, Guid OwnerId, bool Open);

    /// <summary>
    /// Die offenen Posten mit Zahlung von Hand, die heute, in den nächsten Tagen oder schon länger fällig sind, je nach
    /// Einstellung der Empfänger als "heute fällig", Abend, Vorwarnung oder "überfällig".
    /// </summary>
    private async Task<int> SendPositionRemindersAsync(DateOnly today, TimeOnly time, CancellationToken cancellationToken)
    {
        var first = today.AddMonths(-(OverdueMonths - 1));
        var last = today.AddDays(ReminderSettings.MaxWarningDays);
        var rows = await db.Plans
            .Where(plan => (plan.Year > first.Year || (plan.Year == first.Year && plan.Month >= first.Month))
                && (plan.Year < last.Year || (plan.Year == last.Year && plan.Month <= last.Month)))
            .SelectMany(plan => plan.Positions
                .Where(position => position.DoneAt == null)
                .Select(position => new { position.Id, position.Type, position.PaymentMethod, position.DueDay, position.PassThrough, position.AmountPlanned, plan.Year, plan.Month, plan.OwnerId }))
            .ToListAsync(cancellationToken);
        var payable = rows.Where(p => ReminderRules.IsPayable(p.Type) && ReminderRules.NeedsManualPayment(p.PaymentMethod)).ToList();
        var ids = payable.Select(p => p.Id).ToList();
        // Gebucht je Posten wie im Plan (PlanFigures.BookedPerPosition): die Summe der Buchungszeilen auf dem Posten.
        var booked = (await db.Bookings
            .SelectMany(b => b.Lines)
            .Where(l => l.PositionId != null && ids.Contains(l.PositionId.Value))
            .Select(l => new { PositionId = l.PositionId!.Value, l.Amount })
            .ToListAsync(cancellationToken))
            .GroupBy(l => l.PositionId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Amount));
        var candidates = payable
            .Select(p => new Candidate(p.Id, p.Type, p.PaymentMethod, ReminderRules.DueDate(p.Year, p.Month, p.DueDay), p.OwnerId,
                PositionOpen.Is(income: false, p.PassThrough, done: false, p.AmountPlanned, booked.GetValueOrDefault(p.Id))))
            .Where(p => p.DueDate <= last)
            .ToList();
        if (candidates.Count == 0)
        {
            return 0;
        }

        var sent = 0;
        foreach (var owner in candidates.GroupBy(p => p.OwnerId))
        {
            foreach (var recipientId in await ReminderRecipients.OfOwnerAsync(db, owner.Key, cancellationToken))
            {
                var settings = await SettingsOfAsync(recipientId, cancellationToken);
                var morning = settings.MorningTime <= time;
                var evening = settings.EveningEnabled && settings.EveningTime <= time;
                if (!morning && !evening)
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
                    foreach (var kind in KindsOf(position.DueDate, position.Open, today, settings, morning, evening))
                    {
                        sent += await SendNewAsync(position.Id, recipientId, today, kind, devices, cancellationToken);
                    }
                }
            }
        }
        return sent;
    }

    /// <summary>Welche Erinnerungen dieser Posten für diese Person jetzt verdient (ob sie schon gesendet wurden, prüft der Aufrufer). Am Abend und für "überfällig" muss der Posten offen sein (<see cref="PositionOpen"/>, wie im Plan).</summary>
    private static IEnumerable<ReminderKind> KindsOf(DateOnly due, bool open, DateOnly today, ReminderSettings settings, bool morning, bool evening)
    {
        if (due == today)
        {
            if (settings.DueToday && morning)
            {
                yield return ReminderKind.Due;
            }
            if (settings.DueToday && evening && open)
            {
                yield return ReminderKind.Evening;
            }
        }
        else if (due > today)
        {
            if (settings.AdvanceWarning && morning && due == today.AddDays(settings.WarningDays))
            {
                yield return ReminderKind.Advance;
            }
        }
        else if (settings.Overdue && morning && open)
        {
            yield return ReminderKind.Overdue;
        }
    }

    /// <summary>
    /// Legt die Erinnerung an und verschickt sie, wenn es sie heute in dieser Art noch nicht gibt und der Posten nicht gerade
    /// mit "Später" auf morgen verschoben ist (dann kommt er morgen von allein wieder). Jede Erinnerung des Postens, die heute
    /// noch läuft, zählt als vorhanden (eine auf den Fälligkeitstag verschobene Vorwarnung ersetzt "heute fällig"); nur der
    /// Abend kommt zusätzlich zu der vom Morgen.
    /// </summary>
    private async Task<int> SendNewAsync(
        Guid positionId, Guid recipientId, DateOnly today, ReminderKind kind, List<ReminderDevice> devices, CancellationToken cancellationToken)
    {
        var already = await db.Reminders.AnyAsync(r => r.PositionId == positionId && r.RecipientId == recipientId
            && (r.State == ReminderState.Later
                || (r.Day == today && (r.Kind == kind || (kind != ReminderKind.Evening && r.State != ReminderState.Done)))), cancellationToken);
        if (already)
        {
            return 0;
        }
        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            PositionId = positionId,
            RecipientId = recipientId,
            Day = today,
            Kind = kind,
            State = ReminderState.Sent,
        };
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync(cancellationToken);
        return await push.SendAsync(devices, new PushPayload(reminder.Id, PushPayload.Due), cancellationToken);
    }

    /// <summary>Die Erinnerungen, die mit "Später" auf heute oder früher verschoben wurden und deren Morgen da ist (und deren Schalter noch an ist).</summary>
    private async Task<int> SendPostponedAsync(DateOnly today, TimeOnly time, CancellationToken cancellationToken)
    {
        var later = await db.Reminders
            .Where(r => r.State == ReminderState.Later && r.Day <= today && r.PositionId != null)
            .ToListAsync(cancellationToken);
        var sent = 0;
        foreach (var reminder in later)
        {
            if ((await SettingsOfAsync(reminder.RecipientId, cancellationToken)).MorningTime > time)
            {
                continue;
            }
            var home = await db.Plans
                .Where(plan => plan.Positions.Any(p => p.Id == reminder.PositionId))
                .Select(plan => new { plan.OwnerId, plan.Year, plan.Month, DueDay = plan.Positions.First(p => p.Id == reminder.PositionId).DueDay })
                .SingleAsync(cancellationToken);
            var ownerId = home.OwnerId;
            if (!(await ReminderRecipients.OfOwnerAsync(db, ownerId, cancellationToken)).Contains(reminder.RecipientId))
            {
                // Die Freigabe ist weg oder die Person nicht mehr im Haushalt: Die Erinnerung endet hier.
                reminder.State = ReminderState.Done;
                await db.SaveChangesAsync(cancellationToken);
                continue;
            }
            // Der Schalter der Art, in der sie jetzt käme (wie beim Abruf: nach dem Fälligkeitstag); ist er inzwischen aus, endet sie.
            var due = ReminderRules.DueDate(home.Year, home.Month, home.DueDay);
            var settings = await SettingsOfAsync(reminder.RecipientId, cancellationToken);
            if (!(due == today ? settings.DueToday : due > today ? settings.AdvanceWarning : settings.Overdue))
            {
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

    /// <summary>
    /// Zur Abendzeit einer Person (wenn an und der Schalter "Haushalt" an ist; nach einem Ausfall noch eine Stunde lang) eine
    /// Zusammenfassung dessen, was seit der letzten bis zu dieser Abendzeit im Haushalt geschah und die Person sehen darf; ohne
    /// Ereignisse kommt keine. Was nach der Abendzeit geschieht, steht in der Zusammenfassung des nächsten Abends.
    /// </summary>
    private async Task<int> SendHouseholdSummariesAsync(DateOnly today, TimeOnly time, CancellationToken cancellationToken)
    {
        var people = await db.ReminderDevices.AsNoTracking().Select(d => d.UserId).Distinct().ToListAsync(cancellationToken);
        var sent = 0;
        foreach (var personId in people)
        {
            var settings = await SettingsOfAsync(personId, cancellationToken);
            if (!settings.Household || !settings.EveningEnabled || settings.EveningTime > time
                || time > settings.EveningTime.Add(SummaryCatchUp))
            {
                continue;
            }
            if (await db.Reminders.AnyAsync(r => r.RecipientId == personId && r.Kind == ReminderKind.Household && r.Day == today, cancellationToken))
            {
                continue;
            }
            // Die Zusammenfassung deckt bis zur Abendzeit ab, nicht bis jetzt: Was später geschieht, kommt in die nächste.
            var until = EveningMoment(today, settings.EveningTime);
            var lastEnd = await db.Reminders
                .Where(r => r.RecipientId == personId && r.Kind == ReminderKind.Household)
                .OrderByDescending(r => r.CoversUntil)
                .Select(r => r.CoversUntil)
                .FirstOrDefaultAsync(cancellationToken);
            // Die erste deckt den Tag seit dem Abend davor ab, damit nichts zwischen zwei Abenden verloren geht.
            var from = lastEnd ?? EveningMoment(today.AddDays(-1), settings.EveningTime);
            if ((await HouseholdSummary.VisibleAsync(db, personId, from, until, cancellationToken)).Count == 0)
            {
                continue;
            }
            var reminder = new Reminder
            {
                Id = Guid.NewGuid(),
                PositionId = null,
                RecipientId = personId,
                Day = today,
                Kind = ReminderKind.Household,
                State = ReminderState.Sent,
                CoversFrom = from,
                CoversUntil = until,
            };
            db.Reminders.Add(reminder);
            await db.SaveChangesAsync(cancellationToken);
            sent += await push.SendAsync(await DevicesOfAsync(personId, cancellationToken), new PushPayload(reminder.Id, PushPayload.Household), cancellationToken);
        }
        return sent;
    }

    /// <summary>Der Zeitpunkt, zu dem der Abend dieses Tages in Berlin beginnt.</summary>
    private static DateTimeOffset EveningMoment(DateOnly day, TimeOnly time)
    {
        var local = day.ToDateTime(time);
        return new DateTimeOffset(local, Zone.GetUtcOffset(local)).ToUniversalTime();
    }

    private async Task<ReminderSettings> SettingsOfAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!settingsCache.TryGetValue(userId, out var settings))
        {
            settings = await db.ReminderSettings.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken)
                ?? new ReminderSettings { UserId = userId };
            settingsCache[userId] = settings;
        }
        return settings;
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
