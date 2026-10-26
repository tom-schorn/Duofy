using System.Globalization;
using Duofy.Schemas;
using Duofy.Data;
using Duofy.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Duofy.Api;

/// <summary>
/// Liest und legt die Monatspläne einer Person an und baut die Antwort der API daraus. Die
/// Endpunkte in <see cref="PlanEndpoints"/> prüfen die Anfragen und ändern die Pläne; alles, was
/// mehrere von ihnen brauchen, steht hier.
/// </summary>
internal sealed class PlanStore(DuofyDbContext db, TimeProvider time, ILogger<PlanStore> logger)
{
    /// <summary>
    /// Liefert den Plan des Monats und legt ihn beim ersten Öffnen an (R1): ein Posten je Vertrag,
    /// der im Monat fällig ist, und die Quoten aus den Einstellungen der Person. Ein Monat, der vor
    /// dem ersten Starttag eines Kontos endet, bleibt leer.
    /// </summary>
    public async Task<Plan> GetOrCreateAsync(Guid ownerId, int year, int month, CancellationToken cancellationToken)
    {
        if (await FindAsync(ownerId, year, month, cancellationToken) is { } existing)
        {
            return existing;
        }

        var plan = await NewAsync(ownerId, year, month, cancellationToken);
        db.Plans.Add(plan);
        try
        {
            // Wer den Monat eines anderen Mitglieds nur ansehen darf, legt ihn beim Öffnen trotzdem an: Das Anlegen
            // gehört zum Öffnen (R1), die zentrale Prüfung hat Ansehen vorher verlangt. Der Besitzer-Filter
            // (Spec 56) ließe das Speichern sonst nur mit Anlegen zu.
            using (ownerId == db.Owners.CurrentUserId ? null : db.Owners.Bypass("Mitgliedermonat anlegen"))
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            logger.LogInformation("Plan {Year}-{Month} von {UserId} angelegt, {Count} Posten.", year, month, ownerId, plan.Positions.Count);
            return plan;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Zwei Anfragen haben denselben Monat gleichzeitig angelegt; der eindeutige Index lässt
            // nur eine durch. Die andere nimmt den Plan, der gewonnen hat.
            logger.LogInformation(ex, "Plan {Year}-{Month} von {UserId} gleichzeitig angelegt; nehme den vorhandenen.", year, month, ownerId);
            db.ChangeTracker.Clear();
            return await FindAsync(ownerId, year, month, cancellationToken)
                ?? throw new InvalidOperationException($"Plan {year}-{month} von {ownerId} weder angelegt noch gefunden.");
        }
    }

    /// <summary>Der Plan des Monats mit seinen Posten in der Reihenfolge des Plans; null, wenn es ihn noch nicht gibt.</summary>
    public async Task<Plan?> FindAsync(Guid ownerId, int year, int month, CancellationToken cancellationToken)
    {
        var plan = await db.Plans
            .Include(p => p.Positions)
            .FirstOrDefaultAsync(p => p.OwnerId == ownerId && p.Year == year && p.Month == month, cancellationToken);
        plan?.SortPositions();
        return plan;
    }

    private async Task<Plan> NewAsync(Guid ownerId, int year, int month, CancellationToken cancellationToken)
    {
        var quotas = await db.Users
            .Where(u => u.Id == ownerId)
            .Select(u => new Quotas(u.QuotaNeeds, u.QuotaWants, u.QuotaSavings))
            .FirstAsync(cancellationToken);
        var firstOpening = await FirstOpeningAsync(ownerId, cancellationToken);

        var lastDay = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var commitments = firstOpening is { } opening && lastDay < opening
            ? []
            : await db.Commitments.AsNoTracking().Where(c => c.OwnerId == ownerId).ToListAsync(cancellationToken);

        var plan = Plan.CreateFrom(commitments, year, month);
        plan.Id = Guid.NewGuid();
        plan.OwnerId = ownerId;
        plan.Quotas = quotas;
        plan.CreatedAt = time.GetUtcNow();
        return plan;
    }

