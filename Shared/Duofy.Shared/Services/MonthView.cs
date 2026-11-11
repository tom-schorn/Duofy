using Duofy.Schemas;

namespace Duofy.Shared.Services;

/// <summary>Ein Topf der Übersicht: Quote (Ziel und Ist in Prozent) und geplant gegen gebucht in Euro.</summary>
/// <param name="OnPlannedIncome">True, wenn das Ist mit den geplanten Einnahmen gerechnet ist, weil noch keine gebucht sind (Spec 82, R17).</param>
public sealed record MonthPotRow(string Pot, int Target, decimal Actual, decimal Planned, decimal Booked, bool OnPlannedIncome = false)
{
    /// <summary>Die Lücke: gebucht minus geplant; positiv heißt mehr als geplant, negativ weniger.</summary>
    public decimal Gap => Booked - Planned;
}

/// <summary>Eine Buchung der Übersicht oder des Tabs Buchungen; <paramref name="OtherMonth"/> heißt: ihr Datum liegt nicht im Planmonat.</summary>
/// <param name="Kind">Die Art (<see cref="BookingKinds"/>); sie steht farbig und als Wort in der Zeile (Spec 51, R9).</param>
/// <param name="Purpose">Wofür die Buchung war (Spec 51, R10); null, wenn sich das nicht sagen lässt.</param>
/// <param name="Name">Wie die Buchung heißt (Spec 82, R15): die Notiz, sonst die Kategorie der ersten Zeile; null heißt: das Wort der Art.</param>
/// <param name="Positions">Die Posten, auf die sie gebucht ist; leer heißt ungeplant.</param>
/// <param name="Pots">Die Töpfe ihrer Zeilen (<see cref="PotKeys"/>), ohne Wiederholung.</param>
public sealed record MonthBookingRow(Guid Id, DateOnly Date, decimal Amount, string? Detail, bool OtherMonth, string Kind = BookingKinds.Expense, string? Purpose = null,
    string? Name = null, IReadOnlyList<string>? Positions = null, IReadOnlyList<string>? Pots = null);

/// <summary>
/// Ein Übertrag als eigene Zeile des Monats (Spec 66, R9), keine Buchung: <paramref name="Incoming"/> heißt "Übertrag aus ...",
/// sonst "Übertrag nach ..."; <paramref name="Month"/> ist der genannte Monat, <paramref name="Owner"/> im Haushalt der Vorname.
/// </summary>
public sealed record MonthCarryOverRow(bool Incoming, DateOnly Month, decimal Amount, string? Owner);

/// <summary>Eine anstehende oder überfällige Zahlung; <paramref name="Open"/> ist der Rest, der noch nicht gebucht ist.</summary>
public sealed record MonthDueRow(string Label, DateOnly Due, decimal Open, bool Overdue, string? Owner);

/// <summary>Eine Kategorie der Auswertung: geplant und gebucht; <paramref name="Others"/> fasst die kleineren zusammen.</summary>
/// <param name="Name">Der Name der Kategorie; null, wenn er nicht bekannt ist.</param>
public sealed record MonthCategoryRow(string? Name, decimal Planned, decimal Booked, bool Others = false);

/// <summary>Alles, was die Übersicht eines Monats zeigt, gleich für eine Person und für den Haushalt.</summary>
/// <param name="Categories">Die Kategorien der Ausgaben für das runde Diagramm (Spec 82, R14); leer, wo die Namen fehlen.</param>
/// <param name="Curve">Die Tageskurve des Monats (Spec 82, R14).</param>
public sealed record MonthOverviewModel(
    decimal Income,
    decimal Fixed,
    decimal Free,
    IReadOnlyList<MonthPotRow> Pots,
    IReadOnlyList<MonthBookingRow> Bookings,
    IReadOnlyList<MonthDueRow> Due,
    IReadOnlyList<MonthCarryOverRow> CarryOvers,
    IReadOnlyList<MonthCategoryRow>? Categories = null,
    MonthCurveModel? Curve = null);

/// <summary>
/// Setzt aus den Antworten der API die Übersicht des Monats zusammen (Spec 24). Gerechnet wird hier nichts Neues:
/// Einnahmen, Festes, Frei und die Töpfe schickt die API (PlanFigures); die Seite wählt aus (die fünf neuesten
/// Buchungen, was bald fällig oder überfällig ist) und bildet die Lücke aus zwei gelieferten Zahlen.
/// </summary>
public static class MonthView
{
    /// <summary>Wie viele Tage im Voraus eine Zahlung als anstehend gilt.</summary>
    public const int UpcomingDays = 7;

