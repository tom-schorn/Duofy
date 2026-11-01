using Duofy.Schemas;

namespace Duofy.Shared.Services;

/// <summary>Auf welcher Seite des Richtwerts ein Anteil liegt.</summary>
public enum GuidelineSide
{
    /// <summary>Auf dem Richtwert (auf eine Nachkommastelle gerundet gleich).</summary>
    Equal,

    /// <summary>Darüber.</summary>
    Over,

    /// <summary>Darunter.</summary>
    Under,
}

/// <summary>Ein Topf einer Person: Anteil an den Einnahmen, Richtwert und Abweichung in Prozentpunkten.</summary>
/// <param name="Outside">True, wenn die Abweichung größer ist als die Toleranz; nur dann erklärt die Seite sie.</param>
public sealed record GuidelineRow(string Pot, int Target, decimal Actual, decimal Deviation, GuidelineSide Side, bool Outside);

/// <summary>Eine Person mit ihren Anteilen; ohne Einnahmen gibt es keine Zeilen.</summary>
public sealed record GuidelinePerson(Guid UserId, string Name, decimal Income, bool HasIncome, IReadOnlyList<GuidelineRow> Rows)
{
    /// <summary>Die Töpfe, in denen die Person außerhalb der Toleranz liegt.</summary>
    public IReadOnlyList<GuidelineRow> OutsideRows => Rows.Where(r => r.Outside).ToList();
}

/// <summary>Was die Seite über den Richtwert im Haushalt zeigt (Issue #28).</summary>
/// <param name="AnyIncome">False, wenn kein Mitglied Einnahmen hat; dann gibt es nichts zu vergleichen.</param>
/// <param name="HouseholdFits">True, wenn der Haushalt in keinem Topf außerhalb der Toleranz liegt.</param>
/// <param name="HouseholdOutsidePots">Die Töpfe, in denen der Haushalt außerhalb der Toleranz liegt, in der Reihenfolge der API.</param>
public sealed record GuidelineModel(bool AnyIncome, bool HouseholdFits, IReadOnlyList<string> HouseholdOutsidePots, IReadOnlyList<GuidelinePerson> People);

/// <summary>
/// Macht aus den Zahlen der API (HouseholdPlanResponse.Members, Spec 27) die Darstellung der Richtwerte. Gerechnet wird
/// nichts Neues: Anteil und Abweichung schickt die API; hier steht nur, wann eine Abweichung erklärt wird.
/// Der Richtwert ist ein Anhaltspunkt: Bis <see cref="Tolerance"/> Prozentpunkte Abstand gilt ein Anteil als nah dran.
/// </summary>
public static class GuidelineView
{
    /// <summary>Wie viele Prozentpunkte Abstand vom Richtwert als "nah dran" gelten (eine Annahme, siehe Bericht zu Issue #28).</summary>
    public const decimal Tolerance = 5.0m;

    public static GuidelineRow Row(HouseholdMemberQuotaResponse quota)
    {
        var side = Math.Abs(quota.Deviation) < 0.05m ? GuidelineSide.Equal : quota.Deviation > 0m ? GuidelineSide.Over : GuidelineSide.Under;
        return new GuidelineRow(quota.Pot, quota.Target, quota.Actual, quota.Deviation, side, Math.Abs(quota.Deviation) > Tolerance);
    }

    public static GuidelineModel From(HouseholdPlanResponse plan)
    {
        var people = plan.Members
            .Select(m => new GuidelinePerson(m.UserId, m.FirstName, m.Income, m.HasIncome, m.HasIncome ? m.Quotas.Select(Row).ToList() : []))
            .ToList();
        var outside = plan.Quotas.Where(q => Math.Abs(q.Actual - q.Target) > Tolerance).Select(q => q.Pot).ToList();
        return new GuidelineModel(people.Any(p => p.HasIncome), outside.Count == 0, outside, people);
    }
}
