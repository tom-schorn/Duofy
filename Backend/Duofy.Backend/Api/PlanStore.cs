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

    /// <summary>
    /// Die Monate, für die die Person einen Plan hat, der neueste zuerst, mit dem Frei-Betrag je Monat (Spec 23; Spec 93).
    /// Legt nichts an; die Zahl der Monate ist die der vorhandenen Pläne.
    /// </summary>
    /// <param name="seesBookings">
    /// False, wenn der Betrachter die Buchungen nicht sieht: Der Übertrag und das Ungeplante bleiben dann draußen (Spec 68, R4).
    /// </param>
    public async Task<List<PlanMonthResponse>> MonthsAsync(Guid ownerId, bool seesBookings, CancellationToken cancellationToken)
    {
        var plans = await db.Plans
            .AsNoTracking()
            .Include(p => p.Positions)
            .Where(p => p.OwnerId == ownerId)
            .ToListAsync(cancellationToken);
        var bookings = seesBookings ? await BookingsOfAsync([ownerId], cancellationToken) : [];
        var available = seesBookings ? await AvailableAccountIdsAsync([ownerId], cancellationToken) : [];
        return MonthFigures(plans, bookings, available)
            .OrderByDescending(m => m.Plan.Year)
            .ThenByDescending(m => m.Plan.Month)
            .Select(m => new PlanMonthResponse(
                m.Plan.Year, m.Plan.Month, PlanFigures.Free(m.Plan.Positions, m.Booked, m.CarriedIn ?? 0m, m.Unplanned, m.Transfers), m.CarriedIn))
            .ToList();
    }

    /// <summary>
    /// Die Monate mit Plan für die Plan-Übersicht und die Jahreslinie (Issue #94): alle des Jahres und die vorhandenen
    /// künftigen Monate, der neueste zuerst, mit Verplanbar, Anstehend und den Töpfen. Die Zahlen kommen aus denselben
    /// <see cref="PlanFigures"/> wie im Monat selbst. Legt nichts an; die Menge ist begrenzt durch die vorhandenen Pläne ab dem Jahr.
    /// </summary>
    /// <param name="ownerId">Wessen Pläne.</param>
    /// <param name="year">Das Jahr; wer aufruft, prüft es gegen <see cref="PlanRules"/>.</param>
    /// <param name="seesBookings">False, wenn der Betrachter die Buchungen nicht sieht: Gebucht, Übertrag und Ungeplantes bleiben draußen.</param>
    public async Task<List<PlanOverviewMonthResponse>> OverviewAsync(Guid ownerId, int year, bool seesBookings, CancellationToken cancellationToken)
    {
        // Ab dem Jahr laden: Der Übertrag in den nächsten Monat kommt aus dessen Plan, auch aus dem Januar des Folgejahres.
        var plans = await db.Plans
            .AsNoTracking()
            .Include(p => p.Positions)
            .Where(p => p.OwnerId == ownerId && p.Year >= year)
            .ToListAsync(cancellationToken);
        var bookings = seesBookings
            ? await BookingEndpoints.WithLines(db)
                .AsNoTracking()
                .Where(b => b.OwnerId == ownerId && b.PlanYear >= year)
                .ToListAsync(cancellationToken)
            : [];
        var available = seesBookings ? await AvailableAccountIdsAsync([ownerId], cancellationToken) : [];
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        return MonthFigures(plans, bookings, available)
            .Where(m => m.Plan.Year == year || (m.Plan.Year, m.Plan.Month).CompareTo((today.Year, today.Month)) > 0)
            .OrderByDescending(m => m.Plan.Year)
            .ThenByDescending(m => m.Plan.Month)
            .Select(m => new PlanOverviewMonthResponse(
                m.Plan.Year,
                m.Plan.Month,
                PlanFigures.Free(m.Plan.Positions, m.Booked, m.CarriedIn ?? 0m, m.Unplanned, m.Transfers),
                m.CarriedIn is not null,
                m.Plan.Positions
                    .Where(p => p.Pot != Pot.Income && !p.PassThrough && !p.IsMoved && p.DoneAt is null)
                    .Sum(p => Math.Max(p.AmountPlanned - m.Booked.GetValueOrDefault(p.Id), 0m)),
                PlanFigures.PotTotals(m.Plan.Positions, m.Booked, m.Unplanned, m.PotShift)
                    .Select(entry => new PlanPotResponse(PotKey.ToKey(entry.Key), entry.Value.Planned, entry.Value.Booked))
                    .ToList()))
            .ToList();
    }

    /// <summary>
    /// Alle Buchungen dieser Personen mit ihren Zeilen, für die Frei-Beträge in den Listen der Monate. Wer aufruft, prüft
    /// vorher, wessen Buchungen zu sehen sind.
    /// </summary>
    public Task<List<Booking>> BookingsOfAsync(IReadOnlyCollection<Guid> ownerIds, CancellationToken cancellationToken) =>
        BookingEndpoints.WithLines(db)
            .AsNoTracking()
            .Where(b => ownerIds.Contains(b.OwnerId))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Die Kennungen der Konten dieser Personen, die als verfügbar zählen, aktive und aufgelöste, denn auch eine Umbuchung
    /// von einem aufgelösten Konto steht noch im Planmonat (Spec 98).
    /// </summary>
    public async Task<HashSet<Guid>> AvailableAccountIdsAsync(IReadOnlyCollection<Guid> ownerIds, CancellationToken cancellationToken) =>
        (await db.Accounts
            .AsNoTracking()
            .Where(a => ownerIds.Contains(a.OwnerId) && a.CountsAsAvailable)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken))
        .ToHashSet();

    /// <summary>
    /// Gebucht je Posten, Übertrag und Ungeplantes je Plan für die Frei-Beträge in den Listen der Monate, mit denselben Regeln wie im Monat
    /// selbst (<see cref="ResponseAsync"/>): die Buchungen des Planmonats, ohne die Buchungen der Überträge rund um ihn.
    /// </summary>
    /// <param name="plans">Die Pläne; der Übertrag in den nächsten Monat kommt aus dessen Plan, wenn er in der Liste ist.</param>
    /// <param name="bookings">Die Buchungen, die der Betrachter sehen darf; ohne Sicht leer.</param>
    /// <param name="availableAccountIds">Die verfügbaren Konten der Besitzer der Pläne, für die Umbuchungen (Spec 98).</param>
    public static List<(Plan Plan, Dictionary<Guid, decimal> Booked, decimal? CarriedIn, Dictionary<Pot, decimal> Unplanned, decimal Transfers, Dictionary<Pot, decimal> PotShift)> MonthFigures(
        IReadOnlyCollection<Plan> plans, IReadOnlyCollection<Booking> bookings, IReadOnlySet<Guid> availableAccountIds)
    {
        var byMonth = bookings.ToLookup(b => (b.OwnerId, b.PlanYear, b.PlanMonth));
        var byPlan = plans.ToDictionary(p => (p.OwnerId, p.Year, p.Month));
        return plans.Select(plan =>
        {
            var inMonth = byMonth[(plan.OwnerId, plan.Year, plan.Month)].ToList();
            var next = new DateOnly(plan.Year, plan.Month, 1).AddMonths(1);
            var carryOverIds = CarryOverIds(plan, byPlan.GetValueOrDefault((plan.OwnerId, next.Year, next.Month)));
            return (plan, PlanFigures.BookedPerPosition(inMonth), PlanFigures.CarriedIn(plan, inMonth), PlanFigures.Unplanned(inMonth, carryOverIds),
                PlanFigures.TransferEffect(inMonth, availableAccountIds, carryOverIds), PlanFigures.PotShift(inMonth));
        }).ToList();
    }

    /// <summary>
    /// Die Buchungen aller Überträge rund um einen Monat: die beiden des eigenen und die beiden des Übertrags in den nächsten
    /// Monat, von denen eine in diesem Planmonat steht. Keine davon zählt als ungeplant (Spec 72) oder in einer Summe.
    /// </summary>
    public static List<Guid> CarryOverIds(Plan plan, Plan? next) =>
        new[] { plan.CarryOverIncomeBookingId, plan.CarryOverExpenseBookingId, next?.CarryOverIncomeBookingId, next?.CarryOverExpenseBookingId }
            .OfType<Guid>()
            .ToList();

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
            : await SavingsGoalEndpoints.PlanCommitmentsAsync(db, ownerId, cancellationToken);

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
    public async Task<PlanPositionResponse> PositionResponseAsync(Guid ownerId, PlanPosition position, int year, int month, PlanSight sight, CancellationToken cancellationToken)
    {
        if (!sight.Book)
        {
            return Hide(ToResponse(position, 0m, year, month));
        }
        var booked = await BookedAsync(ownerId, [position.Id], cancellationToken);
        return ToResponse(position, booked.GetValueOrDefault(position.Id), year, month);
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
        // Die Buchungen aller Überträge rund um den Monat; sie zählen nicht als ungeplant (Spec 72) und in keiner Summe:
        // Die Summen zeigen nur echtes Geld, der Übertrag steht in seiner eigenen Zeile (Spec 66, R9).
        var carryOverIds = nextCarryOver.Append(plan.CarryOverIncomeBookingId).Append(plan.CarryOverExpenseBookingId).OfType<Guid>().ToList();
        var unplanned = PlanFigures.Unplanned(bookings, carryOverIds);
        var transfers = PlanFigures.TransferEffect(
            bookings, sight.Book ? await AvailableAccountIdsAsync([plan.OwnerId], cancellationToken) : [], carryOverIds);
        var sums = MonthBook.SumsOf(bookings.Where(b => !carryOverIds.Contains(b.Id)), accountId: null);

        var commitments = sight.Commitments
            ? await SavingsGoalEndpoints.PlanCommitmentsAsync(db, plan.OwnerId, cancellationToken)
            : [];
        HashSet<Guid> hidden = sight.Commitments && !sight.Savings
            ? await SavingsGoalEndpoints.SavingsTypeCommitmentIdsAsync(db, plan.OwnerId, cancellationToken)
            : [];
        HashSet<Guid> goalLinked = sight.Commitments && !sight.Savings
            ? await SavingsGoalEndpoints.GoalLinkedCommitmentIdsAsync(db, plan.OwnerId, cancellationToken)
            : [];
        var changes = sight.Commitments
            ? PlanChanges.Compare(plan, commitments, await FirstOpeningAsync(plan.OwnerId, cancellationToken))
                .Where(change => !hidden.Contains(change.CommitmentId)
                    && !(change.Kind == PlanChangeKind.NoLongerDue && goalLinked.Contains(change.CommitmentId)))
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
            PlanFigures.CarriedIn(plan, bookings),
            balances);

        Pot[] quotaPots = [Pot.Needs, Pot.Wants, Pot.Savings];
        return new PlanResponse(
            plan.Id,
            plan.Year,
            plan.Month,
            plan.Positions.Select(p => sight.Book
                ? ToResponse(p, booked.GetValueOrDefault(p.Id), plan.Year, plan.Month)
                : Hide(ToResponse(p, 0m, plan.Year, plan.Month))).ToList(),
            plan.Positions.Where(p => !p.IsMoved && p.Pot == Pot.Income).Sum(p => p.AmountPlanned),
            plan.Positions.Where(p => !p.IsMoved && p.Pot != Pot.Income).Sum(p => p.AmountPlanned),
            sums.Income,
            sums.Expenses,
            quotaPots.Select(pot => new PlanQuotaResponse(PotKey.ToKey(pot), plan.Quotas.For(pot)!.Value, actual[pot])).ToList(),
            changes,
            carryOver,
            new PlanOverviewResponse(
                PlanFigures.Income(plan.Positions),
                PlanFigures.Fixed(plan.Positions),
                // Ohne Sicht auf die Buchungen ist die Liste leer, und so bleiben auch der Übertrag und das Ungeplante
                // draußen (Spec 68, R4).
                PlanFigures.Free(plan.Positions, booked, PlanFigures.CarriedIn(plan, bookings) ?? 0m, unplanned, transfers),
                PlanFigures.PotTotals(plan.Positions, booked, unplanned, PlanFigures.PotShift(bookings))
                    .Select(entry => new PlanPotResponse(PotKey.ToKey(entry.Key), entry.Value.Planned, entry.Value.Booked))
                    .ToList(),
                bookings
                    .Where(b => !carryOverIds.Contains(b.Id))
                    .OrderByDescending(b => b.PaymentDate)
                    .ThenByDescending(b => b.CreatedAt)
                    .Select(BookingEndpoints.ToResponse)
                    .ToList(),
                CarryOverLines(plan, nextCarryOver.ElementAtOrDefault(0), nextCarryOver.ElementAtOrDefault(1), bookings),
                // Jetzt verfügbar (Spec 139): nur der laufende Monat, und wie Kontostände und Buchungen nur mit Sicht auf beide.
                IsCurrentMonth(plan) && sight.Accounts && sight.Book
                    ? ToResponse(await AvailableNowAsync(plan, bookings, Today, cancellationToken))
                    : null));
    }

    public DateOnly Today => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    /// <summary>True, wenn der Plan der des laufenden Monats ist (Spec 139).</summary>
    public bool IsCurrentMonth(Plan plan) => IsCurrentMonth(plan.Year, plan.Month);

    /// <summary>True, wenn Jahr und Monat der laufende Monat sind (Spec 139).</summary>
    public bool IsCurrentMonth(int year, int month) => year == Today.Year && month == Today.Month;

    /// <summary>
    /// "Jetzt verfügbar" eines Plans (Spec 139): die Kontostände von heute ohne Sparkonten minus die offenen Ausgaben des
    /// Monats, siehe <see cref="PlanFigures.AvailableNow"/>. Wer aufruft, prüft vorher die Sicht auf Konten und Buchungen.
    /// </summary>
    /// <param name="bookings">Die Buchungen mit diesem Planmonat.</param>
    public async Task<AvailableNowFigure> AvailableNowAsync(Plan plan, IReadOnlyCollection<Booking> bookings, DateOnly today, CancellationToken cancellationToken)
    {
        var accounts = await db.Accounts
            .AsNoTracking()
            .Where(a => a.OwnerId == plan.OwnerId && a.Active && a.OpeningDate <= today)
            .ToListAsync(cancellationToken);
        var counting = accounts.Where(a => Account.CountsAsAvailableFor(a.Type)).ToList();
        var allBookings = await BookingEndpoints.WithLines(db)
            .AsNoTracking()
            .Where(b => b.OwnerId == plan.OwnerId && b.PaymentDate <= today)
            .ToListAsync(cancellationToken);
        foreach (var account in counting)
        {
            account.Bookings = allBookings.Where(b => b.AccountId == account.Id || b.CounterAccountId == account.Id).ToList();
        }
        // Für die Umbuchungen auf ein Sparkonto zählt jedes Konto, das keine Sparkontoart hat, auch ein geschlossenes.
        var nonSavingsIds = (await db.Accounts.AsNoTracking().Where(a => a.OwnerId == plan.OwnerId).Select(a => new { a.Id, a.Type }).ToListAsync(cancellationToken))
            .Where(a => Account.CountsAsAvailableFor(a.Type)).Select(a => a.Id).ToHashSet();
        return PlanFigures.AvailableNow(plan.Positions, PlanFigures.BookedPerPosition(bookings), nonSavingsIds, counting.Sum(a => a.Balance(today)));
    }

    public static AvailableNowResponse ToResponse(AvailableNowFigure figure) => new(figure.Balances, figure.OpenExpenses, figure.Value);

    /// <summary>
    /// Die Überträge eines Monats als eigene Zeilen (Spec 66, R9): "Übertrag aus ..." mit dem eigenen Übertrag und
    /// "Übertrag nach ..." mit dem des nächsten Monats. Beide kommen aus den Buchungen dieses Planmonats; ohne Sicht auf
    /// die Buchungen ist die Liste leer, und so gibt es auch keine Zeile.
    /// </summary>
    /// <param name="plan">Der Monat.</param>
    /// <param name="nextIncomeId">Die Einnahme im Übertrag des nächsten Monats; sie liegt hier bei einem negativen Rest.</param>
    /// <param name="nextExpenseId">Die Ausgabe im Übertrag des nächsten Monats; sie liegt hier bei einem positiven Rest.</param>
    /// <param name="bookings">Die Buchungen mit diesem Planmonat.</param>
    /// <param name="ownerFirstName">Nur im Haushaltsplan: wessen Übertrag es ist.</param>
    public static List<PlanCarryOverLineResponse> CarryOverLines(
        Plan plan, Guid? nextIncomeId, Guid? nextExpenseId, IReadOnlyCollection<Booking> bookings, string? ownerFirstName = null)
    {
        var first = new DateOnly(plan.Year, plan.Month, 1);
        var lines = new List<PlanCarryOverLineResponse>();
        if (PlanFigures.CarriedIn(plan, bookings) is { } carriedIn)
        {
            var previous = first.AddMonths(-1);
            lines.Add(new PlanCarryOverLineResponse(CarryOverDirections.In, previous.Year, previous.Month, carriedIn, ownerFirstName));
        }
        var carriedOut = bookings.FirstOrDefault(b => b.Id == nextExpenseId)?.Amount ?? -bookings.FirstOrDefault(b => b.Id == nextIncomeId)?.Amount;
        if (carriedOut is { } amount)
        {
            var next = first.AddMonths(1);
            lines.Add(new PlanCarryOverLineResponse(CarryOverDirections.Out, next.Year, next.Month, amount, ownerFirstName));
        }
        return lines;
    }

    /// <summary>Die Buchungen der Person mit diesem Planmonat, mit ihren Zeilen.</summary>
    public Task<List<Booking>> BookingsInPlanMonthAsync(Guid ownerId, int year, int month, CancellationToken cancellationToken) =>
        BookingEndpoints.WithLines(db)
            .AsNoTracking()
            .Where(b => b.OwnerId == ownerId && b.PlanYear == year && b.PlanMonth == month)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Ein Posten als Antwort; <paramref name="year"/> und <paramref name="month"/> sind der Monat seines Plans, aus ihm folgt
    /// der Zielmonat eines verschobenen Postens und der Ursprungsmonat einer Kopie (immer ein Monat davor oder danach).
    /// </summary>
    public static PlanPositionResponse ToResponse(PlanPosition position, decimal booked, int year, int month) => new(
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
        position.TickBookingId,
        position.IsMoved ? Shifted(year, month, 1) : null,
        position.MovedFromPositionId is not null ? Shifted(year, month, -1) : null);

    /// <summary>Der Monat <paramref name="by"/> Monate nach dem gegebenen.</summary>
    public static PlanMonthRef Shifted(int year, int month, int by)
    {
        var moved = new DateOnly(year, month, 1).AddMonths(by);
        return new PlanMonthRef(moved.Year, moved.Month);
    }

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
internal readonly record struct PlanSight(bool Book, bool Accounts, bool Commitments, bool Savings = true)
{
    public static async Task<PlanSight> OfAsync(IAccess access, Guid ownerId, CancellationToken cancellationToken) => new(
        (await access.LevelAsync(ownerId, Area.Book, cancellationToken)).Includes(AccessLevel.View),
        (await access.LevelAsync(ownerId, Area.Accounts, cancellationToken)).Includes(AccessLevel.View),
        (await access.LevelAsync(ownerId, Area.Commitments, cancellationToken)).Includes(AccessLevel.View),
        (await access.LevelAsync(ownerId, Area.Savings, cancellationToken)).Includes(AccessLevel.View));
}
