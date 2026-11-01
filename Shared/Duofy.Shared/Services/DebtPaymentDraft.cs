using Duofy.Schemas;

namespace Duofy.Shared.Services;

/// <summary>
/// Die Eingaben des Dialogs "Zahlung eintragen" bei einer Schuld oder einem Kredit (Issue 31). Daraus wird eine Ausgabe, deren
/// Zeile dem Eintrag zugeordnet ist; was danach noch offen ist, rechnet die API. Der Topf ist Grundbedarf oder Wünsche
/// (Standard Grundbedarf); die Kategorie ist die eingebaute "Schulden".
/// </summary>
public sealed record DebtPaymentDraft
{
    private DebtPaymentDraft()
    {
    }

    /// <summary>Der Eintrag, auf den gezahlt wird.</summary>
    public Guid DebtId { get; init; }

    /// <summary>Der Name des Eintrags, für die Notiz der Buchung.</summary>
    public string DebtName { get; init; } = "";

    /// <summary>Die Kategorie der Zahlung (eingebaut: Schulden).</summary>
    public Guid CategoryId { get; init; }

    /// <summary>Der Betrag in Euro; leer, solange nichts eingegeben ist.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Das Konto, von dem gezahlt wird.</summary>
    public Guid? AccountId { get; init; }

    /// <summary>Der Tag der Zahlung.</summary>
    public DateOnly Date { get; init; }

    /// <summary>Der Topf: Grundbedarf oder Wünsche.</summary>
    public string Pot { get; private set; } = PotKeys.Needs;

    public static DebtPaymentDraft ForNew(Guid debtId, string debtName, Guid categoryId, Guid? accountId, DateOnly today) => new()
    {
        DebtId = debtId,
        DebtName = debtName,
        CategoryId = categoryId,
        AccountId = accountId,
        Date = today,
    };

    /// <summary>Setzt den Topf; alles außer Grundbedarf und Wünschen wird übergangen.</summary>
    public void SetPot(string pot)
    {
        if (pot is PotKeys.Needs or PotKeys.Wants)
        {
            Pot = pot;
        }
    }

    /// <summary>True, wenn der Betrag größer als 0 ist und höchstens zwei Nachkommastellen hat.</summary>
    public bool IsAmountValid => Amount is { } amount && amount > 0m && decimal.Round(amount, 2) == amount;

    /// <summary>Die Ausgabe für die API; <paramref name="owner"/> ist die Person, für die die Seite geöffnet wurde (null: die eigene).</summary>
    public SaveBookingRequest ToRequest(Guid? owner)
    {
        var amount = Amount ?? 0m;
        return new SaveBookingRequest(
            BookingKinds.Expense, AccountId ?? Guid.Empty, null, Date, amount, "Zahlung " + DebtName,
            [new SaveBookingLine(amount, Pot, CategoryId, DebtId: DebtId)], OwnerId: owner);
    }
}
