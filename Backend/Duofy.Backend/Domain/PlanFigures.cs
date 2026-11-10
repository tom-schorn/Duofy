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

            // Umbuchungen zwischen zwei Töpfen verschieben ihren Betrag (Spec 112, R2); Einnahmen und Nenner bleiben.
            foreach (var (pot, moved) in PotShift(bookings))
            {
                spentPerPot[pot] += moved;
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
    /// Der Frei-Betrag eines oder mehrerer Monate (Spec 23; Spec 66, R9; Spec 93): die Einnahme-Posten minus die
    /// Ausgabe-Posten plus der Übertrag aus dem Vormonat, dazu die gebuchten Einnahmen ohne Posten und abzüglich der
    /// gebuchten Ausgaben ohne Posten. Jeder Posten zählt mit seinem geplanten Betrag, oder mit dem gebuchten, wenn mehr
    /// gebucht ist (Spec 93, R7), offen oder abgehakt: Ein Budget wie Lebensmittel wird oft früh abgehakt und trotzdem
    /// weiter ausgegeben, deshalb macht weniger Gebuchtes kein Geld frei. Durchlaufende Posten zählen weder als Einnahme
    /// noch als Ausgabe, wie bei den Quoten. Alle Parameter sind Pflicht, damit keine Stelle das Gebuchte, den Übertrag
    /// oder das Ungeplante aus Versehen vergisst.
    /// </summary>
    /// <param name="booked">
    /// Gebucht je Posten, siehe <see cref="BookedPerPosition"/>, aus den Buchungen, die der Betrachter sehen darf; ohne
    /// Sicht leer, dann zählt jeder Posten mit seinem geplanten Betrag.
    /// </param>
    /// <param name="carriedIn">
    /// Der Übertrag, den der Betrachter sehen darf, siehe <see cref="CarriedIn"/>; 0 ohne Übertrag. Über mehrere Monate
    /// die Summe. Der Übertrag in den nächsten Monat gehört nicht hierher, er ändert den Frei-Betrag dieses Monats nicht.
    /// </param>
    /// <param name="unplanned">
    /// Die Buchungen ohne Posten je Topf, die der Betrachter sehen darf, siehe <see cref="Unplanned"/>: ohne Umbuchungen und
    /// ohne die Buchungen der Überträge. Über mehrere Monate die Summe; ein fehlender Topf zählt als 0.
    /// </param>
    /// <param name="transfers">
    /// Was die Umbuchungen über die Grenze zwischen verfügbaren und nicht verfügbaren Konten ändern (Spec 98), siehe
    /// <see cref="TransferEffect"/>; 0 ohne solche Umbuchungen. Über mehrere Monate die Summe.
    /// </param>
    public static decimal Free(
        IEnumerable<PlanPosition> positions, IReadOnlyDictionary<Guid, decimal> booked, decimal carriedIn, IReadOnlyDictionary<Pot, decimal> unplanned,
        decimal transfers)
    {
        var counting = positions.Where(p => !p.PassThrough).ToList();
        decimal Counted(PlanPosition p) => Math.Max(p.AmountPlanned, booked.GetValueOrDefault(p.Id));
        var income = counting.Where(p => p.Pot == Pot.Income).Sum(Counted);
        var expenses = counting.Where(p => p.Pot != Pot.Income).Sum(Counted);
        var unplannedExpenses = unplanned.Where(entry => entry.Key != Pot.Income).Sum(entry => entry.Value);
        return income - expenses + carriedIn + unplanned.GetValueOrDefault(Pot.Income) - unplannedExpenses + transfers;
    }

    /// <summary>
    /// Was die Umbuchungen eines Planmonats am Frei-Betrag ändern (Spec 98): Geld, das von einem nicht verfügbaren auf ein
    /// verfügbares Konto zurückgeholt wird, erhöht ihn um den Betrag; Geld, das von einem verfügbaren auf ein nicht verfügbares
    /// Konto wandert, senkt ihn, außer die Zeile liegt auf einem Posten, denn dann zählt der Posten schon. Umbuchungen auf
    /// derselben Seite und die Buchungen der Überträge ändern nichts. Die Regel steht nur hier, für eine Person und für den Haushalt.
    /// </summary>
    /// <param name="availableAccountIds">Die Konten, die als verfügbar zählen; jedes andere gilt als nicht verfügbar.</param>
    /// <param name="carryOverBookingIds">Alle Buchungen der Überträge rund um den Monat, siehe <see cref="Unplanned"/>.</param>
    public static decimal TransferEffect(
        IEnumerable<Booking> bookings, IReadOnlySet<Guid> availableAccountIds, IReadOnlyCollection<Guid> carryOverBookingIds)
    {
        var effect = 0m;
        foreach (var transfer in bookings.Where(b => b.IsTransfer && !carryOverBookingIds.Contains(b.Id)))
        {
            var fromAvailable = availableAccountIds.Contains(transfer.AccountId);
            var toAvailable = availableAccountIds.Contains(transfer.CounterAccountId!.Value);
            if (!fromAvailable && toAvailable)
            {
                effect += transfer.Amount;
            }
            else if (fromAvailable && !toAvailable && transfer.Lines.All(l => l.PositionId is null))
            {
                effect -= transfer.Amount;
            }
        }
        return effect;
    }

    /// <summary>
    /// Der Betrag des Übertrags in diesen Monat aus seiner Buchung mit diesem Planmonat: bei einem positiven Rest die Einnahme,
    /// bei einem negativen die Ausgabe als negativer Betrag; null ohne Übertrag oder ohne Sicht auf die Buchungen. Der Plan
    /// kennt beide Hälften des Paares, die andere liegt im Vormonat und zählt hier nicht, auch wenn die Liste sie enthält.
    /// </summary>
    /// <param name="bookings">Die Buchungen, die der Betrachter sehen darf.</param>
    public static decimal? CarriedIn(Plan plan, IReadOnlyCollection<Booking> bookings)
    {
        var inMonth = bookings.Where(b => b.PlanYear == plan.Year && b.PlanMonth == plan.Month).ToList();
        return inMonth.FirstOrDefault(b => b.Id == plan.CarryOverIncomeBookingId)?.Amount
            ?? -inMonth.FirstOrDefault(b => b.Id == plan.CarryOverExpenseBookingId)?.Amount;
    }

    /// <summary>
    /// Geplant und gebucht je Topf für Grundbedarf, Wünsche und Sparen (Spec 24), ohne durchlaufende Posten: der Stoff
    /// des Lückendiagramms. Gebucht zählt auch, was ohne Posten im Topf liegt (Spec 72), sonst bliebe alles Ungeplante
    /// als Abweichung unsichtbar. Gleiche Regel für eine Person und für den Haushalt.
    /// </summary>
    /// <param name="unplanned">Die Buchungen ohne Posten je Topf, siehe <see cref="Unplanned"/>.</param>
    /// <param name="potShift">Was Umbuchungen zwischen Töpfen verschieben (Spec 112), siehe <see cref="PotShift"/>; ohne Angabe nichts.</param>
    public static Dictionary<Pot, PotTotal> PotTotals(
        IEnumerable<PlanPosition> positions, IReadOnlyDictionary<Guid, decimal> booked, IReadOnlyDictionary<Pot, decimal> unplanned,
        IReadOnlyDictionary<Pot, decimal>? potShift = null)
    {
        var counting = positions.Where(p => !p.PassThrough).ToList();
        return new[] { Pot.Needs, Pot.Wants, Pot.Savings }.ToDictionary(
            pot => pot,
            pot => new PotTotal(
                counting.Where(p => p.Pot == pot).Sum(p => p.AmountPlanned),
                counting.Where(p => p.Pot == pot).Sum(p => booked.GetValueOrDefault(p.Id)) + unplanned.GetValueOrDefault(pot)
                    + (potShift?.GetValueOrDefault(pot) ?? 0m)));
    }

    /// <summary>
    /// Was die Umbuchungen zwischen zwei verschiedenen Töpfen verschieben (Spec 112): der Betrag geht vom Quelltopf ab (der Topf
    /// der Zeile) und kommt im Zieltopf dazu. Umbuchungen mit demselben Topf auf beiden Seiten, ohne Zieltopf oder ohne Zeile
    /// ändern nichts. Nur der gebuchte Betrag je Topf und das Ist der Quoten rechnen damit, nie der Frei-Betrag, das Feste
    /// oder ein Kontostand. Die Regel steht nur hier, für eine Person und für den Haushalt.
    /// </summary>
    public static Dictionary<Pot, decimal> PotShift(IEnumerable<Booking> bookings)
    {
        var shift = new Dictionary<Pot, decimal> { [Pot.Needs] = 0m, [Pot.Wants] = 0m, [Pot.Savings] = 0m };
        foreach (var transfer in bookings.Where(b => b.IsTransfer && b.TargetPot is not null))
        {
            if (transfer.Lines.Count != 1)
            {
                continue;
            }
            var source = transfer.Lines[0].Pot;
            var target = transfer.TargetPot!.Value;
            if (source == target || source == Pot.Income || target == Pot.Income)
            {
                continue;
            }
            shift[source] -= transfer.Amount;
            shift[target] += transfer.Amount;
        }
        return shift;
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
