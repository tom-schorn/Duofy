using Duofy.Schemas;
using Duofy.Data;
using Duofy.Domain;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api;

/// <summary>
/// Setzt den Haushaltsplan eines Monats zusammen (Spec 69): aus den Monaten der Mitglieder, die bis zum letzten Tag
/// des Monats dazugehörten. Nichts davon wird gespeichert außer den fehlenden Monaten der Mitglieder, die wie beim
/// eigenen Öffnen aus deren Verträgen entstehen (R2).
///
/// Alle sehen alle Posten aller gezählten Mitglieder, gleich was freigegeben ist (Entscheidung des Menschen,
/// private Posten folgen in V2). Deshalb läuft das Zusammensetzen im genehmigten Bereich "Haushaltsplan
/// zusammensetzen" ohne Besitzer-Filter; was darüber hinausgeht, sind nur die Konten, und die zeigt die
/// zentrale Prüfung (<see cref="IAccess"/>) nur mit Freigabe.
/// </summary>
internal sealed class HouseholdPlanStore(
    DuofyDbContext db, PlanStore plans, IAccess access, ILogger<HouseholdPlanStore> logger)
{
    private static readonly Pot[] QuotaPots = [Pot.Needs, Pot.Wants, Pot.Savings];

    /// <summary>True, wenn die Person bis zum letzten Tag des Monats dazukam (R1, R5). Das Datum zählt in Berlin, wie überall in der API.</summary>
    public static bool IsCounted(HouseholdMember member, int year, int month) =>
        Berlin.DateOf(member.JoinedAt) <= new DateOnly(year, month, DateTime.DaysInMonth(year, month));

    /// <summary>Der Haushaltsplan des Monats; legt dabei die fehlenden Monate der gezählten Mitglieder an (R2).</summary>
    public async Task<HouseholdPlanResponse> ComposeAsync(Household household, Guid viewerId, int year, int month, CancellationToken cancellationToken)
    {
        using var scope = db.Owners.Bypass("Haushaltsplan zusammensetzen");

        var counted = household.Members.Where(m => IsCounted(m, year, month)).Select(m => m.UserId).ToList();
        var memberIds = household.Members.Select(m => m.UserId).ToList();
        var names = await db.Users
            .AsNoTracking()
            .Where(u => memberIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FirstName, cancellationToken);

        var next = new DateOnly(year, month, 1).AddMonths(1);
        var months = new List<MemberMonth>();
        foreach (var ownerId in counted)
        {
            var plan = await plans.GetOrCreateAsync(ownerId, year, month, cancellationToken);
            var bookings = await plans.BookingsInPlanMonthAsync(ownerId, year, month, cancellationToken);

            // Der Übertrag in den nächsten Monat bucht eine seiner Buchungen in diesen, bei einem positiven Rest die
            // Ausgabe, bei einem negativen die Einnahme; beide zählen hier nirgends mit.
            var nextPlan = await plans.FindAsync(ownerId, next.Year, next.Month, cancellationToken);
            var carryOver = new[]
                {
                    plan.CarryOverIncomeBookingId, plan.CarryOverExpenseBookingId,
                    nextPlan?.CarryOverIncomeBookingId, nextPlan?.CarryOverExpenseBookingId,
                }
                .OfType<Guid>().ToList();
            months.Add(new MemberMonth(plan, bookings, carryOver));
        }

        var figures = HouseholdPlanFigures.Compute(months);
        var positions = months
            .SelectMany(m =>
            {
                var booked = PlanFigures.BookedPerPosition(m.Bookings);
                return m.Plan.Positions.Select(p => ToResponse(p, m.Plan.OwnerId, names[m.Plan.OwnerId], booked.GetValueOrDefault(p.Id)));
            })
            .OrderBy(p => PotOrder(p.Pot))
            .ThenBy(p => p.DueDay)
            .ThenBy(p => p.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var members = MemberQuotas(household, months, names);
        var positionsOf = months.SelectMany(m => m.Plan.Positions).ToList();
        // Einzelne Buchungen gehören zum Bereich Buchungen und folgen den Freigaben wie die Konten: nur die eigenen und
        // die der Mitglieder, die sie dem Betrachter zeigen; die übrigen stehen mit Vornamen in BookingsMissing.
        var bookingsMissing = new List<string>();
        var visibleMonths = new List<MemberMonth>();
        foreach (var memberMonth in months)
        {
            if ((await access.LevelAsync(memberMonth.Plan.OwnerId, Area.Book, cancellationToken)).Includes(AccessLevel.View))
            {
                visibleMonths.Add(memberMonth);
            }
            else
            {
                bookingsMissing.Add(names[memberMonth.Plan.OwnerId]);
            }
        }
        bookingsMissing.Sort(StringComparer.CurrentCultureIgnoreCase);
        var bookingsOf = visibleMonths
            .SelectMany(m => m.Bookings.Select(b => (Booking: b, Name: names[m.Plan.OwnerId])))
            .OrderByDescending(x => x.Booking.PaymentDate)
            .ThenByDescending(x => x.Booking.CreatedAt)
            .Select(x => new HouseholdBookingResponse(
                x.Booking.Id, x.Booking.PaymentDate, x.Booking.Amount, x.Booking.PlanYear, x.Booking.PlanMonth, x.Booking.OwnerId, x.Name))
            .ToList();

        var (accounts, missing) = await AccountsAsync(counted, names, year, month, cancellationToken);
        logger.LogInformation(
            "Haushaltsplan {Year}-{Month} von {HouseholdId} für {UserId} zusammengesetzt: {Members} Mitglieder, {Positions} Posten.",
            year, month, household.Id, viewerId, counted.Count, positions.Count);

        return new HouseholdPlanResponse(
            household.Id,
            household.Name,
            year,
            month,
            positions,
            figures.Income,
            QuotaPots.Select(pot => new HouseholdPotResponse(PotKey.ToKey(pot), figures.Pots[pot].Planned, figures.Pots[pot].Booked)).ToList(),
            figures.Unpaid,
            QuotaPots.Select(pot => new PlanQuotaResponse(PotKey.ToKey(pot), household.Quotas.For(pot)!.Value, figures.QuotaActual[pot])).ToList(),
            new HouseholdUnplannedResponse(
                figures.Unplanned[Pot.Income], figures.Unplanned[Pot.Needs], figures.Unplanned[Pot.Wants], figures.Unplanned[Pot.Savings]),
            accounts,
            missing,
            members,
            PlanFigures.Fixed(positionsOf),
            PlanFigures.Free(positionsOf),
            bookingsOf,
            bookingsMissing);
    }

    /// <summary>
    /// Die Quote je Person (Spec 27): dieselbe Rechnung wie die des Haushalts, einzeln über den Monat nur dieser
    /// Person; Ziel ist für alle der Wert des Haushalts, die Abweichung Ist minus Ziel.
    /// </summary>
    private static List<HouseholdMemberResponse> MemberQuotas(
        Household household, IReadOnlyCollection<MemberMonth> months, IReadOnlyDictionary<Guid, string> names) =>
        months.Select(month =>
        {
            var figures = HouseholdPlanFigures.Compute([month]);
            var quotas = QuotaPots.Select(pot =>
            {
                var target = household.Quotas.For(pot)!.Value;
                return new HouseholdMemberQuotaResponse(
                    PotKey.ToKey(pot), target, figures.QuotaActual[pot], decimal.Round(figures.QuotaActual[pot] - target, 1, MidpointRounding.AwayFromZero));
            }).ToList();
            return new HouseholdMemberResponse(month.Plan.OwnerId, names[month.Plan.OwnerId], figures.QuotaIncome, figures.QuotaIncome > 0m, quotas);
        }).ToList();

    /// <summary>Die Monate mit einem Plan eines Mitglieds, das in diesem Monat zählt, der neueste zuerst (R7).</summary>
    public async Task<List<HouseholdPlanMonth>> MonthsAsync(Household household, CancellationToken cancellationToken)
    {
        using var scope = db.Owners.Bypass("Haushaltsplan zusammensetzen");

        var ids = household.Members.Select(m => m.UserId).ToList();
        var rows = await db.Plans
            .AsNoTracking()
            .Include(p => p.Positions)
            .Where(p => ids.Contains(p.OwnerId))
            .ToListAsync(cancellationToken);
        var members = household.Members.ToDictionary(m => m.UserId);
        return rows
            .Where(r => IsCounted(members[r.OwnerId], r.Year, r.Month))
            .GroupBy(r => (r.Year, r.Month))
            .Select(g => new HouseholdPlanMonth(g.Key.Year, g.Key.Month, PlanFigures.Free(g.SelectMany(p => p.Positions))))
            .OrderByDescending(m => m.Year)
            .ThenByDescending(m => m.Month)
            .ToList();
    }

    /// <summary>
    /// Die offenen Konten der gezählten Mitglieder mit ihrem Stand am letzten Tag des Monats, aber nur von denen,
    /// die sie dem Betrachter mindestens zum Ansehen freigegeben haben (R8); die eigenen immer. Die Übrigen stehen
    /// mit Vornamen in der zweiten Liste, damit keine Summe still ohne eine Person bleibt.
    /// </summary>
    private async Task<(List<HouseholdAccountResponse> Accounts, List<string> Missing)> AccountsAsync(
        IReadOnlyCollection<Guid> counted, IReadOnlyDictionary<Guid, string> names, int year, int month, CancellationToken cancellationToken)
    {
        var lastDay = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var shown = new List<HouseholdAccountResponse>();
        var missing = new List<string>();
        foreach (var ownerId in counted)
        {
            if (!(await access.LevelAsync(ownerId, Area.Accounts, cancellationToken)).Includes(AccessLevel.View))
            {
                missing.Add(names[ownerId]);
                continue;
            }

            var accounts = await db.Accounts
                .AsNoTracking()
                .Where(a => a.OwnerId == ownerId && a.Active && a.OpeningDate <= lastDay)
                .OrderByDescending(a => a.IsDefault)
                .ThenBy(a => a.Name)
                .ToListAsync(cancellationToken);
            var bookings = await BookingEndpoints.WithLines(db)
                .AsNoTracking()
                .Where(b => b.OwnerId == ownerId && b.PaymentDate <= lastDay)
                .ToListAsync(cancellationToken);
            foreach (var account in accounts)
            {
                account.Bookings = bookings.Where(b => b.AccountId == account.Id || b.CounterAccountId == account.Id).ToList();
                shown.Add(new HouseholdAccountResponse(account.Id, ownerId, names[ownerId], account.Name, account.IsDefault, account.Balance(lastDay)));
            }
        }
        missing.Sort(StringComparer.CurrentCultureIgnoreCase);
        return (shown, missing);
    }

    private static int PotOrder(string pot) => pot switch
    {
        PotKeys.Income => 0,
        PotKeys.Needs => 1,
        PotKeys.Wants => 2,
        _ => 3,
    };

    private static HouseholdPlanPositionResponse ToResponse(PlanPosition position, Guid ownerId, string ownerFirstName, decimal booked) => new(
        position.Id,
        position.Label,
        position.AmountPlanned,
        position.DueDay,
        CommitmentTypeKey.ToKey(position.Type),
        position.CategoryId,
        PotKey.ToKey(position.Pot),
        position.PassThrough,
        booked,
        position.DoneAt,
        ownerId,
        ownerFirstName);
}
