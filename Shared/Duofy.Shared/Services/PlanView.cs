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

    /// <summary>Der Monat, der <paramref name="by"/> Monate neben dem angegebenen liegt, auch über den Jahreswechsel.</summary>
    public static (int Year, int Month) Shift(int year, int month, int by)
    {
        var shifted = new DateOnly(year, month, 1).AddMonths(by);
        return (shifted.Year, shifted.Month);
    }

    /// <summary>Was beim Abhaken eines Postens mit Buchungen als Ausgleich vorbelegt ist: der offene Rest, nie weniger als 0.</summary>
    public static decimal OpenAmount(PlanPositionResponse position) => Math.Max(position.AmountPlanned - position.Booked, 0m);

    /// <summary>Ein Posten lässt sich nur löschen, solange keine Buchung auf ihm liegt (R10).</summary>
    public static bool CanDelete(PlanPositionResponse position) => position.Booked == 0m;

    /// <summary>
    /// Die Posten, die eine Zeile dieser Art füllen darf (R6): offene Posten des Planmonats, deren Topf
    /// passt. Einnahmen füllen Einnahme-Posten, Ausgaben die übrigen, eine Umbuchung Posten im Topf
    /// Sparen und jeden Posten mit eigenem Zielkonto. Der schon gewählte Posten bleibt in der Liste, auch wenn er inzwischen abgehakt ist.
    /// </summary>
    public static IReadOnlyList<PlanPositionResponse> OfferedPositions(PlanResponse? plan, string kind, Guid? chosen) =>
        plan is null
            ? []
            : plan.Positions
                .Where(p => p.Id == chosen || p.DoneAt is null)
                .Where(p => kind switch
                {
                    BookingKinds.Income => p.Pot == PotKeys.Income,
                    BookingKinds.Transfer => p.Pot == PotKeys.Savings || p.CounterAccountId is not null,
                    _ => p.Pot != PotKeys.Income,
                })
                .ToList();
}
