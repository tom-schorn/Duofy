namespace Duofy.Domain;

/// <summary>
/// Die Zahlen eines Monatsplans, aus den Buchungen seines Planmonats gerechnet. Gespeichert wird
/// davon nichts; wie beim Kontostand sind die Buchungen die einzige Wahrheit.
/// </summary>
public static class PlanFigures
{
    /// <summary>
    /// Gebucht je Posten (R4): die Summe der Buchungszeilen, die dem Posten zugeordnet sind. Die
    /// Zeilen einer Einnahme gehören nur zu Einnahme-Posten und die übrigen nur zu den anderen
    /// (das prüft die API), deshalb ist die Summe immer positiv.
    /// </summary>
    public static Dictionary<Guid, decimal> BookedPerPosition(IEnumerable<Booking> bookings)
    {
        var booked = new Dictionary<Guid, decimal>();
        foreach (var line in bookings.SelectMany(b => b.Lines))
        {
            if (line.PositionId is { } positionId)
            {
                booked[positionId] = booked.GetValueOrDefault(positionId) + line.Amount;
            }
        }
        return booked;
    }

    /// <summary>
    /// Das Ist der Quoten in Prozent mit einer Nachkommastelle, kaufmännisch gerundet (R8): je Topf die gebuchten Beträge
    /// der Ausgabe-Posten dieses Topfs geteilt durch die Einnahmen des Monats.
    ///
    /// Durchlaufende Posten zählen weder als Ausgabe noch als Einnahme, der Übertrag aus dem
    /// Vormonat zählt nicht als Einnahme: Beides ist kein Geld, das in diesem Monat verdient wurde.
    /// Ohne Einnahmen ist jedes Ist 0.
    /// </summary>
    /// <param name="bookings">Die Buchungen mit diesem Planmonat.</param>
    /// <param name="carryOverBookingId">Die Buchung des Übertrags in diesen Monat, falls es eine gibt.</param>
    public static Dictionary<Pot, decimal> QuotaActual(Plan plan, IReadOnlyCollection<Booking> bookings, Guid? carryOverBookingId) =>
        QuotaActual([(plan, bookings, carryOverBookingId)]);

    /// <summary>
    /// Das Ist der Quoten über mehrere Monate zugleich, zum Beispiel die Monate der Mitglieder eines Haushalts
    /// (Spec 69, R4): erst werden Ausgaben und Einnahmen aller Monate zusammengezählt, dann geteilt. Dieselben
    /// Regeln wie bei einem Monat: Durchlaufende Posten und der Übertrag zählen nicht.
    /// </summary>
    public static Dictionary<Pot, decimal> QuotaActual(IEnumerable<(Plan Plan, IReadOnlyCollection<Booking> Bookings, Guid? CarryOverBookingId)> months)
    {
        var income = 0m;
        var spentPerPot = new Dictionary<Pot, decimal> { [Pot.Needs] = 0m, [Pot.Wants] = 0m, [Pot.Savings] = 0m };
        foreach (var (plan, bookings, carryOverBookingId) in months)
        {
            var passThrough = plan.Positions.Where(p => p.PassThrough).Select(p => p.Id).ToHashSet();
            var booked = BookedPerPosition(bookings);

            income += bookings
                .Where(b => !b.IsTransfer && b.Id != carryOverBookingId)
                .SelectMany(b => b.Lines)
                .Where(l => l.Pot == Pot.Income && !(l.PositionId is { } id && passThrough.Contains(id)))
                .Sum(l => l.Amount);

            foreach (var pot in spentPerPot.Keys.ToList())
            {
                spentPerPot[pot] += plan.Positions
                    .Where(p => p.Pot == pot && !p.PassThrough)
                    .Sum(p => booked.GetValueOrDefault(p.Id));
            }
        }

        return spentPerPot.ToDictionary(
            entry => entry.Key,
            entry => income == 0 ? 0m : decimal.Round(entry.Value / income * 100m, 1, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Der Vorschlag für den Übertrag (R9): Einnahmen minus Ausgaben der Buchungen mit dem
    /// Planmonat des Vormonats. Umbuchungen bleiben im eigenen Geld und zählen nicht.
    /// </summary>
    public static decimal CarryOverSuggestion(IEnumerable<Booking> previousMonth)
    {
        var sums = MonthBook.SumsOf(previousMonth, accountId: null);
        return sums.Income - sums.Expenses;
    }
}
