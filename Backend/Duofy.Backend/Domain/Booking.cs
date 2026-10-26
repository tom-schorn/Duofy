namespace Duofy.Domain;

/// <summary>
/// Eine Buchung: eine einzelne Geldbewegung, die tatsächlich stattgefunden hat
/// oder fest erwartet wird, zum Beispiel "Einkauf, 54,20 Euro, am 3. Oktober".
///
/// Es gibt drei Arten, und die Art ergibt sich aus den Feldern, es gibt kein
/// eigenes Feld dafür:
/// - Ausgabe: Geld verlässt ein Konto. Die Zeilen liegen in den Töpfen Grundbedarf, Wünsche oder Sparen.
/// - Einnahme: Geld kommt auf ein Konto. Die Zeilen liegen im Topf Einnahmen.
/// - Umbuchung: Geld wandert von einem eigenen Konto auf ein anderes
///   (<see cref="CounterAccountId"/> ist gesetzt), ohne Kategorie.
///
/// Aus den Buchungen ergibt sich der Kontostand, siehe <see cref="Account.Balance"/>.
/// </summary>
public class Booking
{
    /// <summary>Die eindeutige Kennung der Buchung; die Datenbankschicht vergibt sie beim Speichern.</summary>
    public Guid Id { get; set; }

    /// <summary>Die Kennung der Person, der die Buchung gehört.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>
    /// Das Konto der Buchung. Bei einer Umbuchung ist es das Konto, von dem das
    /// Geld abgeht.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Nur bei einer Umbuchung: das Konto, auf das das Geld geht. Bei Ausgabe und
    /// Einnahme null.
    /// </summary>
    public Guid? CounterAccountId { get; set; }

    /// <summary>
    /// Die Zeilen einer Ausgabe oder Einnahme. Auf einem Kassenzettel stehen oft
    /// mehrere Zwecke, zum Beispiel Lebensmittel und Drogerie; jede Zeile hat
    /// Betrag, Topf und Kategorie. Die Beträge der Zeilen ergeben zusammen den
    /// <see cref="Amount"/>.
    ///
    /// Eine Ausgabe oder Einnahme hat 1 bis 20 Zeilen. Eine Umbuchung hat keine
    /// Zeile oder genau eine mit dem Topf Sparen und ohne Kategorie.
    /// </summary>
    public List<BookingLine> Lines { get; set; } = [];

    /// <summary>Eine freie Notiz, höchstens 200 Zeichen.</summary>
    public string? Note { get; set; }

    /// <summary>
    /// Die Bezeichnung, die im Haushaltsbuch angezeigt wird, zum Beispiel "Einkauf"
    /// oder "Gehalt". Neue Buchungen brauchen sie nicht; sie haben Kategorie und Notiz.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Der Tag, an dem das Geld das Konto verlässt oder dort ankommt.
    ///
    /// Der Tag darf in der Zukunft liegen. Die Buchung zählt dann erst ab diesem
    /// Tag zum Kontostand.
    /// </summary>
    public required DateOnly PaymentDate { get; set; }

    /// <summary>
    /// Das Jahr des Planmonats: der Monat, in dessen Plan die Buchung zählt. Er darf vom Datum
    /// um einen Monat abweichen, zum Beispiel ein Gehalt am 25. September für den Oktober.
    /// Der Kontostand folgt immer dem echten Datum, nie dem Planmonat.
    /// </summary>
    public int PlanYear { get; set; }

    /// <summary>Der Monat des Planmonats, 1 bis 12; siehe <see cref="PlanYear"/>.</summary>
    public int PlanMonth { get; set; }

    /// <summary>
    /// Der Betrag in Euro.
    ///
    /// Für neue Buchungen ist er immer größer als null und gleich der Summe der
    /// Zeilen; diese Regeln prüft die API beim Annehmen, nicht diese Klasse. Die
    /// Richtung ergibt sich aus dem Topf der Zeilen oder der Umbuchung, siehe
    /// <see cref="EffectOn"/>.
    /// </summary>
    public required decimal Amount { get; set; }

    /// <summary>Wann die Buchung angelegt wurde.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Wann die Buchung zuletzt geändert wurde.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Sagt, ob eine Buchung mit diesem Datum im angegebenen Planmonat zählen darf: im Monat des
    /// Datums, im Monat davor oder im Monat danach (R7).
    /// </summary>
    public static bool IsPlanMonthAllowed(DateOnly date, int planYear, int planMonth)
    {
        // Wie in Commitment.IsDueIn: Jahr mal 12 plus Monat macht zwei Monate vergleichbar.
        var distance = (planYear * 12 + planMonth) - (date.Year * 12 + date.Month);
        return planMonth is >= 1 and <= 12 && distance is >= -1 and <= 1;
    }

    /// <summary>True, wenn die Buchung eine Umbuchung zwischen zwei eigenen Konten ist.</summary>
    public bool IsTransfer => CounterAccountId is not null;

    /// <summary>
    /// Liefert, um wie viel Euro diese Buchung den Stand des angegebenen Kontos
    /// verändert. Das Vorzeichen steht im Ergebnis, nicht im Betrag:
    /// - Einnahme: +Betrag auf dem Konto der Buchung
    /// - Ausgabe (Grundbedarf, Wünsche, Sparen): -Betrag auf dem Konto der Buchung
    ///   (bei einer aufgeteilten Buchung zählt immer die Gesamtsumme)
    /// - Umbuchung: -Betrag auf dem Von-Konto, +Betrag auf dem Nach-Konto
    /// - jedes andere Konto: 0
    ///
    /// Beispiel: Ausgabe von 42,10 Euro im Topf Grundbedarf auf dem Girokonto
    /// ergibt -42,10 für das Girokonto und 0 für alle anderen.
    ///
    /// Der Kontostand eines Kontos ist dann Startbetrag plus die Summe von
    /// <see cref="EffectOn"/> aller Buchungen, die das Konto berühren.
    ///
    /// Eine Ausgabe oder Einnahme ohne Zeile hat keine Richtung. Sie wird von der
    /// API nie angenommen; kommt sie trotzdem hier an, ist das ein Fehler im
    /// Programm, und die Methode wirft eine <see cref="InvalidOperationException"/>,
    /// statt still falsch zu rechnen.
    /// </summary>
    public decimal EffectOn(Guid accountId)
    {
        if (IsTransfer)
        {
            if (accountId == AccountId)
            {
                return -Amount;
            }
            return accountId == CounterAccountId ? Amount : 0m;
        }

        if (accountId != AccountId)
        {
            return 0m;
        }

        // Alle Zeilen haben dieselbe Richtung (das prüft die API); die erste entscheidet.
        if (Lines.Count == 0)
        {
            throw new InvalidOperationException(
                $"Buchung {Id} hat keine Zeile; ihre Richtung ist nicht bestimmt.");
        }
        return Lines[0].Pot == Pot.Income ? Amount : -Amount;
    }
}
