using Duofy.Schemas;

namespace Duofy.Shared.Services;

/// <summary>
/// Der Entwurf eines Vertrags im Dialog. Die Klasse hält die Eingaben und die Regeln des Dialogs,
/// damit die Seite schlicht bleibt und man die Regeln ohne Oberfläche testen kann:
///
/// - Sparziel und Schuld liegen fest im Topf Sparen, eine Einnahme fest im Topf Einnahmen; nur ein
///   gewöhnlicher Vertrag wählt zwischen Grundbedarf und Wünschen.
/// - Zielbetrag und Zieldatum gibt es nur bei einem Sparziel, ein Zielkonto nicht bei einer Einnahme.
/// - Eine Einnahme braucht eine Einnahme-Kategorie, alle anderen eine der übrigen.
///
/// Ob die Eingaben gültig sind, sagt erst die API.
/// </summary>
public sealed class CommitmentDraft
{
    private CommitmentDraft()
    {
    }

    /// <summary>
    /// Die Arten, die der Dialog zur Wahl anbietet: Ausgabe und Einnahme (Spec 17, R4). Sparziel und Schuld
    /// bekommen einen eigenen Bereich; bestehende bleiben gespeichert, werden hier aber nicht mehr angelegt.
    /// </summary>
    public static readonly IReadOnlyList<string> OfferedTypes = [CommitmentTypes.Contract, CommitmentTypes.Income];

    /// <summary>"contract", "income", "savings_goal" oder "debt", siehe <see cref="CommitmentTypes"/>.</summary>
    public string Type { get; private set; } = CommitmentTypes.Contract;

    /// <summary>True, wenn die Art eine der angebotenen ist; false bei einem gespeicherten Sparziel oder einer Schuld.</summary>
    public bool IsOfferedType => OfferedTypes.Contains(Type);

    public string Name { get; set; } = "";

    /// <summary>Der Betrag einer Zahlung; leer, solange nichts eingegeben ist.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Alle wie viele Monate gezahlt wird; leer, solange nichts eingegeben ist.</summary>
    public int? IntervalMonths { get; set; } = 1;

    public DateOnly FirstDueDate { get; set; }

    /// <summary>Die letzte Zahlung; leer heißt: läuft ohne Ende.</summary>
    public DateOnly? LastDueDate { get; set; }

    public Guid? CategoryId { get; set; }

    /// <summary>Der Topf, siehe <see cref="PotKeys"/>; bei festem Topf nicht änderbar.</summary>
    public string Pot { get; private set; } = PotKeys.Needs;

    /// <summary>Das zahlende Konto; leer heißt: das Standardkonto.</summary>
    public Guid? AccountId { get; set; }

    /// <summary>Ein eigenes Zielkonto; leer heißt: keine Umbuchung.</summary>
    public Guid? CounterAccountId { get; set; }

    public bool PassThrough { get; set; }

    /// <summary>Die Zahlungsart, siehe <see cref="PaymentMethods"/>; leer, bis eine gewählt ist.</summary>
    public string PaymentMethod { get; set; } = "";

    public decimal? TargetAmount { get; set; }

    public DateOnly? TargetDate { get; set; }

    /// <summary>Das Sparziel, dem die Rate zufließt (Issue 32); nur bei einer Ausgabe mit Zielkonto.</summary>
    public Guid? SavingsGoalId { get; private set; }

    /// <summary>True, wenn der Vertrag einem Sparziel zufließen darf: eine Ausgabe (keine Einnahme, keine alte Art).</summary>
    public bool OffersSavingsGoal => Type == CommitmentTypes.Contract;

    /// <summary>Die Schuld oder der Kredit, den die Rate zurückzahlt (Issue 31); nur bei einer Ausgabe ohne Zielkonto und ohne Sparziel.</summary>
    public Guid? DebtId { get; private set; }

    /// <summary>Die Schuld, die wirklich gesendet wird: nur mit angebotener Art, ohne Zielkonto und ohne Sparziel, sonst keine.</summary>
    public Guid? EffectiveDebtId => OffersSavingsGoal && CounterAccountId is null && SavingsGoalId is null ? DebtId : null;

    /// <summary>Das Sparziel, das wirklich gesendet wird: nur mit angebotener Art und Zielkonto, sonst keines.</summary>
    public Guid? EffectiveSavingsGoalId => OffersSavingsGoal && CounterAccountId is not null ? SavingsGoalId : null;

    /// <summary>True, wenn ein Sparziel gewählt ist, aber das Zielkonto fehlt, auf das die Rate gehen soll.</summary>
    public bool GoalNeedsTargetAccount => SavingsGoalId is not null && OffersSavingsGoal && CounterAccountId is null;

    /// <summary>True, wenn die Art den Topf festlegt; der Dialog zeigt ihn dann nur an.</summary>
    public bool PotIsFixed => CommitmentTypes.FixedPot(Type) is not null;

