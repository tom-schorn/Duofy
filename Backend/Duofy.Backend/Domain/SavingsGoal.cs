namespace Duofy.Domain;

/// <summary>
/// Ein Sparziel (Issue 30): Geld, das eine Person für etwas zurücklegt, zum Beispiel "Urlaub" mit 1.200 Euro bis Juni.
///
/// Der Stand eines Ziels wird nie gespeichert. Er ergibt sich aus den Buchungszeilen, die dem Ziel zugeordnet sind
/// (<see cref="BookingLine.SavingsGoalId"/>), siehe <see cref="SavingsFlow"/>. Mehrere Ziele können dasselbe Konto nennen:
/// Das Konto zeigt, wo das Geld liegt, der Stand je Ziel sagt, wem es gehört.
/// </summary>
public class SavingsGoal
{
    /// <summary>Die eindeutige Kennung; die API vergibt sie beim Anlegen.</summary>
    public Guid Id { get; set; }

    /// <summary>Die Person, der das Ziel gehört.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Der Name, 1 bis 100 Zeichen.</summary>
    public required string Name { get; set; }

    /// <summary>Der Zielbetrag in Euro (größer als 0, zwei Nachkommastellen); ohne Betrag gibt es kein "erreicht".</summary>
    public decimal? TargetAmount { get; set; }

    /// <summary>
    /// "Schon angespart": ein einmaliger Startwert, den die Person eingibt (0 oder mehr, zwei Nachkommastellen). Er ist eine
    /// eingegebene Tatsache und erzeugt keine Buchung; Kontostände, Plan, Topfsummen und Quoten bleiben unberührt. Der Stand
    /// des Ziels ist dieser Wert plus die zugeordneten Einzahlungen minus Entnahmen.
    /// </summary>
    public decimal StartAmount { get; set; }

    /// <summary>Bis wann das Ziel erreicht sein soll; nur zur Anzeige, nichts rechnet damit.</summary>
    public DateOnly? TargetDate { get; set; }

    /// <summary>Das Sparkonto, auf dem das Geld liegt; leer, wenn es keinem Konto zugeordnet ist.</summary>
    public Guid? AccountId { get; set; }

    /// <summary>Wann das Ziel angelegt wurde.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Wann das Ziel zuletzt geändert wurde.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>True, wenn ein Zielbetrag gesetzt ist und der Stand ihn erreicht oder übersteigt.</summary>
    public bool IsReached(decimal balance) => TargetAmount is { } target && balance >= target;
}

/// <summary>
/// Wie eine Buchungszeile den Stand eines Sparziels verändert. Nur Zeilen, die Geld in den Topf Sparen hinein- oder
/// aus ihm herausbewegen, dürfen einem Ziel zugeordnet werden:
/// - Ausgabe im Topf Sparen: Einzahlung (plus).
/// - Umbuchung mit Zieltopf Sparen aus einem anderen Topf: Einzahlung (plus).
/// - Umbuchung aus dem Topf Sparen in einen anderen Topf: Entnahme (minus).
/// Alles andere (Einnahmen, Umbuchungen ohne Zieltopf, von Sparen nach Sparen) bewegt kein Ziel.
/// </summary>
public static class SavingsFlow
{
    /// <summary>
    /// Die Änderung des Stands in Euro, mit Vorzeichen; null, wenn die Zeile keinem Ziel zugeordnet werden darf.
    /// </summary>
    public static decimal? EffectOf(bool isTransfer, Pot? targetPot, Pot linePot, decimal amount)
    {
        if (!isTransfer)
        {
            return linePot == Pot.Savings ? amount : null;
        }
        if (targetPot is not { } target)
        {
            return null;
        }
        if (target == Pot.Savings && linePot != Pot.Savings)
        {
            return amount;
        }
        if (linePot == Pot.Savings && target != Pot.Savings)
        {
            return -amount;
        }
        return null;
    }

    /// <summary>Der Stand: die Summe aller Änderungen.</summary>
    public static decimal BalanceOf(IEnumerable<decimal> effects) => effects.Sum();
}
