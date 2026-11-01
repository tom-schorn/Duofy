namespace Duofy.Schemas;

/// <summary>Legt ein Sparziel an (POST /api/v1/savings-goals); mit <c>OwnerId</c> für ein anderes Mitglied, das Anlegen im Bereich Sparen freigegeben hat.</summary>
public sealed record CreateSavingsGoalRequest(
    string? Name,
    decimal? TargetAmount = null,
    DateOnly? TargetDate = null,
    Guid? AccountId = null,
    Guid? OwnerId = null,
    decimal? StartAmount = null);

/// <summary>Ändert ein Sparziel (PATCH /api/v1/savings-goals/{id}); nur die geschickten Felder ändern sich, die Clear-Schalter leeren ein Feld.</summary>
public sealed record UpdateSavingsGoalRequest(
    string? Name = null,
    decimal? TargetAmount = null,
    DateOnly? TargetDate = null,
    Guid? AccountId = null,
    bool? ClearTargetAmount = null,
    bool? ClearTargetDate = null,
    bool? ClearAccountId = null,
    decimal? StartAmount = null);

/// <summary>
/// Ein Sparziel mit seinem Stand. <c>Balance</c> ist die Summe aller Einzahlungen minus aller Entnahmen, die dem Ziel
/// zugeordnet sind, plus <c>StartAmount</c> ("Schon angespart"); sie wird bei jeder Antwort neu gerechnet. <c>Reached</c> heißt: Es gibt einen Zielbetrag und der
/// Stand hat ihn erreicht.
/// </summary>
public sealed record SavingsGoalResponse(
    Guid Id,
    string Name,
    decimal? TargetAmount,
    DateOnly? TargetDate,
    Guid? AccountId,
    decimal Balance,
    bool Reached,
    decimal StartAmount = 0m);

/// <summary>Grenzen der Sparziele.</summary>
public static class SavingsGoalRules
{
    public const int NameMaxLength = 100;

    public const decimal MaxAmount = 9_999_999_999.99m;
}