    /// <summary>Wie viele Buchungen die Übersicht zeigt.</summary>
    public const int RecentCount = 5;

    private static readonly string[] PotKeysInOrder = [PotKeys.Needs, PotKeys.Wants, PotKeys.Savings];

    private sealed record DueSource(string Label, int DueDay, string Pot, bool PassThrough, decimal Planned, decimal Booked, DateTimeOffset? DoneAt, string? Owner, int PostponeCount = 0);

    /// <summary>Was ein Posten zum Ist der Quote beiträgt: sein Topf, ob er durchläuft, und was auf ihm gebucht ist.</summary>
    public sealed record QuotaSource(string Pot, bool PassThrough, decimal Booked);

    /// <summary>
    /// Das Ist der Quote eines Topfs (Spec 82, R17): Die API teilt die gebuchten Ausgaben der Posten durch die gebuchten
    /// Einnahmen und liefert 0, solange keine Einnahme gebucht ist. Dann rechnet die Oberfläche mit denselben Regeln gegen die
    /// geplanten Einnahmen (ohne durchlaufende Posten), damit "Ist" nicht überall 0 % zeigt. Sonst gilt die Zahl der API.
    /// </summary>
    /// <param name="potBooked">
    /// Der gebuchte Betrag des Topfs, wie ihn die API liefert; in ihm steckt schon, was Umbuchungen zwischen Töpfen verschieben
    /// (Spec 112, R2). Ohne ihn zählen die Posten allein. Die Verschiebung rechnet nur die API, nie die Oberfläche.
    /// </param>
    public static (decimal Actual, bool OnPlannedIncome) QuotaActual(
        string pot, decimal apiActual, bool incomeBooked, decimal plannedIncome, IEnumerable<QuotaSource> positions, decimal? potBooked = null)
    {
        if (incomeBooked || plannedIncome <= 0m)
        {
            return (apiActual, false);
        }
        var spent = potBooked ?? positions.Where(p => p.Pot == pot && !p.PassThrough).Sum(p => p.Booked);
        return (decimal.Round(spent / plannedIncome * 100m, 1, MidpointRounding.AwayFromZero), true);
    }

    /// <summary>Wie viele Kategorien das runde Diagramm einzeln zeigt; die übrigen stehen zusammen als "Weitere".</summary>
    public const int CategoryCount = 5;

    /// <summary>
    /// Die Kategorien der Ausgaben eines Monats (Spec 82, R14): geplant ist die Summe der Posten (ohne Einnahmen und durchlaufende
    /// Posten), gebucht die Buchungen auf diesen Posten und, was ohne Posten in der Kategorie gebucht ist. Die größten
    /// <see cref="CategoryCount"/> stehen einzeln, der Rest als "Weitere".
    /// </summary>
    public static IReadOnlyList<MonthCategoryRow> Categories(PlanResponse plan, Func<Guid, string?> categoryName)
    {
        var sums = new Dictionary<Guid, (decimal Planned, decimal Booked)>();
        foreach (var p in plan.Positions.Where(p => p.Pot != PotKeys.Income && !p.PassThrough))
        {
            var (planned, booked) = sums.GetValueOrDefault(p.CategoryId);
            sums[p.CategoryId] = (planned + p.AmountPlanned, booked + p.Booked);
        }
        foreach (var line in (plan.Overview?.Bookings ?? []).Where(b => b.Kind == BookingKinds.Expense).SelectMany(b => b.Lines)
                     .Where(l => l.PositionId is null && l.Pot != PotKeys.Income && l.CategoryId is not null))
        {
            var (planned, booked) = sums.GetValueOrDefault(line.CategoryId!.Value);
            sums[line.CategoryId.Value] = (planned, booked + line.Amount);
        }
        var ordered = sums
            .Where(s => s.Value.Planned != 0m || s.Value.Booked != 0m)
            .OrderByDescending(s => Math.Max(s.Value.Planned, s.Value.Booked))
            .ToList();
        var rows = ordered.Take(CategoryCount).Select(s => new MonthCategoryRow(categoryName(s.Key), s.Value.Planned, s.Value.Booked)).ToList();
        var rest = ordered.Skip(CategoryCount).ToList();
        if (rest.Count > 0)
        {
            rows.Add(new MonthCategoryRow(null, rest.Sum(s => s.Value.Planned), rest.Sum(s => s.Value.Booked), Others: true));
        }
        return rows;
    }

