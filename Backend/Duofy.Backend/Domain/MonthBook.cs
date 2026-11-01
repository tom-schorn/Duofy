namespace Duofy.Domain;

/// <summary>Die beiden Summen oben auf der Buchungsseite, in Euro und beide nicht negativ.</summary>
public sealed record MonthSums(decimal Income, decimal Expenses);

/// <summary>
/// Das Monatsbuch: die Buchungen eines Monats, wie die Seite "Buchungen" sie zeigt. Das Paar eines Übertrags gehört
/// nicht dazu; das filtert die API, denn erkannt wird es nur über die Kennungen im Plan (Spec 66, R9).
/// </summary>
public static class MonthBook
{
    /// <summary>
    /// Wählt die Buchungen eines Monats aus, die neueste zuerst.
    ///
    /// Mit einem Konto als Filter bleiben nur die Buchungen, die dieses Konto
    /// berühren, also als Konto oder als Gegenkonto einer Umbuchung. Ohne Filter
    /// (<c>null</c>) bleiben alle.
    /// </summary>
    public static IReadOnlyList<Booking> Select(IEnumerable<Booking> bookings, int year, int month, Guid? accountId) =>
        bookings
            .Where(b => b.PaymentDate.Year == year && b.PaymentDate.Month == month)
            .Where(b => accountId is null || b.AccountId == accountId || b.CounterAccountId == accountId)
            .OrderByDescending(b => b.PaymentDate)
            .ToList();

    /// <summary>
    /// Zählt Einnahmen und Ausgaben der gezeigten Buchungen.
    ///
    /// Ohne Konto-Filter zählen Umbuchungen in keiner Summe: Das Geld bleibt in
    /// der eigenen Hand. Mit einem Konto als Filter sieht die Person nur dieses
    /// Konto, und dort ist eine Umbuchung Geld, das hereinkommt oder hinausgeht.
    /// Sie zählt dann als Einnahme oder Ausgabe dieses Kontos.
    /// </summary>
    public static MonthSums SumsOf(IEnumerable<Booking> shown, Guid? accountId)
    {
        var income = 0m;
        var expenses = 0m;
        foreach (var booking in shown)
        {
            if (booking.IsTransfer)
            {
                if (accountId is not { } account)
                {
                    continue;
                }
                var effect = booking.EffectOn(account);
                if (effect > 0)
                {
                    income += effect;
                }
                else
                {
                    expenses -= effect;
                }
            }
            else
            {
                // Die Summen folgen den Töpfen der Zeilen, nicht nur dem Gesamtbetrag.
                foreach (var line in booking.Lines)
                {
                    if (line.Pot == Pot.Income)
                    {
                        income += line.Amount;
                    }
                    else
                    {
                        expenses += line.Amount;
                    }
                }
            }
        }
        return new MonthSums(income, expenses);
    }
}