    /// <summary>Die Töpfe, zwischen denen die Person wählen kann.</summary>
    public IReadOnlyList<string> OfferedPots => CommitmentTypes.FixedPot(Type) is { } fixedPot
        ? [fixedPot]
        : [PotKeys.Needs, PotKeys.Wants];

    /// <summary>True bei einem Sparziel: Zielbetrag und Zieldatum werden angezeigt.</summary>
    public bool ShowsTarget => Type == CommitmentTypes.SavingsGoal;

    /// <summary>False bei einer Einnahme: Sie hat kein Zielkonto.</summary>
    public bool OffersTargetAccount => Type != CommitmentTypes.Income;

    /// <summary>True bei einer Einnahme: Dann passen nur Einnahme-Kategorien.</summary>
    public bool WantsIncomeCategories => Type == CommitmentTypes.Income;

    /// <summary>True, wenn der Abstand eingegeben ist und zwischen 1 und <see cref="CommitmentRules.MaxIntervalMonths"/> liegt.</summary>
    public bool HasValidInterval => IntervalMonths is >= 1 and <= CommitmentRules.MaxIntervalMonths;

    /// <summary>True, wenn die letzte Zahlung vor der ersten liegt; am selben Tag ist erlaubt.</summary>
    public bool EndsBeforeStart => LastDueDate < FirstDueDate;

    /// <summary>Der Entwurf für einen neuen Vertrag: monatlich, erste Zahlung heute, Topf Grundbedarf.</summary>
    public static CommitmentDraft ForNew(DateOnly today) => new() { FirstDueDate = today };

    /// <summary>Der Entwurf zum Ändern eines gespeicherten Vertrags.</summary>
    public static CommitmentDraft From(CommitmentResponse commitment) => new()
    {
        Type = commitment.Type,
        Name = commitment.Name,
        Amount = commitment.Amount,
        IntervalMonths = commitment.IntervalMonths,
        FirstDueDate = commitment.FirstDueDate,
        LastDueDate = commitment.LastDueDate,
        CategoryId = commitment.CategoryId,
        Pot = commitment.Pot,
        AccountId = commitment.AccountId,
        CounterAccountId = commitment.CounterAccountId,
        PassThrough = commitment.PassThrough,
        PaymentMethod = commitment.PaymentMethod,
        TargetAmount = commitment.TargetAmount,
        TargetDate = commitment.TargetDate,
        SavingsGoalId = commitment.SavingsGoalId,
        DebtId = commitment.DebtId,
    };

    /// <summary>
    /// Setzt die Art und alles, was daraus folgt: den Topf, die Zielfelder, das Zielkonto. Die
    /// Kategorie entfällt, wenn sie zwischen Einnahme und Ausgabe wechselt; sie passt dann nicht mehr.
    /// </summary>
    public void SetType(string type)
    {
        if (type == Type)
        {
            return;
        }

        var wasIncome = WantsIncomeCategories;
        Type = type;

        Pot = CommitmentTypes.FixedPot(type) ?? PotKeys.Needs;
        if (!ShowsTarget)
        {
            TargetAmount = null;
            TargetDate = null;
        }
        if (!OffersTargetAccount)
        {
            CounterAccountId = null;
        }
        if (!OffersSavingsGoal)
        {
            SavingsGoalId = null;
            DebtId = null;
        }
        if (wasIncome != WantsIncomeCategories)
        {
            CategoryId = null;
        }
    }

    /// <summary>Wählt einen der angebotenen Töpfe; ein fester Topf bleibt, wie er ist.</summary>
    /// <summary>
    /// Wählt das Sparziel (null: keines). Hat das Ziel ein Sparkonto und ist noch kein Zielkonto gewählt, wird dieses das
    /// Zielkonto, denn dorthin geht die Rate.
    /// </summary>
    public void ChooseSavingsGoal(Guid? goalId, Guid? goalAccountId)
    {
        SavingsGoalId = goalId;
        if (goalId is not null)
        {
            DebtId = null;
        }
        if (goalId is not null && goalAccountId is { } account && CounterAccountId is null)
        {
            CounterAccountId = account;
        }
    }

    /// <summary>Setzt das Zielkonto; ohne Zielkonto geht keine Rate an ein Sparziel, die Wahl fällt dann weg.</summary>
    public void ChooseCounterAccount(Guid? accountId)
    {
        CounterAccountId = accountId;
        if (accountId is null)
        {
            SavingsGoalId = null;
        }
        else
        {
            DebtId = null;
        }
    }

    /// <summary>Wählt die Schuld (null: keine). Eine Rate an einen Gläubiger geht von außen: Zielkonto und Sparziel fallen dann weg.</summary>
    public void ChooseDebt(Guid? debtId)
    {
        DebtId = debtId;
        if (debtId is not null)
        {
            CounterAccountId = null;
            SavingsGoalId = null;
        }
    }

    public void SetPot(string pot)
    {
        if (OfferedPots.Contains(pot))
        {
            Pot = pot;
        }
    }