    public static MonthOverviewModel ForPerson(PlanResponse plan, DateOnly today, Func<Guid, string?> accountName, Func<BookingResponse, string?>? purpose = null,
        Func<Guid, string?>? categoryName = null)
    {
        var overview = plan.Overview;
        var sources = plan.Positions.Select(p => new QuotaSource(p.Pot, p.PassThrough, p.Booked)).ToList();
        var plannedIncome = overview?.Income ?? plan.PlannedIncome;
        var pots = PotKeysInOrder
            .Select(key => Pot(key, plan.Quotas, overview?.Pots.FirstOrDefault(p => p.Pot == key), plan.BookedIncome != 0m, plannedIncome, sources, PlanView.Unplanned(plan, key)))
            .ToList();
        var bookings = BookingRows(overview?.Bookings ?? [], accountName, purpose);
        var due = DueRows(
            plan.Positions.Select(p => new DueSource(p.Label, p.DueDay, p.Pot, p.PassThrough, p.AmountPlanned, p.Booked, p.DoneAt, null, p.PostponeCount)),
            plan.Year, plan.Month, today);
        return new MonthOverviewModel(overview?.Income ?? 0m, overview?.Fixed ?? 0m, overview?.Free ?? 0m, pots, bookings.Take(RecentCount).ToList(), due, CarryOverRows(overview?.CarryOvers),
            categoryName is null ? [] : Categories(plan, categoryName), MonthCurve.ForPerson(plan, today));
    }

    public static MonthOverviewModel ForHousehold(HouseholdPlanResponse plan, DateOnly today)
    {
        var sources = plan.Positions.Select(p => new QuotaSource(p.Pot, p.PassThrough, p.Booked)).ToList();
        var incomeBooked = (plan.Bookings ?? []).Any(b => b.Kind == BookingKinds.Income) || plan.Unplanned.Income != 0m;
        var pots = PotKeysInOrder
            .Select(key => Pot(key, plan.Quotas, plan.Pots.Where(p => p.Pot == key).Select(p => new PlanPotResponse(p.Pot, p.Planned, p.Booked)).FirstOrDefault(),
                incomeBooked, plan.Income, sources, HouseholdUnplanned(plan.Unplanned, key)))
            .ToList();
        var bookings = HouseholdBookingRows(plan);
        var due = DueRows(
            plan.Positions.Select(p => new DueSource(p.Label, p.DueDay, p.Pot, p.PassThrough, p.AmountPlanned, p.Booked, p.DoneAt, p.OwnerFirstName, p.PostponeCount)),
            plan.Year, plan.Month, today);
        return new MonthOverviewModel(plan.Income, plan.Fixed, plan.Free, pots, bookings.Take(RecentCount).ToList(), due, CarryOverRows(plan.CarryOvers),
            [], MonthCurve.ForHousehold(plan, today));
    }

    /// <summary>
    /// Anstehend: was von den nicht abgehakten Posten noch offen ist, ohne Einnahmen und durchlaufende Posten (Spec 82).
    /// </summary>
    public static decimal Unpaid(IEnumerable<PlanPositionResponse> positions) => positions
        .Where(p => p.Pot != PotKeys.Income && !p.PassThrough && p.DoneAt is null)
        .Sum(p => Math.Max(p.AmountPlanned - p.Booked, 0m));

    /// <summary>Die Buchungen eines Planmonats für die Liste, in der Reihenfolge der API (neueste zuerst); <paramref name="accountName"/> gibt den Namen des Kontos.</summary>
    public static IReadOnlyList<MonthBookingRow> BookingRows(IEnumerable<BookingResponse> bookings, Func<Guid, string?> accountName, Func<BookingResponse, string?>? purpose = null) =>
        BookingRows(bookings, accountName, purpose, [], _ => null);

    /// <summary>
    /// Die Buchungen für die Tabelle des Reiters Buchungen (Spec 82, R15): je Buchung Name, Posten, Konto und Topf. Eine Umbuchung
    /// nennt als Konto beide Konten.
    /// </summary>
    public static IReadOnlyList<MonthBookingRow> BookingRows(
        IEnumerable<BookingResponse> bookings,
        Func<Guid, string?> accountName,
        Func<BookingResponse, string?>? purpose,
        IReadOnlyList<PlanPositionResponse> positions,
        Func<Guid, string?> categoryName) =>
        bookings
            .Select(b => new MonthBookingRow(
                b.Id, b.Date, b.Amount,
                b.Kind == BookingKinds.Transfer && b.CounterAccountId is { } to
                    ? $"{accountName(b.AccountId)} → {accountName(to)}"
                    : accountName(b.AccountId),
                OtherMonth(b.Date, b.PlanYear, b.PlanMonth), b.Kind, purpose?.Invoke(b),
                b.Note ?? (b.Lines.FirstOrDefault()?.CategoryId is { } category ? categoryName(category) : null),
                b.Lines.Select(l => l.PositionId is { } id ? positions.FirstOrDefault(p => p.Id == id)?.Label : null).OfType<string>().Distinct().ToList(),
                (b.Lines.Count > 0 ? b.Lines.Select(l => l.Pot) : b.TargetPot is { } target ? [target] : []).Distinct().ToList()))
            .ToList();

