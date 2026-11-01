namespace Duofy.Schemas;

/// <summary>Die Art eines Vertrags in der API: Vertrag, Einnahme, Sparziel oder Schuld.</summary>
public static class CommitmentTypes
{
    public const string Contract = "contract";
    public const string Income = "income";
    public const string SavingsGoal = "savings_goal";
    public const string Debt = "debt";

    /// <summary>Alle Arten in der Reihenfolge, in der die Oberfläche sie zeigt.</summary>
    public static readonly IReadOnlyList<string> All = [Contract, Income, SavingsGoal, Debt];

    /// <summary>
    /// Der Topf, den die Art fest vorgibt: Sparen für Sparziel und Schuld, Einnahmen für eine
    /// Einnahme; null bei einem gewöhnlichen Vertrag. Dieselbe Regel steht in der Domäne; hier
    /// braucht sie die Oberfläche beim Tippen.
    /// </summary>
    public static string? FixedPot(string? type) => type switch
    {
        SavingsGoal or Debt => PotKeys.Savings,
        Income => PotKeys.Income,
        _ => null,
    };
}

/// <summary>Wie das Geld eines Vertrags fließt.</summary>
public static class PaymentMethods
{
    public const string DirectDebit = "direct_debit";
    public const string StandingOrder = "standing_order";
    public const string Transfer = "transfer";
    public const string Withdrawal = "withdrawal";
    public const string Other = "other";

    /// <summary>Alle Zahlungsarten in der Reihenfolge, in der die Oberfläche sie zeigt.</summary>
    public static readonly IReadOnlyList<string> All = [DirectDebit, StandingOrder, Transfer, Withdrawal, Other];
}

/// <summary>
/// Ein neuer Vertrag (POST /api/v1/commitments).
/// </summary>
/// <param name="Type">"contract", "income", "savings_goal" oder "debt", siehe <see cref="CommitmentTypes"/>.</param>
/// <param name="Name">Die Bezeichnung, 1 bis <see cref="CommitmentRules.NameMaxLength"/> Zeichen.</param>
/// <param name="Amount">Der Betrag einer Zahlung in Euro, größer als null, höchstens zwei Nachkommastellen.</param>
/// <param name="IntervalMonths">Alle wie viele Monate gezahlt wird, 1 bis <see cref="CommitmentRules.MaxIntervalMonths"/>.</param>
/// <param name="FirstDueDate">Der Tag der ersten Zahlung.</param>
/// <param name="LastDueDate">Der Tag der letzten Zahlung; leer heißt: ohne Ende.</param>
/// <param name="CategoryId">Eine eingebaute oder eigene Kategorie.</param>
/// <param name="Pot">Der Topf; bei Sparziel, Schuld und Einnahme legt die API ihn selbst fest.</param>
/// <param name="AccountId">Das Konto, von dem gezahlt wird; leer heißt: das Standardkonto.</param>
/// <param name="CounterAccountId">Ein eigenes Zielkonto; dann ist jede Zahlung eine Umbuchung.</param>
/// <param name="PassThrough">Durchlaufender Posten; fehlt der Wert, heißt das nein.</param>
/// <param name="PaymentMethod">Die Zahlungsart, siehe <see cref="PaymentMethods"/>.</param>
/// <param name="TargetAmount">Nur bei einem Sparziel: der Zielbetrag.</param>
/// <param name="TargetDate">Nur bei einem Sparziel: das Zieldatum.</param>
/// <param name="OwnerId">Für wen der Vertrag ist (Spec 68); leer heißt: für die angemeldete Person.</param>
public sealed record CreateCommitmentRequest(
    string? Type,
    string? Name,
    decimal Amount,
    int IntervalMonths,
    DateOnly? FirstDueDate,
    DateOnly? LastDueDate,
    Guid? CategoryId,
    string? Pot,
    Guid? AccountId = null,
    Guid? CounterAccountId = null,
    bool? PassThrough = null,
    string? PaymentMethod = null,
    decimal? TargetAmount = null,
    DateOnly? TargetDate = null,
    Guid? OwnerId = null,
    Guid? SavingsGoalId = null,
    Guid? DebtId = null);

/// <summary>
/// Eine Änderung an einem Vertrag (PATCH /api/v1/commitments/{id}). Jedes Feld ist freiwillig;
/// was fehlt (null), bleibt, wie es ist. Ein freiwilliges Feld wird mit dem passenden
/// Clear-Schalter geleert, zum Beispiel <paramref name="ClearLastDueDate"/> true für "läuft ohne Ende".
/// Ein geschickter Wert hat Vorrang vor seinem Clear-Schalter. Danach gelten für den ganzen
/// Vertrag dieselben Regeln wie beim Anlegen.
/// </summary>
/// <param name="ClearTarget">Leert Zielbetrag und Zieldatum; ein mitgeschickter Zielbetrag oder ein Zieldatum bleibt.</param>
public sealed record UpdateCommitmentRequest(
    string? Type = null,
    string? Name = null,
    decimal? Amount = null,
    int? IntervalMonths = null,
    DateOnly? FirstDueDate = null,
    DateOnly? LastDueDate = null,
    Guid? CategoryId = null,
    string? Pot = null,
    Guid? AccountId = null,
    Guid? CounterAccountId = null,
    bool? PassThrough = null,
    string? PaymentMethod = null,
    decimal? TargetAmount = null,
    DateOnly? TargetDate = null,
    bool? ClearLastDueDate = null,
    bool? ClearAccountId = null,
    bool? ClearCounterAccountId = null,
    bool? ClearTarget = null,
    Guid? SavingsGoalId = null,
    bool? ClearSavingsGoalId = null,
    Guid? DebtId = null,
    bool? ClearDebtId = null);

/// <summary>Ein Vertrag in der Antwort der API.</summary>
/// <param name="AccountId">Das Zahlkonto; null heißt: das Standardkonto am Tag der Zahlung.</param>
/// <param name="NextDueDate">Der Tag der nächsten Zahlung ab heute (Berlin); null, wenn keine mehr kommt.</param>
/// <param name="Running">True, wenn der Vertrag heute noch läuft; beendete blendet die Liste aus.</param>
public sealed record CommitmentResponse(
    Guid Id,
    string Type,
    string Name,
    decimal Amount,
    int IntervalMonths,
    DateOnly FirstDueDate,
    DateOnly? LastDueDate,
    Guid CategoryId,
    string Pot,
    Guid? AccountId,
    Guid? CounterAccountId,
    bool PassThrough,
    string PaymentMethod,
    decimal? TargetAmount,
    DateOnly? TargetDate,
    DateOnly? NextDueDate,
    bool Running,
    Guid? SavingsGoalId = null,
    Guid? DebtId = null);

/// <summary>Die Regeln für Verträge. Die API prüft sie, die Oberfläche kann sie schon beim Tippen anzeigen.</summary>
public static class CommitmentRules
{
    /// <summary>Die größte erlaubte Länge der Bezeichnung.</summary>
    public const int NameMaxLength = 100;

    /// <summary>Der größte Abstand zwischen zwei Zahlungen: zehn Jahre.</summary>
    public const int MaxIntervalMonths = 120;

    /// <summary>Der größte erlaubte Betrag (Spalte numeric(12,2)).</summary>
    public const decimal MaxAmount = 9_999_999_999.99m;
}
