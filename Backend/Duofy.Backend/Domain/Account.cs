namespace Duofy.Domain;

/// <summary>
/// Ein Konto im Haushaltsbuch: etwas, auf dem Geld liegt und dessen Stand sich
/// durch Buchungen verändert, zum Beispiel ein Girokonto, ein Sparkonto oder
/// das Bargeld im Portemonnaie.
///
/// Der Kontostand wird nirgends gespeichert. Er wird jedes Mal aus dem
/// Startbetrag und den Buchungen ausgerechnet, siehe <see cref="Balance"/>.
/// Das ist Absicht: Ein gespeicherter Stand müsste bei jeder neuen, geänderten
/// oder gelöschten Buchung mitgepflegt werden. Wird das ein einziges Mal
/// vergessen, stimmt er nicht mehr, und niemand merkt es. So gibt es nur eine
/// Wahrheit, nämlich die Buchungen.
/// </summary>
public class Account
{
    /// <summary>Die eindeutige Kennung; die API vergibt sie beim Anlegen.</summary>
    public Guid Id { get; set; }

    /// <summary>Die Person, der das Konto gehört. Nur sie sieht und ändert es.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Die Bezeichnung, unter der das Konto angezeigt wird, 1 bis 100 Zeichen, zum Beispiel "Giro".</summary>
    public required string Name { get; set; }

    /// <summary>Girokonto, Sparkonto oder Bargeld.</summary>
    public required AccountType Type { get; set; }

    /// <summary>
    /// Der Betrag in Euro, der am Ende des Starttages auf dem Konto war,
    /// siehe <see cref="OpeningDate"/>.
    ///
    /// Wer mitten im Jahr mit Duofy anfängt, trägt hier den heutigen Kontostand
    /// ein und muss ältere Bewegungen nicht nachtragen. Ein überzogenes Konto
    /// hat einen negativen Startbetrag.
    /// </summary>
    public required decimal OpeningBalance { get; set; }

    /// <summary>
    /// Der Tag, für den der Startbetrag gilt. Buchungen an diesem Tag und davor
    /// stecken schon im Startbetrag.
    ///
    /// Startbetrag und Starttag gehören zusammen. Ohne den Tag wüsste man nicht,
    /// welche Buchungen im Startbetrag schon enthalten sind und welche noch
    /// dazugerechnet werden müssen.
    /// </summary>
    public required DateOnly OpeningDate { get; set; }

    /// <summary>
    /// Sagt, ob das Geld auf dem Konto ausgegeben werden darf (true) oder zurückgelegt ist
    /// (false). Die Art schlägt den Wert vor (<see cref="CountsAsAvailableFor"/>), die Person
    /// darf ihn ändern.
    /// </summary>
    public bool CountsAsAvailable { get; set; }

    /// <summary>Die IBAN ohne Leerzeichen und in Großbuchstaben, siehe <see cref="Duofy.Domain.Iban"/>; null, wenn keine eingetragen ist.</summary>
    public string? Iban { get; set; }

    /// <summary>
    /// Das Symbol, das die Person für das Konto gewählt hat (Name aus <see cref="Duofy.Schemas.AccountIcons"/>); null, solange sie
    /// keines gewählt hat. Dann zeigt das Konto das Standardsymbol seiner Art. Bestehende Konten sind so ohne Änderung der Daten versorgt.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// True beim Standardkonto: Es ist beim Buchen vorgewählt. Eine Person hat höchstens
    /// ein Standardkonto, und es ist immer ein aktives.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Sagt, ob das Konto noch benutzt wird.
    ///
    /// Ein aufgelöstes Konto wird nicht gelöscht, sondern auf "nicht aktiv"
    /// gesetzt. Es verschwindet dann aus der Auswahl für neue Buchungen, seine
    /// alten Buchungen und sein Stand bleiben aber erhalten. Würde man es
    /// löschen, ginge die Vergangenheit mit verloren.
    /// </summary>
    public required bool Active { get; set; }

    /// <summary>Wann das Konto angelegt wurde.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Wann das Konto zuletzt geändert wurde.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Die Buchungen, die dieses Konto berühren, in beliebiger Reihenfolge: seine Ausgaben
    /// und Einnahmen und die Umbuchungen von und zu ihm. Eine Umbuchung steht also bei
    /// beiden Konten. Die Liste wird nicht gespeichert, sondern für die Rechnung gefüllt.
    /// </summary>
    public List<Booking> Bookings { get; set; } = [];

    /// <summary>
    /// Der Vorschlag für <see cref="CountsAsAvailable"/>: Girokonto und Bargeld ja, Sparkonto nein.
    /// </summary>
    public static bool CountsAsAvailableFor(AccountType type) => type != AccountType.Savings;

    /// <summary>
    /// Liefert den Kontostand in Euro am Ende des angegebenen Tages.
    ///
    /// Gerechnet wird: Startbetrag plus die Wirkung aller Buchungen nach dem Starttag
    /// bis einschließlich des angegebenen Tages. Die Wirkung einer Buchung liefert
    /// <see cref="Booking.EffectOn"/>: Einnahmen und eingehende Umbuchungen erhöhen den
    /// Stand, Ausgaben und abgehende Umbuchungen senken ihn.
    ///
    /// Beispiel: Start am 1. Oktober mit 1.240,50 Euro, am 3. Oktober ein Einkauf
    /// über 54,20 Euro, am 28. Oktober 2.400 Euro Gehalt. Der Stand am 15. Oktober
    /// ist 1.186,30 Euro, am 31. Oktober 3.586,30 Euro.
    ///
    /// Drei Grenzfälle:
    /// - Eine Buchung am Starttag zählt nicht. Der Startbetrag ist der Stand am Ende
    ///   dieses Tages; sie steckt schon darin.
    /// - Eine Buchung vor dem Starttag zählt aus demselben Grund nicht.
    /// - Eine Buchung nach dem angegebenen Tag zählt nicht. Ein Gehalt, das für
    ///   den 28. eingetragen ist, ist am 20. noch nicht auf dem Konto.
    ///
    /// Der Tag wird von außen übergeben und nicht in der Methode aus der Uhr
    /// gelesen. So liefert derselbe Aufruf immer dasselbe Ergebnis, egal wann er
    /// läuft. Für den aktuellen Stand übergibt man den heutigen Tag.
    /// </summary>
    public decimal Balance(DateOnly day)
    {
        return OpeningBalance + Bookings
            .Where(b => b.PaymentDate > OpeningDate && b.PaymentDate <= day)
            .Sum(b => b.EffectOn(Id));
    }
}
