namespace Duofy.Schemas;

/// <summary>Die Arten eines Eintrags im Bereich Schulden und Kredite, so wie die API sie schickt.</summary>
public static class DebtKinds
{
    /// <summary>Eine Forderung: offene Rechnung, Mahnung oder Inkasso.</summary>
    public const string Claim = "claim";

    /// <summary>Ein Kredit.</summary>
    public const string Loan = "loan";

    public static readonly IReadOnlyList<string> All = [Claim, Loan];
}

/// <summary>Legt eine Schuld oder einen Kredit an (POST /api/v1/debts); mit <c>OwnerId</c> für ein anderes Mitglied, das Anlegen im Bereich Sparen freigegeben hat.</summary>
public sealed record CreateDebtRequest(
    string? Kind,
    string? Name,
    string? Creditor,
    decimal OriginalAmount,
    DateOnly? DueDate = null,
    string? Note = null,
    Guid? OwnerId = null);

/// <summary>Ändert einen Eintrag (PATCH /api/v1/debts/{id}); nur die geschickten Felder ändern sich, die Clear-Schalter leeren ein Feld.</summary>
public sealed record UpdateDebtRequest(
    string? Name = null,
    string? Creditor = null,
    decimal? OriginalAmount = null,
    DateOnly? DueDate = null,
    string? Note = null,
    bool? ClearDueDate = null,
    bool? ClearNote = null);

/// <summary>
/// Ein Eintrag mit dem, was gezahlt und was noch offen ist. <c>Paid</c> ist die Summe der zugeordneten Ausgaben, <c>Open</c>
/// der Ursprungsbetrag minus <c>Paid</c> (nie unter 0); beides wird bei jeder Antwort neu gerechnet.
/// </summary>
public sealed record DebtResponse(
    Guid Id,
    string Kind,
    string Name,
    string Creditor,
    decimal OriginalAmount,
    DateOnly? DueDate,
    string? Note,
    decimal Paid,
    decimal Open,
    bool Settled);

/// <summary>Grenzen der Schulden und Kredite.</summary>
public static class DebtRules
{
    public const int NameMaxLength = 100;

    public const int CreditorMaxLength = 100;

    public const int NoteMaxLength = 200;

    public const decimal MaxAmount = 9_999_999_999.99m;
}