    /// <summary>
    /// Wählt die Kategorie. Bei einem gewöhnlichen Vertrag wird ihr vorgeschlagener Topf vorgewählt,
    /// wenn er einer der beiden erlaubten ist; die Person darf ihn danach ändern.
    /// </summary>
    public void ChooseCategory(CategoryResponse? category)
    {
        CategoryId = category?.Id;
        if (category is not null && !PotIsFixed)
        {
            SetPot(category.SuggestedPot);
        }
    }

    /// <summary>
    /// Macht aus dem Entwurf die Anfrage zum Anlegen. Fehlende Zahlen werden zu 0, eine fehlende
    /// Zahlungsart zu null; die API weist sie mit ihrem Fehlercode ab, so gibt es die Regeln nur an einer Stelle.
    /// </summary>
    public CreateCommitmentRequest ToCreateRequest() => new(
        Type,
        Name,
        Amount ?? 0m,
        IntervalMonths ?? 0,
        FirstDueDate,
        LastDueDate,
        CategoryId,
        Pot,
        AccountId,
        CounterAccountId,
        PassThrough,
        NullIfEmpty(PaymentMethod),
        ShowsTarget ? TargetAmount : null,
        ShowsTarget ? TargetDate : null,
        SavingsGoalId: EffectiveSavingsGoalId,
        DebtId: EffectiveDebtId);

    /// <summary>
    /// Macht aus dem Entwurf die Änderung eines gespeicherten Vertrags. Alle Werte gehen mit; ein
    /// freiwilliges Feld, das gespeichert war und jetzt leer ist, wird über seinen Clear-Schalter geleert.
    /// </summary>
    public UpdateCommitmentRequest ToUpdateRequest(CommitmentResponse saved)
    {
        var targetAmount = ShowsTarget ? TargetAmount : null;
        var targetDate = ShowsTarget ? TargetDate : null;
        return new UpdateCommitmentRequest(
            Type,
            Name,
            Amount ?? 0m,
            IntervalMonths ?? 0,
            FirstDueDate,
            LastDueDate,
            CategoryId,
            Pot,
            AccountId,
            CounterAccountId,
            PassThrough,
            NullIfEmpty(PaymentMethod),
            targetAmount,
            targetDate,
            ClearLastDueDate: Cleared(saved.LastDueDate, LastDueDate),
            ClearAccountId: Cleared(saved.AccountId, AccountId),
            ClearCounterAccountId: Cleared(saved.CounterAccountId, CounterAccountId),
            ClearTarget: Cleared(saved.TargetAmount, targetAmount) ?? Cleared(saved.TargetDate, targetDate),
            SavingsGoalId: EffectiveSavingsGoalId,
            ClearSavingsGoalId: Cleared(saved.SavingsGoalId, EffectiveSavingsGoalId),
            DebtId: EffectiveDebtId,
            ClearDebtId: Cleared(saved.DebtId, EffectiveDebtId));
    }

    /// <summary>
    /// Der Tag der nächsten Zahlung an <paramref name="today"/> oder danach, oder null, wenn keine mehr kommt
    /// oder der Abstand nicht gültig ist. Der Dialog zeigt ihn in Schritt 2 (Spec 17, R2).
    ///
    /// Dieselbe Rechnung wie Commitment.NextPaymentOnOrAfter in der Domäne, die die Oberfläche nicht kennt:
    /// fällig in jedem Monat, der den Abstand ab der ersten Zahlung trifft, am Tag der ersten Zahlung oder
    /// am letzten Tag eines kürzeren Monats, und nicht nach der letzten Zahlung.
    /// </summary>
    public DateOnly? NextDueDate(DateOnly today)
    {
        if (!HasValidInterval)
        {
            return null;
        }

        var interval = IntervalMonths!.Value;
        var firstMonth = new DateOnly(FirstDueDate.Year, FirstDueDate.Month, 1);
        var month = new DateOnly(today.Year, today.Month, 1);
        if (month < firstMonth)
        {
            month = firstMonth;
        }

        // Ab dem ersten Monat, der den Abstand trifft, liegt die Zahlung in diesem oder im nächsten Treffer.
        var distance = (month.Year - firstMonth.Year) * 12 + month.Month - firstMonth.Month;
        month = month.AddMonths((interval - distance % interval) % interval);
        for (var i = 0; i < 2; i++, month = month.AddMonths(interval))
        {
            var due = new DateOnly(month.Year, month.Month, Math.Min(FirstDueDate.Day, DateTime.DaysInMonth(month.Year, month.Month)));
            if (LastDueDate is { } last && due > last)
            {
                return null;
            }
            if (due >= today)
            {
                return due;
            }
        }
        return null;
    }

    // True, wenn ein gespeicherter Wert jetzt leer ist; sonst null, damit der Schalter gar nicht mitgeht.
    private static bool? Cleared<T>(T? saved, T? now) where T : struct =>
        saved is not null && now is null ? true : null;

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;
}