    /// <summary>
    /// Sperrt die Zeile des Plans bis zum Ende der Transaktion (<c>SELECT … FOR UPDATE</c>). Von zwei gleichzeitigen
    /// Anfragen, die im selben Monat buchen (Abhaken, Übertrag), wartet die zweite, bis die erste fertig ist, und
    /// sieht dann deren Buchung. Muss in einer Transaktion laufen; was danach gelesen wird, ist frisch.
    /// </summary>
    public Task LockAsync(Guid ownerId, int year, int month, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlRawAsync(
            "SELECT id FROM plans WHERE owner_id = {0} AND year = {1} AND month = {2} FOR UPDATE",
            [ownerId, year, month],
            cancellationToken);

    /// <summary>Der früheste Starttag eines Kontos der Person; null, wenn sie noch kein Konto hat.</summary>
    public Task<DateOnly?> FirstOpeningAsync(Guid ownerId, CancellationToken cancellationToken) =>
        db.Accounts
            .Where(a => a.OwnerId == ownerId)
            .MinAsync(a => (DateOnly?)a.OpeningDate, cancellationToken);

    /// <summary>Gebucht je Posten des Plans, aus allen Zeilen der Person, die auf diese Posten zeigen.</summary>
    public async Task<Dictionary<Guid, decimal>> BookedAsync(Guid ownerId, IReadOnlyCollection<Guid> positionIds, CancellationToken cancellationToken)
    {
        var lines = await db.Bookings
            .Where(b => b.OwnerId == ownerId)
            .SelectMany(b => b.Lines)
            .Where(l => l.PositionId != null && positionIds.Contains(l.PositionId.Value))
            .Select(l => new { PositionId = l.PositionId!.Value, l.Amount })
            .ToListAsync(cancellationToken);
        return lines.GroupBy(l => l.PositionId).ToDictionary(g => g.Key, g => g.Sum(l => l.Amount));
    }

    /// <summary>True, wenn mindestens eine Buchungszeile dem Posten zugeordnet ist.</summary>
    public Task<bool> HasBookingsAsync(Guid positionId, CancellationToken cancellationToken) =>
        db.Set<BookingLine>().AnyAsync(l => l.PositionId == positionId, cancellationToken);

    /// <summary>Das heutige Standardkonto der Person; null, wenn sie keines hat.</summary>
    public Task<Guid?> DefaultAccountAsync(Guid ownerId, CancellationToken cancellationToken) =>
        db.Accounts
            .Where(a => a.OwnerId == ownerId && a.IsDefault)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Die gerechneten Stände der offenen Konten am angegebenen Tag. Konten, die erst an diesem Tag
    /// oder später starten, fehlen: Auf sie lässt sich an diesem Tag nichts mehr buchen.
    /// </summary>
    public async Task<List<Account>> BalancesOnAsync(Guid ownerId, DateOnly day, CancellationToken cancellationToken)
    {
        var accounts = await db.Accounts
            .AsNoTracking()
            .Where(a => a.OwnerId == ownerId && a.Active && a.OpeningDate < day)
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.Name)
            .ToListAsync(cancellationToken);
        var bookings = await BookingEndpoints.WithLines(db)
            .AsNoTracking()
            .Where(b => b.OwnerId == ownerId && b.PaymentDate <= day)
            .ToListAsync(cancellationToken);
        foreach (var account in accounts)
        {
            account.Bookings = bookings.Where(b => b.AccountId == account.Id || b.CounterAccountId == account.Id).ToList();
        }
        return accounts;
    }

    /// <summary>Ein Posten in der Antwort, mit seinem gebuchten Betrag, wenn die Buchungen zu sehen sind.</summary>
    public async Task<PlanPositionResponse> PositionResponseAsync(Guid ownerId, PlanPosition position, PlanSight sight, CancellationToken cancellationToken)
    {
        if (!sight.Book)
        {
            return Hide(ToResponse(position, 0m));
        }
        var booked = await BookedAsync(ownerId, [position.Id], cancellationToken);
        return ToResponse(position, booked.GetValueOrDefault(position.Id));
    }

    /// <summary>Ein Posten ohne Verweis auf die Buchung vom Abhaken, für wen die Buchungen nicht sieht.</summary>
    private static PlanPositionResponse Hide(PlanPositionResponse position) => position with { Booked = 0m, TickBookingId = null };

