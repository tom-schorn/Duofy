namespace Duofy.Domain;

/// <summary>Was für ein Eintrag es ist: eine Forderung an die Person oder ein Kredit, den sie zurückzahlt.</summary>
public enum DebtKind
{
    /// <summary>Eine Forderung: offene Rechnung, Mahnung oder Inkasso.</summary>
    Claim,

    /// <summary>Ein Kredit, der in Raten zurückgezahlt wird.</summary>
    Loan,
}

/// <summary>
/// Eine Schuld oder ein Kredit (Issue 31): Was eine Person jemandem noch schuldet. Der gezahlte Betrag wird nie
/// gespeichert; er ist die Summe der Ausgaben, die dem Eintrag zugeordnet sind (<see cref="BookingLine.DebtId"/>). Offen
/// ist, was vom <see cref="OriginalAmount"/> noch fehlt.
/// </summary>
public class Debt
{
    /// <summary>Die eindeutige Kennung; die API vergibt sie beim Anlegen.</summary>
    public Guid Id { get; set; }

    /// <summary>Die Person, der der Eintrag gehört.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Forderung oder Kredit.</summary>
    public DebtKind Kind { get; set; }

    /// <summary>Wie der Eintrag heißt, 1 bis 100 Zeichen, zum Beispiel "Stromrechnung".</summary>
    public required string Name { get; set; }

    /// <summary>Wem das Geld geschuldet wird, 1 bis 100 Zeichen.</summary>
    public required string Creditor { get; set; }

    /// <summary>Der Betrag, mit dem der Eintrag angelegt wurde (größer als 0, zwei Nachkommastellen).</summary>
    public decimal OriginalAmount { get; set; }

    /// <summary>Bis wann gezahlt werden soll; nur zur Anzeige.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>Eine freie Notiz, höchstens 1000 Zeichen.</summary>
    public string? Note { get; set; }

    /// <summary>Das Aktenzeichen des Gläubigers, höchstens 50 Zeichen (Issue 137).</summary>
    public string? Reference { get; set; }

    /// <summary>
    /// Der Vertrag, den Duofy als Ratenplan für diese Schuld angelegt hat; null, wenn es keinen gibt. Wird der Vertrag
    /// gelöscht, wird das Feld leer.
    /// </summary>
    public Guid? PlanCommitmentId { get; set; }

    /// <summary>Ein von Hand korrigierter offener Betrag; null, wenn der Betrag aus Ursprungsbetrag und Zahlungen folgt.</summary>
    public decimal? AdjustedOpen { get; set; }

    /// <summary>Der Tag, an dem der offene Betrag von Hand gesetzt wurde; Zahlungen danach senken ihn weiter.</summary>
    public DateOnly? AdjustedOpenOn { get; set; }

    /// <summary>Wann der Eintrag angelegt wurde.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Wann der Eintrag zuletzt geändert wurde.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Was noch offen ist: der Ursprungsbetrag minus das Gezahlte, nie unter 0.</summary>
    public decimal OpenOf(decimal paid) => Math.Max(0m, OriginalAmount - paid);

    /// <summary>
    /// Was noch offen ist, aus den einzelnen Zahlungen: ohne Korrektur der Ursprungsbetrag minus alles Gezahlte, mit Korrektur
    /// der korrigierte Betrag minus die Zahlungen nach dem Korrekturtag. Nie unter 0.
    /// </summary>
    public decimal OpenOf(IReadOnlyCollection<DebtPayment> payments)
    {
        if (AdjustedOpen is not { } adjusted)
        {
            return OpenOf(payments.Sum(p => p.Amount));
        }
        var after = payments.Where(p => AdjustedOpenOn is not { } on || p.Date > on).Sum(p => p.Amount);
        return Math.Max(0m, adjusted - after);
    }

    /// <summary>True, wenn alles gezahlt ist.</summary>
    public bool IsSettled(decimal paid) => paid >= OriginalAmount;
}

/// <summary>Eine Zahlung auf eine Schuld: der Tag der Buchung und der Betrag der Zeile.</summary>
public readonly record struct DebtPayment(DateOnly Date, decimal Amount);

/// <summary>
/// Welche Buchungszeilen eine Zahlung auf eine Schuld sein dürfen: Ausgaben, gleich in welchem Topf außer Einnahmen.
/// Umbuchungen und Einnahmen zahlen nichts ab.
/// </summary>
public static class DebtPayments
{
    /// <summary>True, wenn die Zeile eine Zahlung sein darf.</summary>
    public static bool IsPayment(bool isTransfer, Pot linePot) => !isTransfer && linePot != Pot.Income;
}
