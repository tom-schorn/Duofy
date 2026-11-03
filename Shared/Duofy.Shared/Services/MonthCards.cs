using Duofy.Schemas;

namespace Duofy.Shared.Services;

/// <summary>Ein Topf auf einer Monatskarte: Quote, was verplant ist und wie viel die Quote bei diesen Einnahmen erlaubt.</summary>
public sealed record MonthCardPot(string Pot, int Target, decimal Planned, decimal Limit)
{
    /// <summary>Mehr verplant, als die Quote erlaubt; dann steht der Betrag in der reservierten Farbe.</summary>
    public bool Over => Planned > Limit;

    /// <summary>Wie weit der Balken reicht, in Prozent der erlaubten Summe.</summary>
    public decimal Percent => Limit <= 0m ? (Planned > 0m ? 100m : 0m) : Math.Min(Planned / Limit * 100m, 100m);
}

/// <summary>Was eine Monatskarte der Übersicht zeigt (Spec 82): Verplanbar, Anstehend und die drei Topf-Balken.</summary>
public sealed record MonthCard(decimal Free, decimal Unpaid, IReadOnlyList<MonthCardPot> Pots);

/// <summary>
/// Setzt die Monatskarten aus den Antworten der API zusammen. Verplanbar ist derselbe Wert der API, der früher "Frei" hieß;
/// Anstehend ergibt sich aus den nicht abgehakten Posten. Die erlaubte Summe eines Topfs ist seine Quote an den Einnahmen.
/// </summary>
public static class MonthCards
{
    private static readonly string[] PotsInOrder = [PotKeys.Needs, PotKeys.Wants, PotKeys.Savings];

    public static MonthCard ForPerson(PlanResponse plan)
    {
        var income = plan.Overview?.Income ?? plan.PlannedIncome;
        return new MonthCard(
            plan.Overview?.Free ?? 0m,
            MonthView.Unpaid(plan.Positions),
            Pots(income, plan.Quotas, key => plan.Overview?.Pots.FirstOrDefault(p => p.Pot == key)?.Planned ?? 0m));
    }

    public static MonthCard ForHousehold(HouseholdPlanResponse plan) => new(
        plan.Free,
        plan.Unpaid,
        Pots(plan.Income, plan.Quotas, key => plan.Pots.FirstOrDefault(p => p.Pot == key)?.Planned ?? 0m));

    private static List<MonthCardPot> Pots(decimal income, IReadOnlyList<PlanQuotaResponse> quotas, Func<string, decimal> planned) =>
        PotsInOrder
            .Select(key =>
            {
                var target = quotas.FirstOrDefault(q => q.Pot == key)?.Target ?? 0;
                return new MonthCardPot(key, target, planned(key), Math.Round(income * target / 100m, 2));
            })
            .ToList();
}