    /// <summary>
    /// Der ganze Monat für die Antwort: Posten mit gebuchten Beträgen, Summen, Quoten, die
    /// Unterschiede zu den Verträgen und was der Dialog "Übertrag" braucht. Was zu einem Bereich gehört, den
    /// <paramref name="sight"/> nicht zeigt, bleibt leer (Spec 68, R4): gebuchte Beträge ohne die Buchungen,
    /// Kontostände ohne die Konten, die Unterschiede zu den Verträgen ohne die Verträge.
    /// </summary>
    public async Task<PlanResponse> ResponseAsync(Plan plan, PlanSight sight, CancellationToken cancellationToken)
    {
        var bookings = sight.Book ? await BookingsInPlanMonthAsync(plan.OwnerId, plan.Year, plan.Month, cancellationToken) : [];
        var booked = PlanFigures.BookedPerPosition(bookings);
        // Der Übertrag in den nächsten Monat bucht eine seiner Buchungen in diesen Planmonat, bei einem negativen
        // Rest eine Einnahme. Sie ist kein verdientes Geld und zählt in den Quoten so wenig wie der eigene Übertrag.
        var next = new DateOnly(plan.Year, plan.Month, 1).AddMonths(1);
        var nextCarryOver = await db.Plans
            .AsNoTracking()
            .Where(p => p.OwnerId == plan.OwnerId && p.Year == next.Year && p.Month == next.Month)
            .Select(p => new[] { p.CarryOverIncomeBookingId, p.CarryOverExpenseBookingId })
            .FirstOrDefaultAsync(cancellationToken) ?? [];
        var actual = PlanFigures.QuotaActual(
            plan, bookings.Where(b => !nextCarryOver.Contains(b.Id)).ToList(), plan.CarryOverIncomeBookingId);
        var sums = MonthBook.SumsOf(bookings, accountId: null);

        var commitments = sight.Commitments
            ? await db.Commitments.AsNoTracking().Where(c => c.OwnerId == plan.OwnerId).ToListAsync(cancellationToken)
            : [];
        var changes = sight.Commitments
            ? PlanChanges.Compare(plan, commitments, await FirstOpeningAsync(plan.OwnerId, cancellationToken))
                .Select(change => ToResponse(change, plan, commitments)).ToList()
            : [];

        var first = new DateOnly(plan.Year, plan.Month, 1);
        var previous = first.AddMonths(-1);
        var previousBookings = sight.Book ? await BookingsInPlanMonthAsync(plan.OwnerId, previous.Year, previous.Month, cancellationToken) : [];
        var lastOfPrevious = first.AddDays(-1);
        var balances = sight.Accounts
            ? (await BalancesOnAsync(plan.OwnerId, lastOfPrevious, cancellationToken))
                .Select(a => new PlanAccountBalanceResponse(a.Id, a.Balance(lastOfPrevious)))
                .ToList()
            : [];
        var carryOver = new PlanCarryOverResponse(
            PlanFigures.CarryOverSuggestion(previousBookings),
            plan.HasCarryOver,
            CarryOverAmount(plan, bookings),
            balances);

        Pot[] quotaPots = [Pot.Needs, Pot.Wants, Pot.Savings];
        return new PlanResponse(
            plan.Id,
            plan.Year,
            plan.Month,
            plan.Positions.Select(p => sight.Book ? ToResponse(p, booked.GetValueOrDefault(p.Id)) : Hide(ToResponse(p, 0m))).ToList(),
            plan.Positions.Where(p => p.Pot == Pot.Income).Sum(p => p.AmountPlanned),
            plan.Positions.Where(p => p.Pot != Pot.Income).Sum(p => p.AmountPlanned),
            sums.Income,
            sums.Expenses,
            quotaPots.Select(pot => new PlanQuotaResponse(PotKey.ToKey(pot), plan.Quotas.For(pot)!.Value, actual[pot])).ToList(),
            changes,
            carryOver);
    }