    /// <summary>Die Buchungen des Haushalts mit dem Vornamen der Person als Zusatz.</summary>
    public static IReadOnlyList<MonthBookingRow> HouseholdBookingRows(HouseholdPlanResponse plan) =>
        (plan.Bookings ?? [])
            .Select(b =>
            {
                var lines = b.Lines ?? [];
                var positions = lines.Select(l => l.PositionId is { } id ? plan.Positions.FirstOrDefault(p => p.Id == id) : null).OfType<HouseholdPlanPositionResponse>().ToList();
                return new MonthBookingRow(b.Id, b.Date, b.Amount, b.OwnerFirstName, OtherMonth(b.Date, b.PlanYear, b.PlanMonth), b.Kind, b.Purpose,
                    lines.FirstOrDefault()?.Category ?? b.Purpose,
                    positions.Select(p => p.Label).Distinct().ToList(),
                    positions.Select(p => p.Pot).Distinct().ToList());
            })
            .ToList();

    /// <summary>Die Überträge des Monats für die Zeilen über den Buchungen; sie zählen nicht zu den fünf neuesten Buchungen.</summary>
    public static IReadOnlyList<MonthCarryOverRow> CarryOverRows(IEnumerable<PlanCarryOverLineResponse>? lines) =>
        (lines ?? [])
            .Select(l => new MonthCarryOverRow(l.Direction == CarryOverDirections.In, new DateOnly(l.Year, l.Month, 1), l.Amount, l.OwnerFirstName))
            .ToList();

    private static bool OtherMonth(DateOnly date, int planYear, int planMonth) =>
        planYear != 0 && (date.Year, date.Month) != (planYear, planMonth);

    // Das Ist einer Quote zählt die Posten, nicht das Ungeplante (Spec 72, R5); der gebuchte Betrag des Topfs enthält es, also geht es hier ab.
    private static decimal HouseholdUnplanned(HouseholdUnplannedResponse unplanned, string pot) => pot switch
    {
        PotKeys.Needs => unplanned.Needs,
        PotKeys.Wants => unplanned.Wants,
        PotKeys.Savings => unplanned.Savings,
        _ => 0m,
    };

    private static MonthPotRow Pot(string key, IReadOnlyList<PlanQuotaResponse> quotas, PlanPotResponse? totals,
        bool incomeBooked, decimal plannedIncome, IReadOnlyList<QuotaSource> sources, decimal unplanned)
    {
        var quota = quotas.FirstOrDefault(q => q.Pot == key);
        var (actual, onPlanned) = QuotaActual(key, quota?.Actual ?? 0m, incomeBooked, plannedIncome, sources,
            totals is null ? null : totals.Booked - unplanned);
        return new MonthPotRow(key, quota?.Target ?? 0, actual, totals?.Planned ?? 0m, totals?.Booked ?? 0m, onPlanned);
    }

    // Anstehend: verplant, aber nicht abgehakt oder bezahlt, ohne Einnahmen und durchlaufende Posten (Spec 23, Begriffe).
    // Überfällig heißt: der Fälligkeitstag liegt vor heute oder der Posten wurde dreimal verschoben; anstehend: heute bis in UpcomingDays Tagen.
    private static IReadOnlyList<MonthDueRow> DueRows(IEnumerable<DueSource> positions, int year, int month, DateOnly today)
    {
        var rows = new List<MonthDueRow>();
        foreach (var p in positions)
        {
            var open = Math.Max(p.Planned - p.Booked, 0m);
            if (!PositionOpen.Is(p.Pot == PotKeys.Income, p.PassThrough, p.DoneAt is not null, p.Planned, p.Booked))
            {
                continue;
            }
            var due = new DateOnly(year, month, Math.Min(p.DueDay, DateTime.DaysInMonth(year, month)));
            // Überfällig: der Tag ist vorbei oder die Erinnerung wurde dreimal verschoben (Spec 118, R6).
            var overdue = due < today || p.PostponeCount >= ReminderLimits.MaxPostpones;
            if (overdue || due <= today.AddDays(UpcomingDays))
            {
                rows.Add(new MonthDueRow(p.Label, due, open, overdue, p.Owner));
            }
        }
        return rows.OrderByDescending(r => r.Overdue).ThenBy(r => r.Due).ThenBy(r => r.Label, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
