namespace Duofy.Domain;

/// <summary>
/// Eine Zeile einer Buchung: ein Teil des Betrags mit eigenem Topf und eigener
/// Kategorie, zum Beispiel "60,00 Euro Lebensmittel, Grundbedarf" auf einem
/// Kassenzettel über 80,00 Euro, auf dem auch noch Körperpflege steht.
/// </summary>
public class BookingLine
{
    /// <summary>Die eindeutige Kennung; die Datenbankschicht vergibt sie beim Speichern.</summary>
    public Guid Id { get; set; }

    /// <summary>Der Anteil am Betrag der Buchung in Euro, immer größer als null.</summary>
    public required decimal Amount { get; set; }

    /// <summary>Der Topf dieser Zeile.</summary>
    public required Pot Pot { get; set; }

    /// <summary>Die Kategorie, siehe <see cref="Category"/>. Bei der Zeile einer Umbuchung null.</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>
    /// Der Posten des Monatsplans, den diese Zeile füllt; null, wenn sie keinem zugeordnet ist.
    /// Eine aufgeteilte Buchung kann so mehrere Posten füllen. Der Posten liegt immer im
    /// Planmonat der Buchung.
    /// </summary>
    public Guid? PositionId { get; set; }

    /// <summary>
    /// Das Sparziel, dem diese Zeile zugeordnet ist (Issue 30); null, wenn keinem. Erlaubt ist es nur für Zeilen, die
    /// Geld in den Topf Sparen hinein- oder aus ihm herausbewegen, siehe <see cref="SavingsFlow"/>.
    /// </summary>
    public Guid? SavingsGoalId { get; set; }

    /// <summary>
    /// Die Schuld oder der Kredit, auf den diese Zeile eine Zahlung ist (Issue 31); null, wenn keiner. Erlaubt ist es nur für
    /// Ausgaben, siehe <see cref="DebtPayments"/>. Eine Zeile gehört nie zugleich einem Sparziel und einer Schuld.
    /// </summary>
    public Guid? DebtId { get; set; }
}
