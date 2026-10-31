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
        var (income, spentPerPot) = QuotaBase(months);
        return spentPerPot.ToDictionary(
            entry => entry.Key,
            entry => income == 0 ? 0m : decimal.Round(entry.Value / income * 100m, 1, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Die Einnahmen, durch die das Ist der Quoten geteilt wird (Spec 27): gebuchte Einnahmen ohne durchlaufende
    /// Posten, Umbuchungen und den Übertrag. Dieselbe Zahl wie im Nenner von QuotaActual.
    /// </summary>
    public static decimal QuotaIncome(IEnumerable<(Plan Plan, IReadOnlyCollection<Booking> Bookings, Guid? CarryOverBookingId)> months) =>
        QuotaBase(months).Income;

    /// <summary>Einnahmen und gebuchte Ausgaben je Topf, wie sie die Quote braucht; die Regeln stehen nur hier.</summary>
    private static (decimal Income, Dictionary<Pot, decimal> SpentPerPot) QuotaBase(
        IEnumerable<(Plan Plan, IReadOnlyCollection<Booking> Bookings, Guid? CarryOverBookingId)> months)
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

        return (income, spentPerPot);
    }

    /// <summary>Die geplanten Einnahmen eines oder mehrerer Monate (Spec 24); durchlaufende Posten zählen nicht.</summary>
    public static decimal Income(IEnumerable<PlanPosition> positions) =>
        positions.Where(p => !p.PassThrough && p.Pot == Pot.Income).Sum(p => p.AmountPlanned);

    /// <summary>Das Feste eines oder mehrerer Monate (Spec 24): die geplanten Ausgaben in allen Töpfen, ohne durchlaufende Posten.</summary>
    public static decimal Fixed(IEnumerable<PlanPosition> positions) =>
        positions.Where(p => !p.PassThrough && p.Pot != Pot.Income).Sum(p => p.AmountPlanned);

    /// <summary>
    /// Der Frei-Betrag eines oder mehrerer Monate für die Plan-Übersicht (Spec 23): die geplanten Einnahmen minus die
    /// geplanten Ausgaben. Durchlaufende Posten zählen weder als Einnahme noch als Ausgabe, wie bei den Quoten.
    /// </summary>
    public static decimal Free(IEnumerable<PlanPosition> positions)
    {
        var counting = positions.ToList();
        return Income(counting) - Fixed(counting);
    }

    /// <summary>
    /// Geplant und gebucht je Topf für Grundbedarf, Wünsche und Sparen (Spec 24), ohne durchlaufende Posten: der Stoff
    /// des Lückendiagramms. Gebucht zählt auch, was ohne Posten im Topf liegt (Spec 72), sonst bliebe alles Ungeplante
    /// als Abweichung unsichtbar. Gleiche Regel für eine Person und für den Haushalt.
    /// </summary>
    /// <param name="unplanned">Die Buchungen ohne Posten je Topf, siehe <see cref="Unplanned"/>.</param>
    public static Dictionary<Pot, PotTotal> PotTotals(
        IEnumerable<PlanPosition> positions, IReadOnlyDictionary<Guid, decimal> booked, IReadOnlyDictionary<Pot, decimal> unplanned)
    {
        var counting = positions.Where(p => !p.PassThrough).ToList();
        return new[] { Pot.Needs, Pot.Wants, Pot.Savings }.ToDictionary(
            pot => pot,
            pot => new PotTotal(
                counting.Where(p => p.Pot == pot).Sum(p => p.AmountPlanned),
                counting.Where(p => p.Pot == pot).Sum(p => booked.GetValueOrDefault(p.Id)) + unplanned.GetValueOrDefault(pot)));
    }

    /// <summary>
    /// Die Buchungen ohne Posten je Topf, Einnahmen eingeschlossen (Spec 69, R4): Umbuchungen und die Buchungen des
    /// Übertrags zählen nicht. Die Regel steht nur hier, für eine Person und für den Haushalt.
    /// </summary>
    /// <param name="carryOverBookingIds">Alle Buchungen der Überträge rund um den Monat, siehe <see cref="MemberMonth"/>.</param>
    public static Dictionary<Pot, decimal> Unplanned(IEnumerable<Booking> bookings, IReadOnlyCollection<Guid> carryOverBookingIds)
    {
        var lines = bookings
            .Where(b => !b.IsTransfer && !carryOverBookingIds.Contains(b.Id))
            .SelectMany(b => b.Lines)
            .Where(l => l.PositionId is null)
            .ToList();
        return Enum.GetValues<Pot>().ToDictionary(pot => pot, pot => lines.Where(l => l.Pot == pot).Sum(l => l.Amount));
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
