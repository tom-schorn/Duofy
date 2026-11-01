using Duofy.Schemas;

namespace Duofy.Shared.Services;

/// <summary>Ob Geld in ein Sparziel hinein- oder aus ihm herausgeht.</summary>
public enum SavingsMoveKind
{
    /// <summary>Einzahlen: vom anderen Konto auf das Sparkonto, in den Topf Sparen.</summary>
    Deposit,

    /// <summary>Entnehmen: vom Sparkonto auf das andere Konto, aus dem Topf Sparen.</summary>
    Withdrawal,
}

/// <summary>
/// Die Eingaben des Dialogs "Einzahlen" und "Entnehmen" bei einem Sparziel (Issue 30). Daraus wird eine Umbuchung, deren
/// Zeile dem Ziel zugeordnet ist; der Stand des Ziels rechnet danach die API. Der Topf der anderen Seite ist
/// Grundbedarf oder Wünsche (Standard Wünsche); der Topf Sparen ist je nach Richtung Ziel- oder Quelltopf.
/// </summary>
public sealed record SavingsMoveDraft
{
    private SavingsMoveDraft()
    {
    }

    /// <summary>Einzahlen oder Entnehmen.</summary>
    public SavingsMoveKind Kind { get; init; }

    /// <summary>Das Ziel, dem die Zeile zugeordnet wird.</summary>
    public Guid GoalId { get; init; }

    /// <summary>Der Name des Ziels, für die Notiz der Buchung.</summary>
    public string GoalName { get; init; } = "";

    /// <summary>Der Betrag in Euro; leer, solange nichts eingegeben ist.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Das andere Konto: beim Einzahlen das, von dem das Geld kommt, beim Entnehmen das, auf das es geht.</summary>
    public Guid? OtherAccountId { get; init; }

    /// <summary>Das Sparkonto; beim Anlegen das Konto des Ziels.</summary>
    public Guid? SavingsAccountId { get; init; }

    /// <summary>Der Tag der Buchung.</summary>
    public DateOnly Date { get; init; }

    /// <summary>Der Topf der anderen Seite: Grundbedarf oder Wünsche.</summary>
    public string Pot { get; private set; } = PotKeys.Wants;

    public static SavingsMoveDraft ForNew(SavingsMoveKind kind, Guid goalId, string goalName, Guid? savingsAccountId, DateOnly today) => new()
    {
        Kind = kind,
        GoalId = goalId,
        GoalName = goalName,
        SavingsAccountId = savingsAccountId,
        Date = today,
    };

    /// <summary>Setzt den Topf der anderen Seite; alles außer Grundbedarf und Wünschen wird übergangen.</summary>
    public void SetPot(string pot)
    {
        if (pot is PotKeys.Needs or PotKeys.Wants)
        {
            Pot = pot;
        }
    }

    /// <summary>True, wenn der Betrag größer als 0 ist und höchstens zwei Nachkommastellen hat.</summary>
    public bool IsAmountValid => Amount is { } amount && amount > 0m && decimal.Round(amount, 2) == amount;

    /// <summary>True, wenn beide Konten gewählt sind und nicht dasselbe sind.</summary>
    public bool HasAccounts => OtherAccountId is not null && SavingsAccountId is not null && OtherAccountId != SavingsAccountId;

    /// <summary>Die Umbuchung für die API; <paramref name="owner"/> ist die Person, für die die Seite geöffnet wurde (null: die eigene).</summary>
    public SaveBookingRequest ToRequest(Guid? owner)
    {
        var amount = Amount ?? 0m;
        var deposit = Kind == SavingsMoveKind.Deposit;
        var from = deposit ? OtherAccountId : SavingsAccountId;
        var to = deposit ? SavingsAccountId : OtherAccountId;
        var sourcePot = deposit ? Pot : PotKeys.Savings;
        var targetPot = deposit ? PotKeys.Savings : Pot;
        var note = (deposit ? "Einzahlung " : "Entnahme ") + GoalName;
        return new SaveBookingRequest(
            BookingKinds.Transfer, from ?? Guid.Empty, to, Date, amount, note,
            [new SaveBookingLine(amount, sourcePot, null, SavingsGoalId: GoalId)],
            OwnerId: owner, TargetPot: targetPot);
    }
}
