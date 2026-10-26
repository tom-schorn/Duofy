namespace Duofy.Domain;

/// <summary>
/// Der Topf einer Buchung: eines der vier Budgets der 50/30/20-Regel.
///
/// Der Topf bestimmt auch die Richtung einer normalen Buchung: Eine Buchung im
/// Topf <see cref="Income"/> bringt Geld aufs Konto, eine Buchung in einem der
/// anderen drei Töpfe nimmt es weg. Deshalb ist der Betrag einer Buchung immer
/// positiv, siehe <see cref="Booking.EffectOn"/>.
/// </summary>
public enum Pot
{
    /// <summary>Einnahmen, zum Beispiel Gehalt.</summary>
    Income,

    /// <summary>Grundbedarf: Miete, Lebensmittel und alles, was sein muss.</summary>
    Needs,

    /// <summary>Wünsche: alles, worauf man auch verzichten könnte.</summary>
    Wants,

    /// <summary>Sparen: Geld, das zurückgelegt wird.</summary>
    Savings,
}
