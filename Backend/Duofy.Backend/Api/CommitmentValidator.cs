using Duofy.Schemas;
using Duofy.Domain;

namespace Duofy.Api;

/// <summary>
/// Ein Vertrag, wie er nach dem Anlegen oder Ändern aussehen soll, noch ungeprüft. Beim Anlegen
/// kommt alles aus der Anfrage; beim Ändern ist es der gespeicherte Vertrag mit den Feldern der
/// Anfrage darüber. So gelten für beide Wege dieselben Regeln.
/// </summary>
public sealed record CommitmentInput(
    string? Type,
    string? Name,
    decimal Amount,
    int IntervalMonths,
    DateOnly? FirstDueDate,
    DateOnly? LastDueDate,
    Guid? CategoryId,
    string? Pot,
    Guid? AccountId,
    Guid? CounterAccountId,
    bool PassThrough,
    string? PaymentMethod,
    decimal? TargetAmount,
    DateOnly? TargetDate);

/// <summary>Die geprüften Werte eines Vertrags, fertig zum Speichern.</summary>
public sealed record CheckedCommitment(
    CommitmentType Type,
    string Name,
    decimal Amount,
    int IntervalMonths,
    DateOnly FirstDueDate,
    DateOnly? LastDueDate,
    Guid CategoryId,
    Pot Pot,
    Guid? AccountId,
    Guid? CounterAccountId,
    bool PassThrough,
    PaymentMethod PaymentMethod,
    decimal? TargetAmount,
    DateOnly? TargetDate);

/// <summary>Das Ergebnis der Prüfung: entweder ein Fehlercode (Antwort 400) oder die geprüften Werte.</summary>
public readonly record struct CommitmentCheck(string? Error, CheckedCommitment? Value)
{
    public static CommitmentCheck Invalid(string code) => new(code, null);
}

/// <summary>
/// Die Regeln für das Anlegen und Ändern eines Vertrags (Spec 64, R1 bis R7). Die Prüfung geht
/// von außen nach innen und liefert den ersten Fehler:
/// 1. Form: Art, Name, Betrag, Abstand, erste Zahlung, Zahlungsart, Kategorie, Topf, Sparziel-Felder,
///    Zielkonto. Das braucht keine Daten.
/// 2. Konten: gibt es sie bei der Person, sind sie noch offen, unterscheidet sich das Zielkonto
///    vom zahlenden Konto (oder vom Standardkonto, wenn keines genannt ist).
/// 3. Kategorie: gibt es sie für die Person, passt sie zur Art.
///
/// Fremde Kennungen kommen erst nach der Form dran und sehen aus wie unbekannte. So verrät keine
/// Antwort etwas über Konten und Kategorien anderer Personen.
/// </summary>
public static class CommitmentValidator
{
    /// <param name="input">Der Vertrag, wie er gespeichert werden soll.</param>
    /// <param name="ownerId">Die angemeldete Person.</param>
    /// <param name="defaultAccountId">Das heutige Standardkonto der Person, falls es eines gibt.</param>
    /// <param name="storedAccountIds">
    /// Beim Ändern die Konten, die der Vertrag schon hat. Ein Konto, das erst nach dem Anlegen
    /// aufgelöst wurde, hindert dann nicht daran, zum Beispiel den Namen zu ändern.
    /// </param>
    public static async Task<CommitmentCheck> CheckAsync(
        CommitmentInput input,
        Guid ownerId,
        Guid? defaultAccountId,
        IReadOnlyCollection<Guid> storedAccountIds,
        IAccountLookup accounts,
        ICategoryLookup categories,
        CancellationToken cancellationToken)
    {
        if (!CommitmentTypeKey.TryParse(input.Type, out var type))
        {
            return CommitmentCheck.Invalid(ErrorCodes.CommitmentTypeInvalid);
        }
        var name = (input.Name ?? "").Trim();
        if (name.Length == 0)
        {
            return CommitmentCheck.Invalid(ErrorCodes.CommitmentNameRequired);
        }
        if (name.Length > CommitmentRules.NameMaxLength)
        {
            return CommitmentCheck.Invalid(ErrorCodes.CommitmentNameTooLong);
        }
        if (!IsValidAmount(input.Amount))
        {
            return CommitmentCheck.Invalid(ErrorCodes.AmountInvalid);
        }
        if (input.IntervalMonths is < 1 or > CommitmentRules.MaxIntervalMonths)
        {
            return CommitmentCheck.Invalid(ErrorCodes.CommitmentIntervalInvalid);
        }
        if (input.FirstDueDate is not { } firstDueDate)
        {
            return CommitmentCheck.Invalid(ErrorCodes.CommitmentFirstDueDateRequired);
        }
        if (!PaymentMethodKeys.TryParse(input.PaymentMethod, out var paymentMethod))
        {
            return CommitmentCheck.Invalid(ErrorCodes.PaymentMethodInvalid);
        }
        if (input.CategoryId is not { } categoryId)
        {
            return CommitmentCheck.Invalid(ErrorCodes.CategoryRequired);
        }
        if (CheckPot(type, input.Pot, out var pot) is { } potError)
        {
            return CommitmentCheck.Invalid(potError);
        }
        if (CheckTarget(type, input) is { } targetError)
        {
            return CommitmentCheck.Invalid(targetError);
        }
        if (input.CounterAccountId is not null && type == CommitmentType.Income)
        {
            return CommitmentCheck.Invalid(ErrorCodes.CommitmentTargetInvalid);
        }

        foreach (var accountId in new[] { input.AccountId, input.CounterAccountId })
        {
            if (accountId is { } id
                && await CheckAccountAsync(id, ownerId, storedAccountIds.Contains(id), accounts, cancellationToken) is { } accountError)
            {
                return CommitmentCheck.Invalid(accountError);
            }
        }
        // Ohne zahlendes Konto zahlt das Standardkonto; auch davon muss das Zielkonto verschieden sein.
        if (input.CounterAccountId is { } counter && counter == (input.AccountId ?? defaultAccountId))
        {
            return CommitmentCheck.Invalid(ErrorCodes.CommitmentTargetSameAccount);
        }

        if (await categories.FindAsync(ownerId, categoryId, cancellationToken) is not { } category)
        {
            return CommitmentCheck.Invalid(ErrorCodes.CategoryNotFound);
        }
        // Wie bei Buchungen: Einnahmen nur mit Einnahme-Kategorien, alles andere nur mit den übrigen.
        if (category.IsIncome != (type == CommitmentType.Income))
        {
            return CommitmentCheck.Invalid(ErrorCodes.CommitmentCategoryInvalid);
        }

        return new CommitmentCheck(null, new CheckedCommitment(
            type, name, input.Amount, input.IntervalMonths, firstDueDate, input.LastDueDate, categoryId, pot,
            input.AccountId, input.CounterAccountId, input.PassThrough, paymentMethod, input.TargetAmount, input.TargetDate));
    }

