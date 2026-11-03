using Duofy.Schemas;

namespace Duofy.Shared.Services;

/// <summary>Der Stand am Ende eines Tages; <paramref name="Forecast"/> heißt: ab morgen, aus den offenen Posten gerechnet.</summary>
public sealed record CurvePoint(DateOnly Day, decimal Balance, bool Forecast);

/// <summary>Eine echte Buchung für die Kurve: ihr Datum und ihr Betrag mit Vorzeichen (Einnahme plus, Ausgabe minus).</summary>
public sealed record CurveMove(DateOnly Date, decimal Signed);

/// <summary>Was von einem offenen Posten noch kommt oder noch abgeht, mit Vorzeichen, an seinem Fälligkeitstag.</summary>
/// <param name="Income">Ein Einnahmen-Posten: Er zählt am 1. des Monats, nicht an seinem Tag (Tom, 03.11.2026).</param>
public sealed record CurveOpen(int DueDay, decimal Signed, bool Income = false);

/// <summary>
/// Die Tageskurve eines Monats (Spec 82, R14): der Stand zu Beginn, der Stand an jedem Tag, der erste Tag unter null und die
/// tiefste Stelle. <see cref="Gap"/> ist der Betrag der Lücke, 0 ohne Lücke.
/// </summary>
public sealed record MonthCurveModel(decimal Start, IReadOnlyList<CurvePoint> Points, DateOnly? FirstBelowZero, CurvePoint Lowest)
{
    public decimal Gap => Lowest.Balance < 0m ? -Lowest.Balance : 0m;
}

/// <summary>
/// Rechnet die Tageskurve aus dem, was die API liefert; die API hat dafür keinen eigenen Verlauf (Spec 82: keine Backend-Änderung).
///
/// Regeln (Spec 82, R14, mit der Korrektur vom 03.11.2026):
/// - Die Kurve beginnt mit dem Übertrag in diesen Monat und den Buchungen, die per Planzuordnung schon zu diesem Monat gehören,
///   aber vor seinem ersten Tag datiert sind, etwa das Gehalt vom 28. des Vormonats.
/// - Jede Buchung des Planmonats zählt an ihrem Tag; eine, die erst nach dem Monat datiert ist, am letzten Tag.
///   Umbuchungen zwischen eigenen Konten ändern den Stand nicht.
/// - Ab morgen kommen die offenen Ausgabe-Posten an ihrem Fälligkeitstag dazu, mit dem Rest, der noch nicht gebucht ist. Ein
///   offener Posten, dessen Tag schon vorbei ist, zählt morgen: Er ist noch zu bezahlen. Durchlaufende und abgehakte Posten
///   zählen nicht.
/// - Einnahmen-Posten, die noch nicht (voll) gebucht sind, zählen mit ihrem Rest am 1. des Monats, egal welcher Tag (Tom,
///   03.11.2026: "Am 1. des Monats"). Ein Posten kennt nur den Tag; nach der Planzuordnung kommt etwa der Lohn vom 29.10. für
///   den November, und am 29.11. gerechnet zeigte die Kurve eine Lücke, die es nicht gibt.
/// - Liegt der ganze Monat in der Vergangenheit, zählen nur die Buchungen.
/// </summary>
public static class MonthCurve
{
    public static MonthCurveModel Build(int year, int month, DateOnly today, decimal carryIn, IEnumerable<CurveMove> bookings, IEnumerable<CurveOpen> open)
    {
        var first = new DateOnly(year, month, 1);
        var days = DateTime.DaysInMonth(year, month);
        var last = first.AddDays(days - 1);
        var tomorrow = today.AddDays(1);

        var moves = bookings.ToList();
        var start = carryIn + moves.Where(m => m.Date < first).Sum(m => m.Signed);

        var perDay = new decimal[days + 1];
        foreach (var move in moves.Where(m => m.Date >= first))
        {
            perDay[move.Date > last ? days : move.Date.Day] += move.Signed;
        }
        var past = last < today;
        foreach (var item in open.Where(o => o.Income && !past))
        {
            perDay[1] += item.Signed;
        }
        foreach (var item in open.Where(o => !o.Income))
        {
            var due = new DateOnly(year, month, Math.Clamp(item.DueDay, 1, days));
            var day = due <= today ? tomorrow : due;
            if (day >= first && day <= last && day > today)
            {
                perDay[day.Day] += item.Signed;
            }
        }

        var points = new List<CurvePoint>(days);
        var balance = start;
        DateOnly? firstBelow = null;
        for (var d = 1; d <= days; d++)
        {
            balance += perDay[d];
            var date = new DateOnly(year, month, d);
            points.Add(new CurvePoint(date, balance, date > today));
            if (balance < 0m && firstBelow is null)
            {
                firstBelow = date;
            }
        }

        var lowest = points.OrderBy(p => p.Balance).ThenBy(p => p.Day).First();
        return new MonthCurveModel(start, points, firstBelow, lowest);
    }

    /// <summary>Die Kurve eines eigenen Monats aus der Antwort der API.</summary>
    public static MonthCurveModel ForPerson(PlanResponse plan, DateOnly today) => Build(
        plan.Year, plan.Month, today,
        CarryIn(plan.Overview?.CarryOvers),
        (plan.Overview?.Bookings ?? []).Select(b => new CurveMove(b.Date, Signed(b.Kind, b.Amount))),
        plan.Positions
            .Where(p => !p.PassThrough && p.DoneAt is null && p.AmountPlanned - p.Booked > 0m)
            .Select(p => new CurveOpen(p.DueDay, (p.Pot == PotKeys.Income ? 1 : -1) * (p.AmountPlanned - p.Booked), p.Pot == PotKeys.Income)));

    /// <summary>Die Kurve des Haushaltsplans: Buchungen nur von denen, die sie freigegeben haben.</summary>
    public static MonthCurveModel ForHousehold(HouseholdPlanResponse plan, DateOnly today) => Build(
        plan.Year, plan.Month, today,
        CarryIn(plan.CarryOvers),
        (plan.Bookings ?? []).Select(b => new CurveMove(b.Date, Signed(b.Kind, b.Amount))),
        plan.Positions
            .Where(p => !p.PassThrough && p.DoneAt is null && p.AmountPlanned - p.Booked > 0m)
            .Select(p => new CurveOpen(p.DueDay, (p.Pot == PotKeys.Income ? 1 : -1) * (p.AmountPlanned - p.Booked), p.Pot == PotKeys.Income)));

    private static decimal CarryIn(IEnumerable<PlanCarryOverLineResponse>? lines) =>
        (lines ?? []).Where(l => l.Direction == CarryOverDirections.In).Sum(l => l.Amount);

    private static decimal Signed(string kind, decimal amount) => kind switch
    {
        BookingKinds.Income => amount,
        BookingKinds.Expense => -amount,
        _ => 0m,
    };
}
