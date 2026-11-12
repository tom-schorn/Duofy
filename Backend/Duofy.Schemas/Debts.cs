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

/// <summary>
/// Der Ratenplan einer Schuld (Issue 137): Duofy macht daraus einen Vertrag der Art Schuld im Topf Sparen, der zur Schuld gehört.
/// Anzahl und letzte Rate folgen aus dem offenen Betrag am ersten Fälligkeitstag.
/// </summary>
/// <param name="Rate">Die Höhe einer Rate in Euro.</param>
/// <param name="IntervalMonths">Alle wie viele Monate eine Rate fällig ist.</param>
/// <param name="FirstDueDate">Der Tag der ersten Rate.</param>
/// <param name="AccountId">Das zahlende Konto; leer heißt: das Standardkonto.</param>
/// <param name="PaymentMethod">Die Zahlungsart, siehe <see cref="PaymentMethods"/>.</param>
/// <param name="CategoryId">Die Kategorie der Raten.</param>
/// <param name="LastAmount">Die letzte Rate von Hand; leer heißt: Duofy rechnet sie aus.</param>
/// <param name="FromMonth">Beim Ändern eines bestehenden Plans: ab welchem Monat (ein Tag in diesem Monat); frühere Monate bleiben.</param>
public sealed record DebtPlanRequest(
    decimal Rate,
    int IntervalMonths,
    DateOnly FirstDueDate,
    Guid? AccountId,
    string? PaymentMethod,
    Guid? CategoryId,
    decimal? LastAmount = null,
    DateOnly? FromMonth = null);

/// <summary>Legt eine Schuld oder einen Kredit an (POST /api/v1/debts); mit <c>OwnerId</c> für ein anderes Mitglied, das Anlegen im Bereich Sparen freigegeben hat.</summary>
public sealed record CreateDebtRequest(
    string? Kind,
    string? Name,
    string? Creditor,
    decimal OriginalAmount,
    DateOnly? DueDate = null,
    string? Note = null,
    Guid? OwnerId = null,
    string? Reference = null,
    DebtPlanRequest? Plan = null,
    decimal? AdjustedOpen = null,
    DateOnly? AdjustedOpenOn = null);

/// <summary>Ändert einen Eintrag (PATCH /api/v1/debts/{id}); nur die geschickten Felder ändern sich, die Clear-Schalter leeren ein Feld.</summary>
public sealed record UpdateDebtRequest(
    string? Name = null,
    string? Creditor = null,
    decimal? OriginalAmount = null,
    DateOnly? DueDate = null,
    string? Note = null,
    bool? ClearDueDate = null,
    bool? ClearNote = null,
    string? Reference = null,
    bool? ClearReference = null,
    DebtPlanRequest? Plan = null,
    bool? ClearPlan = null,
    decimal? AdjustedOpen = null,
    DateOnly? AdjustedOpenOn = null,
    bool? ClearAdjustedOpen = null,
    Guid? LinkedCommitmentId = null);

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
    bool Settled,
    string? Reference = null,
    decimal? AdjustedOpen = null,
    DateOnly? AdjustedOpenOn = null,
    DebtInstallmentResponse? NextInstallment = null,
    DateOnly? ExpectedEnd = null,
    DebtPlanResponse? Plan = null);

/// <summary>Die nächste Rate einer Schuld: Tag und Betrag.</summary>
public sealed record DebtInstallmentResponse(DateOnly Date, decimal Amount);

/// <summary>
/// Der Vertrag hinter einer Schuld. <c>IsOwnPlan</c> ist true beim Ratenplan, den Duofy für die Schuld angelegt hat; false bei einem
/// bestehenden Vertrag, der mit der Schuld verknüpft ist ("verknüpfter Vertrag").
/// </summary>
public sealed record DebtPlanResponse(
    Guid CommitmentId,
    bool IsOwnPlan,
    decimal Rate,
    int IntervalMonths,
    DateOnly FirstDueDate,
    decimal? LastAmount,
    Guid? AccountId,
    string PaymentMethod,
    Guid CategoryId);

/// <summary>Grenzen der Schulden und Kredite.</summary>
public static class DebtRules
{
    public const int NameMaxLength = 100;

    public const int CreditorMaxLength = 100;

    public const int NoteMaxLength = 1000;

    public const int ReferenceMaxLength = 50;

    public const decimal MaxAmount = 9_999_999_999.99m;
}
