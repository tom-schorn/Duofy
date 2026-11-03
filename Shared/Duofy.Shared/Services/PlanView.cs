using Duofy.Schemas;

namespace Duofy.Shared.Services;

/// <summary>
/// Die Regeln der Planseite und des Buchungsdialogs, die nichts mit dem Zeichnen zu tun haben:
/// Reihenfolge der Töpfe, Blättern zwischen Monaten, was beim Abhaken vorbelegt ist und welche
/// Posten eine Buchungszeile füllen darf. So lassen sie sich ohne Oberfläche testen.
/// </summary>
public static class PlanView
{
    /// <summary>Die Töpfe in der Reihenfolge der Seite: zuerst, was hereinkommt, dann was hinausgeht.</summary>
    public static IReadOnlyList<string> PotOrder { get; } = [PotKeys.Income, PotKeys.Needs, PotKeys.Wants, PotKeys.Savings];

    /// <summary>Die Posten je Topf, in der Reihenfolge der API (Fälligkeitstag, dann Name); leere Töpfe fehlen.</summary>
    public static IReadOnlyList<(string Pot, IReadOnlyList<PlanPositionResponse> Positions)> Groups(PlanResponse plan) =>
        PotOrder
            .Select(pot => (pot, (IReadOnlyList<PlanPositionResponse>)plan.Positions.Where(p => p.Pot == pot).ToList()))
            .Where(group => group.Item2.Count > 0)
            .ToList();

    /// <summary>
    /// Das Geplante eines Topfs, wie es die Übersicht und das Flussdiagramm zeigen (Spec 82, R17): aus der Übersicht der API,
    /// ohne durchlaufende Posten; ohne Übersicht die Summe der Posten.
    /// </summary>
    public static decimal PotPlanned(PlanResponse plan, string pot) =>
        pot == PotKeys.Income
            ? plan.Overview?.Income ?? plan.Positions.Where(p => p.Pot == pot && !p.PassThrough).Sum(p => p.AmountPlanned)
            : plan.Overview?.Pots.FirstOrDefault(p => p.Pot == pot)?.Planned ?? plan.Positions.Where(p => p.Pot == pot && !p.PassThrough).Sum(p => p.AmountPlanned);

    /// <summary>Was in den drei Ausgabe-Töpfen ohne Posten gebucht ist, je Topf: die Zeile Ungeplant (Spec 82, R17).</summary>
    public static IReadOnlyDictionary<string, decimal> UnplannedPerPot(PlanResponse plan) =>
        new[] { PotKeys.Needs, PotKeys.Wants, PotKeys.Savings }.ToDictionary(pot => pot, pot => Unplanned(plan, pot));

    /// <summary>Was in einem Topf ohne Posten gebucht ist: die Zeile Ungeplant (Spec 82).</summary>
    public static decimal Unplanned(PlanResponse plan, string pot) => (plan.Overview?.Bookings ?? [])
        .SelectMany(b => b.Lines)
        .Where(l => l.PositionId is null && l.Pot == pot)
        .Sum(l => l.Amount);

    /// <summary>Der Monat, der <paramref name="by"/> Monate neben dem angegebenen liegt, auch über den Jahreswechsel.</summary>
    public static (int Year, int Month) Shift(int year, int month, int by)
    {
        var shifted = new DateOnly(year, month, 1).AddMonths(by);
        return (shifted.Year, shifted.Month);
    }

    /// <summary>Ein Posten lässt sich nur löschen, solange keine Buchung auf ihm liegt (R10).</summary>
    public static bool CanDelete(PlanPositionResponse position) => position.Booked == 0m;

    /// <summary>
    /// Die Posten, die eine Zeile dieser Art füllen darf: alle Posten des Planmonats, deren Topf passt, auch abgehakte
    /// (Spec 51, R7: die Mahnkosten gehören zur GEZ, auch wenn sie schon abgehakt ist). Einnahmen füllen Einnahme-Posten,
    /// Ausgaben die übrigen, eine Umbuchung Posten im Topf Sparen und jeden Posten mit eigenem Zielkonto.
    /// </summary>
    public static IReadOnlyList<PlanPositionResponse> OfferedPositions(PlanResponse? plan, string kind) =>
        plan is null
            ? []
            : plan.Positions
                .Where(p => kind switch
                {
                    BookingKinds.Income => p.Pot == PotKeys.Income,
                    BookingKinds.Transfer => p.Pot == PotKeys.Savings || p.CounterAccountId is not null,
                    _ => p.Pot != PotKeys.Income,
                })
                .ToList();
}