    /// <summary>
    /// Sparziel und Schuld liegen immer im Topf Sparen, eine Einnahme immer im Topf Einnahmen;
    /// was geschickt wurde, zählt dann nicht. Ein gewöhnlicher Vertrag braucht Grundbedarf oder Wünsche.
    /// </summary>
    private static string? CheckPot(CommitmentType type, string? sent, out Pot pot)
    {
        if (Commitment.FixedPotFor(type) is { } fixedPot)
        {
            pot = fixedPot;
            return null;
        }
        if (sent is null)
        {
            pot = default;
            return ErrorCodes.PotRequired;
        }
        if (!PotKey.TryParse(sent, out pot) || pot is not (Pot.Needs or Pot.Wants))
        {
            return ErrorCodes.CommitmentPotInvalid;
        }
        return null;
    }

    /// <summary>Zielbetrag und Zieldatum gibt es nur bei einem Sparziel, beide freiwillig.</summary>
    private static string? CheckTarget(CommitmentType type, CommitmentInput input)
    {
        if (input.TargetAmount is null && input.TargetDate is null)
        {
            return null;
        }
        if (type != CommitmentType.SavingsGoal)
        {
            return ErrorCodes.CommitmentTargetOnlyForSavingsGoal;
        }
        return input.TargetAmount is { } target && !IsValidAmount(target) ? ErrorCodes.CommitmentTargetAmountInvalid : null;
    }

    /// <summary>Das Konto muss der Person gehören und offen sein; ein schon gespeichertes darf inzwischen aufgelöst sein.</summary>
    private static async Task<string?> CheckAccountAsync(
        Guid accountId, Guid ownerId, bool alreadyStored, IAccountLookup accounts, CancellationToken cancellationToken)
    {
        if (await accounts.FindAsync(ownerId, accountId, cancellationToken) is not { } account)
        {
            return ErrorCodes.AccountNotFound;
        }
        return account.Active || alreadyStored ? null : ErrorCodes.AccountClosed;
    }

    /// <summary>Größer als null, höchstens der größte Betrag der Spalte und höchstens zwei Nachkommastellen.</summary>
    private static bool IsValidAmount(decimal amount) =>
        amount > 0 && amount <= CommitmentRules.MaxAmount && decimal.Round(amount, 2) == amount;
}

/// <summary>
/// Übersetzt einen <see cref="CommitmentType"/> in seinen Schlüssel in der API ("savings_goal") und
/// zurück. Es gilt genau diese Schreibweise.
/// </summary>
internal static class CommitmentTypeKey
{
    public static string ToKey(CommitmentType type) => type switch
    {
        CommitmentType.Contract => CommitmentTypes.Contract,
        CommitmentType.Income => CommitmentTypes.Income,
        CommitmentType.SavingsGoal => CommitmentTypes.SavingsGoal,
        CommitmentType.Debt => CommitmentTypes.Debt,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unbekannte Vertragsart."),
    };

    public static bool TryParse(string? key, out CommitmentType type)
    {
        foreach (var candidate in Enum.GetValues<CommitmentType>())
        {
            if (ToKey(candidate) == key)
            {
                type = candidate;
                return true;
            }
        }
        type = default;
        return false;
    }
}
