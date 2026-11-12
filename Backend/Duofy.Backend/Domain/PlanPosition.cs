namespace Duofy.Domain;

/// <summary>
/// Ein Posten: eine Zeile im Monatsplan, zum Beispiel "Miete, 890 Euro, am 1.".
///
/// Ein Posten entsteht aus einem Vertrag, wenn der Monat angelegt wird. Er ist
/// eine Kopie und kein Verweis: Name, Betrag und Zahltag werden in dem Moment
/// aus dem Vertrag übernommen und gehören danach dem Posten.
///
/// Der Grund dafür: Wird die Miete im Oktober erhöht und der Vertrag angepasst,
/// soll der bereits angelegte September weiter die alte Miete zeigen. Würde der
/// Posten nur auf den Vertrag zeigen, würde jede Änderung am Vertrag auch die
/// Vergangenheit umschreiben.
///
/// Einmalige Posten legt die Person von Hand an; sie haben keinen Vertrag
/// (<see cref="CommitmentId"/> ist null) und gelten nur in ihrem Monat.
/// </summary>
public class PlanPosition
{
    /// <summary>Die eindeutige Kennung; der Plan vergibt sie beim Anlegen des Postens.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Der Vertrag, aus dem der Posten stammt; null bei einem einmaligen Posten. Über diese
    /// Kennung findet der Abgleich (<see cref="PlanChanges"/>) heraus, was sich am Vertrag geändert hat.
    /// </summary>
    public Guid? CommitmentId { get; set; }

    /// <summary>Die Bezeichnung, übernommen aus dem Namen des Vertrags.</summary>
    public required string Label { get; set; }

    /// <summary>
    /// Der Betrag, der für diesen Monat eingeplant ist, in Euro. Übernommen aus
    /// dem Betrag des Vertrags. Wie alle Geldbeträge im Projekt ein decimal.
    /// </summary>
    public required decimal AmountPlanned { get; set; }

    /// <summary>
    /// Der Tag des Monats, an dem der Posten fällig ist, also eine Zahl von 1 bis 31.
    ///
    /// Hier steht der tatsächliche Tag in diesem Monat und nicht der Zahltag aus
    /// dem Vertrag. Ein Vertrag mit Zahltag 31 hat im September einen Posten mit
    /// Tag 30, weil der September nur 30 Tage hat.
    /// </summary>
    public required int DueDay { get; set; }

    /// <summary>Die Art, übernommen aus dem Vertrag, zum Beispiel Ausgabe oder Einnahme.</summary>
    public required CommitmentType Type { get; set; }

    /// <summary>Die Kategorie, übernommen aus dem Vertrag oder beim einmaligen Posten gewählt.</summary>
    public Guid CategoryId { get; set; }

    /// <summary>Der Topf, unter dem der Posten im Plan steht und in dessen Quote er zählt.</summary>
    public Pot Pot { get; set; }

    /// <summary>Das zahlende Konto (bei einer Einnahme: das empfangende); null heißt Standardkonto.</summary>
    public Guid? AccountId { get; set; }

    /// <summary>Ein eigenes Zielkonto; dann wird beim Abhaken eine Umbuchung angelegt.</summary>
    public Guid? CounterAccountId { get; set; }

    /// <summary>Durchlaufender Posten: zählt in keiner Quote.</summary>
    public bool PassThrough { get; set; }

    /// <summary>Wie das Geld fließt, übernommen aus dem Vertrag.</summary>
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Other;

    /// <summary>Wann der Posten von Hand abgehakt wurde; null, solange er offen ist. Nie automatisch.</summary>
    public DateTimeOffset? DoneAt { get; set; }

    /// <summary>
    /// True, wenn die Person den Posten im Monat von Hand geändert hat. Der Abgleich mit dem
    /// Vertrag überschreibt ihn dann nur, wenn die Person die Änderung ausdrücklich wählt.
    /// </summary>
    public bool ChangedByHand { get; set; }

    /// <summary>Die Buchung, die beim Abhaken entstanden ist; das Zurücknehmen des Hakens löscht sie wieder.</summary>
    public Guid? TickBookingId { get; set; }

    /// <summary>
    /// Die Kopie dieses Postens im nächsten Monat, wenn er verschoben wurde (Spec 136). Ein verschobener Posten zählt in
    /// seinem Monat nirgends mehr.
    /// </summary>
    public Guid? MovedToPositionId { get; set; }

    /// <summary>Der Posten im Vormonat, aus dem diese Kopie durch Verschieben entstanden ist; null bei allen anderen Posten.</summary>
    public Guid? MovedFromPositionId { get; set; }

    /// <summary>True, wenn der Posten in den nächsten Monat verschoben ist und in diesem Monat nirgends zählt.</summary>
    public bool IsMoved => MovedToPositionId is not null;

    /// <summary>
    /// Verschiebt den Posten: legt die Kopie für den Zielmonat an (Betrag, Vertrag, Topf, Kategorie, Konto und Zahlungsart
    /// gleich, Zahltag auf die Länge des Monats gekürzt) und verknüpft beide. Die Kopie gilt als von Hand geändert, damit
    /// der Vertrag sie nicht überschreibt.
    /// </summary>
    public PlanPosition PostponedCopy(int targetYear, int targetMonth)
    {
        var copy = new PlanPosition
        {
            Id = Guid.NewGuid(),
            CommitmentId = CommitmentId,
            Label = Label,
            AmountPlanned = AmountPlanned,
            DueDay = Math.Min(DueDay, DateTime.DaysInMonth(targetYear, targetMonth)),
            Type = Type,
            CategoryId = CategoryId,
            Pot = Pot,
            AccountId = AccountId,
            CounterAccountId = CounterAccountId,
            PassThrough = PassThrough,
            PaymentMethod = PaymentMethod,
            ChangedByHand = true,
            MovedFromPositionId = Id,
        };
        MovedToPositionId = copy.Id;
        return copy;
    }

    /// <summary>True, wenn der Posten abgehakt ist.</summary>
    public bool IsDone => DoneAt is not null;

    /// <summary>
    /// Ein neuer Posten als Kopie des Vertrags für diesen Monat, mit neuer Kennung.
    /// </summary>
    public static PlanPosition CopyOf(Commitment commitment, int year, int month)
    {
        var position = new PlanPosition
        {
            Id = Guid.NewGuid(),
            Label = commitment.Name,
            AmountPlanned = commitment.AmountIn(year, month),
            Type = commitment.Type,
            DueDay = commitment.EffectiveDueDay(year, month),
        };
        position.TakeOver(commitment, year, month);
        return position;
    }

    /// <summary>
    /// Übernimmt alle Felder des Vertrags, die der Plan kennt. Danach folgt der Posten wieder
    /// dem Vertrag; die Markierung "von Hand geändert" fällt weg. Abgehakt bleibt er.
    /// </summary>
    public void TakeOver(Commitment commitment, int year, int month)
    {
        CommitmentId = commitment.Id;
        Label = commitment.Name;
        AmountPlanned = commitment.AmountIn(year, month);
        Type = commitment.Type;
        DueDay = commitment.EffectiveDueDay(year, month);
        CategoryId = commitment.CategoryId;
        Pot = commitment.Pot;
        AccountId = commitment.AccountId;
        CounterAccountId = commitment.CounterAccountId;
        PassThrough = commitment.PassThrough;
        PaymentMethod = commitment.PaymentMethod;
        ChangedByHand = false;
    }
}
