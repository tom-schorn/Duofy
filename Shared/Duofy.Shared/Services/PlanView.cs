using Duofy.Schemas;

namespace Duofy.Shared.Services;

/// <summary>
/// Die Regeln der Planseite und des Buchungsdialogs, die nichts mit dem Zeichnen zu tun haben:
/// Reihenfolge der Töpfe, Blättern zwischen Monaten, was beim Abhaken vorbelegt ist und welche
/// Posten eine Buchungszeile füllen darf. So lassen sie sich ohne Oberfläche testen.
/// </summary>
public static class PlanView
{
    /// <summary>
    /// True, wenn der Plan den Posten als überfällig zeigt, weil seine Erinnerung dreimal mit "Später" verschoben wurde und er
    /// noch offen ist (Spec 118, R6). Sonst entscheidet allein der Fälligkeitstag (siehe <see cref="MonthView"/>).
    /// </summary>
    public static bool IsPostponedOverdue(PlanPositionResponse position) =>
        position.DoneAt is null && position.PostponeCount >= ReminderLimits.MaxPostpones;

    /// <summary>Die Töpfe in der Reihenfolge der Seite: zuerst, was hereinkommt, dann was hinausgeht.</summary>
    public static IReadOnlyList<string> PotOrder { get; } = [PotKeys.Income, PotKeys.Needs, PotKeys.Wants, PotKeys.Savings];

    /// <summary>Die Posten je Topf, in der Reihenfolge der API (Fälligkeitstag, dann Name); leere Töpfe fehlen.</summary>
    public static IReadOnlyList<(string Pot, IReadOnlyList<PlanPositionResponse> Positions)> Groups(PlanResponse plan) =>
        PotOrder
            .Select(pot => (pot, (IReadOnlyList<PlanPositionResponse>)plan.Positions.Where(p => p.Pot == pot).ToList()))
            .Where(group => group.Item2.Count > 0)
            .ToList();

    /// <summary>
    /// Die Töpfe der Planseite (Issue #94, R8): die mit Posten und dazu die Ausgabe-Töpfe, in denen etwas ohne Posten gebucht ist, damit
    /// auch dort die Zeile Ungeplant steht; sonst wie <see cref="Groups"/>.
    /// </summary>
    public static IReadOnlyList<(string Pot, IReadOnlyList<PlanPositionResponse> Positions)> GroupsWithUnplanned(PlanResponse plan) =>
        PotOrder
            .Select(pot => (pot, (IReadOnlyList<PlanPositionResponse>)plan.Positions.Where(p => p.Pot == pot).ToList()))
            .Where(group => group.Item2.Count > 0 || (group.pot != PotKeys.Income && Unplanned(plan, group.pot) != 0m))
            .ToList();

    /// <summary>
    /// Das Geplante eines Topfs, wie es die Übersicht und das Flussdiagramm zeigen (Spec 82, R17): aus der Übersicht der API,
    /// ohne durchlaufende Posten; ohne Übersicht die Summe der Posten.
    /// </summary>
    public static decimal PotPlanned(PlanResponse plan, string pot) =>
        pot == PotKeys.Income
            ? plan.Overview?.Income ?? plan.Positions.Where(p => p.Pot == pot && !p.PassThrough).Sum(p => p.AmountPlanned)
            : plan.Overview?.Pots.FirstOrDefault(p => p.Pot == pot)?.Planned ?? plan.Positions.Where(p => p.Pot == pot && !p.PassThrough).Sum(p => p.AmountPlanned);

    /// <summary>Das Gebuchte eines Topfs aus der Übersicht der API, dieselbe Zahl wie auf Start und Übersicht; ohne Übersicht null.</summary>
    public static decimal? PotBooked(PlanResponse plan, string pot) =>
        plan.Overview is null ? null : plan.Overview.Pots.FirstOrDefault(p => p.Pot == pot)?.Booked ?? 0m;

    /// <summary>Was in den drei Ausgabe-Töpfen ohne Posten gebucht ist, je Topf: die Zeile Ungeplant (Spec 82, R17).</summary>
    public static IReadOnlyDictionary<string, decimal> UnplannedPerPot(PlanResponse plan) =>
        new[] { PotKeys.Needs, PotKeys.Wants, PotKeys.Savings }.ToDictionary(pot => pot, pot => Unplanned(plan, pot));

    /// <summary>
    /// Was in einem Topf ohne Posten gebucht ist: die Zeile Ungeplant (Spec 82). Umbuchungen zählen nie dazu, auch wenn ihre
    /// Zeilen einen Topf und keinen Posten haben; so rechnet auch die API.
    /// </summary>
    public static decimal Unplanned(PlanResponse plan, string pot) => (plan.Overview?.Bookings ?? [])
        .Where(b => b.Kind != BookingKinds.Transfer)
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
