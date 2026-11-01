using Duofy.Schemas;

namespace Duofy.Shared.Services;

/// <summary>Ein Topf der Übersicht: Quote (Ziel und Ist in Prozent) und geplant gegen gebucht in Euro.</summary>
public sealed record MonthPotRow(string Pot, int Target, decimal Actual, decimal Planned, decimal Booked)
{
    /// <summary>Die Lücke: gebucht minus geplant; positiv heißt mehr als geplant, negativ weniger.</summary>
    public decimal Gap => Booked - Planned;
}

/// <summary>Eine Buchung der Übersicht oder des Tabs Buchungen; <paramref name="OtherMonth"/> heißt: ihr Datum liegt nicht im Planmonat.</summary>
public sealed record MonthBookingRow(Guid Id, DateOnly Date, decimal Amount, string? Detail, bool OtherMonth);

/// <summary>
/// Ein Übertrag als eigene Zeile des Monats (Spec 66, R9), keine Buchung: <paramref name="Incoming"/> heißt "Übertrag aus ...",
/// sonst "Übertrag nach ..."; <paramref name="Month"/> ist der genannte Monat, <paramref name="Owner"/> im Haushalt der Vorname.
/// </summary>
public sealed record MonthCarryOverRow(bool Incoming, DateOnly Month, decimal Amount, string? Owner);

/// <summary>Eine anstehende oder überfällige Zahlung; <paramref name="Open"/> ist der Rest, der noch nicht gebucht ist.</summary>
public sealed record MonthDueRow(string Label, DateOnly Due, decimal Open, bool Overdue, string? Owner);

/// <summary>Alles, was die Übersicht eines Monats zeigt, gleich für eine Person und für den Haushalt.</summary>
public sealed record MonthOverviewModel(
    decimal Income,
    decimal Fixed,
    decimal Free,
    IReadOnlyList<MonthPotRow> Pots,
    IReadOnlyList<MonthBookingRow> Bookings,
    IReadOnlyList<MonthDueRow> Due,
    IReadOnlyList<MonthCarryOverRow> CarryOvers);

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

    private sealed record DueSource(string Label, int DueDay, string Pot, bool PassThrough, decimal Planned, decimal Booked, DateTimeOffset? DoneAt, string? Owner);

    public static MonthOverviewModel ForPerson(PlanResponse plan, DateOnly today, Func<Guid, string?> accountName)
    {
        var overview = plan.Overview;
        var pots = PotKeysInOrder.Select(key => Pot(key, plan.Quotas, overview?.Pots.FirstOrDefault(p => p.Pot == key))).ToList();
        var bookings = BookingRows(overview?.Bookings ?? [], accountName);
        var due = DueRows(
            plan.Positions.Select(p => new DueSource(p.Label, p.DueDay, p.Pot, p.PassThrough, p.AmountPlanned, p.Booked, p.DoneAt, null)),
            plan.Year, plan.Month, today);
        return new MonthOverviewModel(overview?.Income ?? 0m, overview?.Fixed ?? 0m, overview?.Free ?? 0m, pots, bookings.Take(RecentCount).ToList(), due, CarryOverRows(overview?.CarryOvers));
    }

    public static MonthOverviewModel ForHousehold(HouseholdPlanResponse plan, DateOnly today)
    {
        var pots = PotKeysInOrder
            .Select(key => Pot(key, plan.Quotas, plan.Pots.Where(p => p.Pot == key).Select(p => new PlanPotResponse(p.Pot, p.Planned, p.Booked)).FirstOrDefault()))
            .ToList();
        var bookings = HouseholdBookingRows(plan);
        var due = DueRows(
            plan.Positions.Select(p => new DueSource(p.Label, p.DueDay, p.Pot, p.PassThrough, p.AmountPlanned, p.Booked, p.DoneAt, p.OwnerFirstName)),
            plan.Year, plan.Month, today);
        return new MonthOverviewModel(plan.Income, plan.Fixed, plan.Free, pots, bookings.Take(RecentCount).ToList(), due, CarryOverRows(plan.CarryOvers));
    }

    /// <summary>Die Buchungen eines Planmonats für die Liste, in der Reihenfolge der API (neueste zuerst); <paramref name="accountName"/> gibt den Namen des Kontos.</summary>
    public static IReadOnlyList<MonthBookingRow> BookingRows(IEnumerable<BookingResponse> bookings, Func<Guid, string?> accountName) =>
        bookings
            .Select(b => new MonthBookingRow(b.Id, b.Date, b.Amount, accountName(b.AccountId), OtherMonth(b.Date, b.PlanYear, b.PlanMonth)))
            .ToList();

    /// <summary>Die Buchungen des Haushalts mit dem Vornamen der Person als Zusatz.</summary>
    public static IReadOnlyList<MonthBookingRow> HouseholdBookingRows(HouseholdPlanResponse plan) =>
        (plan.Bookings ?? [])
            .Select(b => new MonthBookingRow(b.Id, b.Date, b.Amount, b.OwnerFirstName, OtherMonth(b.Date, b.PlanYear, b.PlanMonth)))
            .ToList();

    /// <summary>Die Überträge des Monats für die Zeilen über den Buchungen; sie zählen nicht zu den fünf neuesten Buchungen.</summary>
    public static IReadOnlyList<MonthCarryOverRow> CarryOverRows(IEnumerable<PlanCarryOverLineResponse>? lines) =>
        (lines ?? [])
            .Select(l => new MonthCarryOverRow(l.Direction == CarryOverDirections.In, new DateOnly(l.Year, l.Month, 1), l.Amount, l.OwnerFirstName))
            .ToList();

    private static bool OtherMonth(DateOnly date, int planYear, int planMonth) =>
        planYear != 0 && (date.Year, date.Month) != (planYear, planMonth);

    private static MonthPotRow Pot(string key, IReadOnlyList<PlanQuotaResponse> quotas, PlanPotResponse? totals)
    {
        var quota = quotas.FirstOrDefault(q => q.Pot == key);
        return new MonthPotRow(key, quota?.Target ?? 0, quota?.Actual ?? 0m, totals?.Planned ?? 0m, totals?.Booked ?? 0m);
    }

    // Anstehend: verplant, aber nicht abgehakt oder bezahlt, ohne Einnahmen und durchlaufende Posten (Spec 23, Begriffe).
    // Überfällig heißt: der Fälligkeitstag liegt vor heute; anstehend: heute bis in UpcomingDays Tagen.
    private static IReadOnlyList<MonthDueRow> DueRows(IEnumerable<DueSource> positions, int year, int month, DateOnly today)
    {
        var rows = new List<MonthDueRow>();
        foreach (var p in positions)
        {
            var open = Math.Max(p.Planned - p.Booked, 0m);
            if (p.Pot == PotKeys.Income || p.PassThrough || p.DoneAt is not null || open == 0m)
            {
                continue;
            }
            var due = new DateOnly(year, month, Math.Min(p.DueDay, DateTime.DaysInMonth(year, month)));
            var overdue = due < today;
            if (overdue || due <= today.AddDays(UpcomingDays))
            {
                rows.Add(new MonthDueRow(p.Label, due, open, overdue, p.Owner));
            }
        }
        return rows.OrderByDescending(r => r.Overdue).ThenBy(r => r.Due).ThenBy(r => r.Label, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
