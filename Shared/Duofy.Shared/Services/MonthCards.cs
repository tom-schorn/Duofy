using Duofy.Schemas;

namespace Duofy.Shared.Services;

/// <summary>
/// Ein Topf auf einer Monatskarte (Issue #94, R7): Ziel des Balkens ist die Summe der geplanten Posten des Topfs, gefüllt wird mit
/// dem Gebuchten. Keine Quote und kein Richtwert.
/// </summary>
public sealed record MonthCardPot(string Pot, decimal Planned, decimal Booked)
{
    /// <summary>Mehr gebucht, als geplant ist; dann endet der Balken rot und der Text sagt "überzogen".</summary>
    public bool Over => Booked > Planned;

    /// <summary>Wie weit der Balken reicht, in Prozent der geplanten Summe, höchstens 100.</summary>
    public decimal Percent => Planned <= 0m ? (Booked > 0m ? 100m : 0m) : Math.Min(Booked / Planned * 100m, 100m);
}

/// <summary>Was eine Monatskarte der Übersicht zeigt (Spec 82): Verplanbar, Anstehend und die drei Topf-Balken.</summary>
public sealed record MonthCard(decimal Free, decimal Unpaid, IReadOnlyList<MonthCardPot> Pots);

/// <summary>
/// Setzt die Monatskarten aus den Antworten der API zusammen. Verplanbar ist derselbe Wert der API, der früher "Frei" hieß;
/// Anstehend ergibt sich aus den nicht abgehakten Posten. Geplant und Gebucht je Topf kommen fertig von der API.
/// </summary>
public static class MonthCards
{
    private static readonly string[] PotsInOrder = [PotKeys.Needs, PotKeys.Wants, PotKeys.Savings];

    /// <summary>Eine Karte aus der Übersicht mit Zahlen (GET /api/v1/plans/overview).</summary>
    public static MonthCard ForOverview(PlanOverviewMonthResponse month) =>
        new(month.Free, month.Unpaid, Pots(key => month.Pots.FirstOrDefault(p => p.Pot == key) is { } p ? (p.Planned, p.Booked) : (0m, 0m)));

    /// <summary>Eine Karte aus dem Monat selbst, für Server ohne Übersicht (ältere Version): dieselben Zahlen, einzeln geholt.</summary>
    public static MonthCard ForPerson(PlanResponse plan) => new(
        plan.Overview?.Free ?? 0m,
        MonthView.Unpaid(plan.Positions),
        Pots(key => plan.Overview?.Pots.FirstOrDefault(p => p.Pot == key) is { } p ? (p.Planned, p.Booked) : (0m, 0m)));

    public static MonthCard ForHousehold(HouseholdPlanResponse plan) => new(
        plan.Free,
        plan.Unpaid,
        Pots(key => plan.Pots.FirstOrDefault(p => p.Pot == key) is { } p ? (p.Planned, p.Booked) : (0m, 0m)));

    private static List<MonthCardPot> Pots(Func<string, (decimal Planned, decimal Booked)> figures) =>
        PotsInOrder.Select(key => new MonthCardPot(key, figures(key).Planned, figures(key).Booked)).ToList();
}