    /// <summary>
    /// Der Betrag des Übertrags in diesen Monat aus seiner Buchung am Ersten: bei einem positiven Rest die Einnahme,
    /// bei einem negativen die Ausgabe als negativer Betrag; null ohne Übertrag oder ohne Sicht auf die Buchungen.
    /// </summary>
    private static decimal? CarryOverAmount(Plan plan, IReadOnlyCollection<Booking> bookings) =>
        bookings.FirstOrDefault(b => b.Id == plan.CarryOverIncomeBookingId)?.Amount
        ?? -bookings.FirstOrDefault(b => b.Id == plan.CarryOverExpenseBookingId)?.Amount;

    /// <summary>Die Buchungen der Person mit diesem Planmonat, mit ihren Zeilen.</summary>
    public Task<List<Booking>> BookingsInPlanMonthAsync(Guid ownerId, int year, int month, CancellationToken cancellationToken) =>
        BookingEndpoints.WithLines(db)
            .AsNoTracking()
            .Where(b => b.OwnerId == ownerId && b.PlanYear == year && b.PlanMonth == month)
            .ToListAsync(cancellationToken);

    public static PlanPositionResponse ToResponse(PlanPosition position, decimal booked) => new(
        position.Id,
        position.CommitmentId,
        position.Label,
        position.AmountPlanned,
        position.DueDay,
        CommitmentTypeKey.ToKey(position.Type),
        position.CategoryId,
        PotKey.ToKey(position.Pot),
        position.AccountId,
        position.CounterAccountId,
        position.PassThrough,
        PaymentMethodKeys.ToKey(position.PaymentMethod),
        booked,
        position.DoneAt,
        position.ChangedByHand,
        position.TickBookingId);

    private static PlanChangeResponse ToResponse(PlanChange change, Plan plan, IReadOnlyList<Commitment> commitments)
    {
        var label = plan.Positions.FirstOrDefault(p => p.Id == change.PositionId)?.Label
            ?? commitments.First(c => c.Id == change.CommitmentId).Name;
        return new PlanChangeResponse(
            change.Kind switch
            {
                PlanChangeKind.Changed => PlanChangeKinds.Changed,
                PlanChangeKind.New => PlanChangeKinds.New,
                _ => PlanChangeKinds.NoLongerDue,
            },
            change.PositionId,
            change.CommitmentId,
            label,
            change.Fields.Select(f => new PlanFieldChangeResponse(FieldKey(f.Field), Text(f.Before), Text(f.After))).ToList(),
            change.Protected);
    }

    private static string FieldKey(PlanField field) => field switch
    {
        PlanField.Label => PlanFields.Label,
        PlanField.Amount => PlanFields.Amount,
        PlanField.DueDay => PlanFields.DueDay,
        PlanField.Type => PlanFields.Type,
        PlanField.Category => PlanFields.Category,
        PlanField.Pot => PlanFields.Pot,
        PlanField.Account => PlanFields.Account,
        PlanField.CounterAccount => PlanFields.CounterAccount,
        PlanField.PassThrough => PlanFields.PassThrough,
        PlanField.PaymentMethod => PlanFields.PaymentMethod,
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unbekanntes Feld."),
    };

    /// <summary>Ein Wert als Text für die API, unabhängig von der Sprache des Servers.</summary>
    private static string? Text(object? value) => value switch
    {
        null => null,
        decimal amount => amount.ToString("0.00", CultureInfo.InvariantCulture),
        bool flag => flag ? "true" : "false",
        Pot pot => PotKey.ToKey(pot),
        CommitmentType type => CommitmentTypeKey.ToKey(type),
        PaymentMethod method => PaymentMethodKeys.ToKey(method),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };
}

/// <summary>
/// Welche anderen Bereiche ein Plan zeigen darf (Spec 68, R4): die eigenen alle, bei einem anderen Mitglied nur
/// die, die es mindestens zum Ansehen freigegeben hat.
/// </summary>
internal readonly record struct PlanSight(bool Book, bool Accounts, bool Commitments)
{
    public static async Task<PlanSight> OfAsync(IAccess access, Guid ownerId, CancellationToken cancellationToken) => new(
        (await access.LevelAsync(ownerId, Area.Book, cancellationToken)).Includes(AccessLevel.View),
        (await access.LevelAsync(ownerId, Area.Accounts, cancellationToken)).Includes(AccessLevel.View),
        (await access.LevelAsync(ownerId, Area.Commitments, cancellationToken)).Includes(AccessLevel.View));
}
