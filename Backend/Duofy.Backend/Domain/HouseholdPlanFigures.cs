namespace Duofy.Domain;

/// <summary>
/// Ein Monat eines gezählten Mitglieds für den Haushaltsplan (Spec 69): sein Plan, seine Buchungen mit
/// diesem Planmonat und die Buchungen, die zum Übertrag gehören und nirgends mitzählen.
/// </summary>
/// <param name="CarryOverBookingIds">
/// Alle Buchungen der Überträge rund um diesen Monat: die beiden des eigenen Übertrags und die beiden des Übertrags
/// in den nächsten Monat, von denen eine in diesem Planmonat steht ("Übertrag nach …", bei einem negativen Rest eine
/// Einnahme). Keine davon zählt bei den Einnahmen der Quoten oder als nicht zugeordnet.
/// </param>
public sealed record MemberMonth(Plan Plan, IReadOnlyCollection<Booking> Bookings, IReadOnlyCollection<Guid> CarryOverBookingIds);

/// <summary>Geplant und gebucht in einem Topf.</summary>
public sealed record PotTotal(decimal Planned, decimal Booked);

/// <summary>Die Zahlen des Haushaltsplans; die Zielquoten kommen vom Haushalt und stehen hier nicht.</summary>
/// <param name="Income">Die geplanten Einnahmen ohne durchlaufende Posten.</param>
/// <param name="Pots">Geplant und gebucht je Topf für Grundbedarf, Wünsche und Sparen, ohne durchlaufende Posten.</param>
/// <param name="Unpaid">Was noch hinausgeht.</param>
/// <param name="QuotaActual">Das Ist der Quoten in Prozent mit einer Nachkommastelle.</param>
/// <param name="Unplanned">Die Buchungen des Planmonats ohne Posten je Topf, Einnahmen eingeschlossen.</param>
public sealed record HouseholdFigures(
    decimal Income,
    IReadOnlyDictionary<Pot, PotTotal> Pots,
    decimal Unpaid,
    IReadOnlyDictionary<Pot, decimal> QuotaActual,
    IReadOnlyDictionary<Pot, decimal> Unplanned);

/// <summary>
/// Die Zahlen des zusammengesetzten Haushaltsplans (Spec 69, R4): dieselben Regeln wie beim Plan einer Person
/// (<see cref="PlanFigures"/>), nur über die Monate aller gezählten Mitglieder.
/// </summary>
public static class HouseholdPlanFigures
{
    private static readonly Pot[] QuotaPots = [Pot.Needs, Pot.Wants, Pot.Savings];

    public static HouseholdFigures Compute(IReadOnlyCollection<MemberMonth> months)
    {
        var positions = months.SelectMany(m => m.Plan.Positions).ToList();
        var counting = positions.Where(p => !p.PassThrough).ToList();
        var booked = new Dictionary<Guid, decimal>();
        foreach (var month in months)
        {
            foreach (var (id, amount) in PlanFigures.BookedPerPosition(month.Bookings))
            {
                booked[id] = booked.GetValueOrDefault(id) + amount;
            }
        }

        var pots = QuotaPots.ToDictionary(
            pot => pot,
            pot => new PotTotal(
                counting.Where(p => p.Pot == pot).Sum(p => p.AmountPlanned),
                counting.Where(p => p.Pot == pot).Sum(p => booked.GetValueOrDefault(p.Id))));

        // Was noch hinausgeht: Einnahmen, Abgehaktes und durchlaufende Posten nie; sonst der geplante Betrag
        // minus das Gebuchte, nie unter 0 (wer ein Budget überzieht, hat nichts mehr offen).
        var unpaid = positions
            .Where(p => p.Pot != Pot.Income && !p.IsDone && !p.PassThrough)
            .Sum(p => Math.Max(p.AmountPlanned - booked.GetValueOrDefault(p.Id), 0m));

        var unplanned = new Dictionary<Pot, decimal>();
        foreach (Pot pot in Enum.GetValues<Pot>())
        {
            unplanned[pot] = months
                .SelectMany(m => m.Bookings.Where(b => !b.IsTransfer && !m.CarryOverBookingIds.Contains(b.Id)))
                .SelectMany(b => b.Lines)
                .Where(l => l.PositionId is null && l.Pot == pot)
                .Sum(l => l.Amount);
        }

        return new HouseholdFigures(
            counting.Where(p => p.Pot == Pot.Income).Sum(p => p.AmountPlanned),
            pots,
            unpaid,
            PlanFigures.QuotaActual(months.Select(m => (
                m.Plan,
                (IReadOnlyCollection<Booking>)m.Bookings.Where(b => !m.CarryOverBookingIds.Contains(b.Id)).ToList(),
                m.Plan.CarryOverIncomeBookingId))),
            unplanned);
    }
}
