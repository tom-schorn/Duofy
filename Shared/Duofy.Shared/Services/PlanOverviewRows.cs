namespace Duofy.Shared.Services;

/// <summary>
/// Eine Zeile der Plan-Übersicht: ein Monat, sein Frei-Betrag (null, solange es keinen Plan gibt), ob er der laufende ist
/// und ob im Frei-Betrag ein Übertrag steckt.
/// </summary>
public sealed record PlanOverviewRow(int Year, int Month, decimal? Free, bool IsCurrent, bool WithCarryOver = false, string BasePath = "/plan")
{
    /// <summary>Die Adresse des Monatsplans, eigener Plan oder Haushaltsplan.</summary>
    public string Href => $"{BasePath}/{Year}/{Month}";

    /// <summary>Die Kennung der Zeile für Tests und Sprungmarken, zum Beispiel "2026-9".</summary>
    public string Key => $"{Year}-{Month}";
}

/// <summary>
/// Die Regel der Plan-Übersicht (Spec 23, Issue #94, R6): der laufende Monat zuerst, dann die vorhandenen künftigen Monate (der
/// nächste zuerst), dann die vergangenen mit Plan, der neueste zuerst. Leere künftige Karten gibt es nicht; stattdessen bietet die
/// Seite einen Knopf zum Anlegen des nächsten Monats (<see cref="NextToCreate"/>).
/// </summary>
public static class PlanOverviewRows
{
    /// <param name="withPlan">Die Monate mit Plan, ihr Frei-Betrag und ob ein Übertrag darin steckt.</param>
    /// <param name="today">Der heutige Tag; sein Monat ist der laufende.</param>
    /// <param name="basePath">Wohin eine Karte führt: "/plan" für eigene Pläne, "/haushaltsplan" für den Haushalt.</param>
    public static IReadOnlyList<PlanOverviewRow> Build(IEnumerable<(int Year, int Month, decimal Free, bool WithCarryOver)> withPlan, DateOnly today, string basePath = "/plan")
    {
        var known = new Dictionary<(int Year, int Month), (decimal Amount, bool WithCarryOver)>();
        foreach (var (year, month, amount, withCarryOver) in withPlan)
        {
            known[(year, month)] = (amount, withCarryOver);
        }

        var current = (today.Year, today.Month);
        PlanOverviewRow Row((int Year, int Month) m) => known.TryGetValue(m, out var plan)
            ? new PlanOverviewRow(m.Year, m.Month, plan.Amount, m == current, plan.WithCarryOver, basePath)
            : new PlanOverviewRow(m.Year, m.Month, null, m == current, BasePath: basePath);

        var ahead = known.Keys.Where(m => m.CompareTo(current) > 0).OrderBy(m => m.Year).ThenBy(m => m.Month);
        var past = known.Keys.Where(m => m.CompareTo(current) < 0).OrderByDescending(m => m.Year).ThenByDescending(m => m.Month);
        return new[] { current }.Concat(ahead).Concat(past).Select(Row).ToList();
    }

    /// <summary>Der Monat, den "Nächsten Monat anlegen" öffnet: der Monat nach dem letzten vorhandenen, mindestens nach dem laufenden.</summary>
    public static (int Year, int Month) NextToCreate(IEnumerable<(int Year, int Month, decimal Free, bool WithCarryOver)> withPlan, DateOnly today)
    {
        var last = withPlan.Select(m => (m.Year, m.Month)).Append((today.Year, today.Month)).Max();
        return PlanView.Shift(last.Year, last.Month, 1);
    }
}
