namespace Duofy.Shared.Services;

/// <summary>Eine Zeile der Plan-Übersicht: ein Monat, sein Frei-Betrag (null, solange es keinen Plan gibt) und ob er der laufende ist.</summary>
public sealed record PlanOverviewRow(int Year, int Month, decimal? Free, bool IsCurrent)
{
    /// <summary>Die Adresse des Monatsplans.</summary>
    public string Href => $"/plan/{Year}/{Month}";

    /// <summary>Die Kennung der Zeile für Tests und Sprungmarken, zum Beispiel "2026-9".</summary>
    public string Key => $"{Year}-{Month}";
}

/// <summary>
/// Die Regel der Plan-Übersicht (Spec 23, R2): alle Monate, für die es einen Plan gibt, dazu der laufende Monat und die
/// drei kommenden, der neueste zuerst. Mehr als diese Monate legt niemand an, die Liste bleibt also kurz.
/// </summary>
public static class PlanOverviewRows
{
    /// <summary>Wie viele Monate nach dem laufenden die Übersicht zum Vorausplanen anbietet.</summary>
    public const int ComingMonths = 3;

    /// <param name="withPlan">Die Monate mit Plan und ihr Frei-Betrag.</param>
    /// <param name="today">Der heutige Tag; sein Monat ist der laufende.</param>
    public static IReadOnlyList<PlanOverviewRow> Build(IEnumerable<(int Year, int Month, decimal Free)> withPlan, DateOnly today)
    {
        var free = new Dictionary<(int Year, int Month), decimal>();
        foreach (var (year, month, amount) in withPlan)
        {
            free[(year, month)] = amount;
        }

        var current = (today.Year, today.Month);
        var months = new HashSet<(int Year, int Month)>(free.Keys) { current };
        for (var by = 1; by <= ComingMonths; by++)
        {
            months.Add(PlanView.Shift(current.Year, current.Month, by));
        }

        return months
            .OrderByDescending(m => m.Year)
            .ThenByDescending(m => m.Month)
            .Select(m => new PlanOverviewRow(m.Year, m.Month, free.TryGetValue(m, out var amount) ? amount : null, m == current))
            .ToList();
    }
}
