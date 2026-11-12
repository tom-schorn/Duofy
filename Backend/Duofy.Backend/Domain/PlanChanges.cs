namespace Duofy.Domain;

/// <summary>Welche Art von Unterschied der Abgleich gefunden hat.</summary>
public enum PlanChangeKind
{
    /// <summary>Der Vertrag hat andere Werte als sein Posten.</summary>
    Changed,

    /// <summary>Der Vertrag ist in diesem Monat fällig, hat aber noch keinen Posten.</summary>
    New,

    /// <summary>Der Posten stammt aus einem Vertrag, der in diesem Monat nicht mehr fällig ist.</summary>
    NoLongerDue,
}

/// <summary>Die Felder eines Postens, die aus dem Vertrag stammen.</summary>
public enum PlanField
{
    Label,
    Amount,
    DueDay,
    Type,
    Category,
    Pot,
    Account,
    CounterAccount,
    PassThrough,
    PaymentMethod,
}

/// <summary>Ein Feld mit dem Wert im Posten (vorher) und im Vertrag (nachher).</summary>
public sealed record PlanFieldChange(PlanField Field, object? Before, object? After);

/// <summary>
/// Ein Unterschied zwischen einem Monat und den Verträgen von heute.
/// </summary>
/// <param name="PositionId">Der betroffene Posten; null bei einem neuen Vertrag.</param>
/// <param name="CommitmentId">Der Vertrag, um den es geht.</param>
/// <param name="Fields">Bei <see cref="PlanChangeKind.Changed"/> die Felder, die sich unterscheiden; sonst leer.</param>
/// <param name="Protected">
/// True, wenn der Posten abgehakt oder von Hand geändert ist. Dann ändert ihn nur eine
/// ausdrückliche Wahl der Person, nie "alle übernehmen".
/// </param>
public sealed record PlanChange(
    PlanChangeKind Kind,
    Guid? PositionId,
    Guid CommitmentId,
    IReadOnlyList<PlanFieldChange> Fields,
    bool Protected);

/// <summary>
/// Der Abgleich eines Monats mit den Verträgen (R2). Ein Monat ist eine Kopie der Verträge vom Tag
/// seiner Anlage; ändert sich danach ein Vertrag, zeigt der Abgleich, was anders ist, und die
/// Person übernimmt die Unterschiede alle oder einzeln. Einmalige Posten haben keinen Vertrag und
/// kommen im Abgleich nie vor.
/// </summary>
public static class PlanChanges
{
    /// <summary>
    /// Vergleicht die Posten des Monats mit den Verträgen der Person. Reihenfolge: zuerst die
    /// Posten in ihrer Reihenfolge im Plan, danach die neuen Verträge nach ihrem Zahltag.
    /// </summary>
    /// <param name="firstOpening">
    /// Der früheste Starttag eines Kontos der Person. Ein Monat, der davor endet, bleibt leer (R1) und
    /// bietet deshalb auch keine neuen Verträge an.
    /// </param>
    public static IReadOnlyList<PlanChange> Compare(Plan plan, IEnumerable<Commitment> commitments, DateOnly? firstOpening = null)
    {
        var byId = commitments.ToDictionary(c => c.Id);
        var changes = new List<PlanChange>();

        foreach (var position in plan.Positions)
        {
            // Eine Kopie aus einem Verschieben gehört zu keinem Vertragsposten des Monats und kommt im Abgleich nie vor (Spec 136, R6).
            if (position.CommitmentId is not { } commitmentId || position.MovedFromPositionId is not null)
            {
                continue;
            }
            var isProtected = position.IsDone || position.ChangedByHand || position.IsMoved;

            if (!byId.TryGetValue(commitmentId, out var commitment) || !commitment.IsDueIn(plan.Year, plan.Month))
            {
                changes.Add(new PlanChange(PlanChangeKind.NoLongerDue, position.Id, commitmentId, [], isProtected));
                continue;
            }

            var fields = Differences(position, PlanPosition.CopyOf(commitment, plan.Year, plan.Month));
            if (fields.Count > 0)
            {
                changes.Add(new PlanChange(PlanChangeKind.Changed, position.Id, commitmentId, fields, isProtected));
            }
        }

        var lastDay = new DateOnly(plan.Year, plan.Month, DateTime.DaysInMonth(plan.Year, plan.Month));
        if (firstOpening is { } opening && lastDay < opening)
        {
            return changes;
        }

        var planned = plan.Positions.Where(p => p.MovedFromPositionId is null).Select(p => p.CommitmentId).OfType<Guid>().ToHashSet();
        changes.AddRange(byId.Values
            .Where(c => !planned.Contains(c.Id) && c.IsDueIn(plan.Year, plan.Month))
            .OrderBy(c => c.EffectiveDueDay(plan.Year, plan.Month))
            .ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(c => new PlanChange(PlanChangeKind.New, null, c.Id, [], false)));

        return changes;
    }

    /// <summary>
    /// Übernimmt einen Unterschied in den Plan: Ein geänderter Vertrag wird neu kopiert, ein neuer
    /// bekommt einen Posten, ein nicht mehr fälliger verliert seinen Posten. Ob ein Posten mit
    /// Buchungen entfernt werden darf, entscheidet der Aufrufer vorher (R10).
    /// </summary>
    public static void Apply(Plan plan, PlanChange change, IEnumerable<Commitment> commitments)
    {
        var commitment = commitments.FirstOrDefault(c => c.Id == change.CommitmentId);
        var position = plan.Positions.FirstOrDefault(p => p.Id == change.PositionId);

        switch (change.Kind)
        {
            case PlanChangeKind.Changed when position is not null && commitment is not null:
                position.TakeOver(commitment, plan.Year, plan.Month);
                break;
            case PlanChangeKind.New when commitment is not null:
                plan.Positions.Add(PlanPosition.CopyOf(commitment, plan.Year, plan.Month));
                break;
            case PlanChangeKind.NoLongerDue when position is not null:
                plan.Positions.Remove(position);
                break;
            default:
                throw new InvalidOperationException(
                    $"Der Unterschied {change.Kind} für Vertrag {change.CommitmentId} passt nicht mehr zum Plan {plan.Id}.");
        }
        plan.SortPositions();
    }

    private static List<PlanFieldChange> Differences(PlanPosition position, PlanPosition contract)
    {
        var fields = new List<PlanFieldChange>();
        void Check<T>(PlanField field, T before, T after)
        {
            if (!EqualityComparer<T>.Default.Equals(before, after))
            {
                fields.Add(new PlanFieldChange(field, before, after));
            }
        }

        Check(PlanField.Label, position.Label, contract.Label);
        Check(PlanField.Amount, position.AmountPlanned, contract.AmountPlanned);
        Check(PlanField.DueDay, position.DueDay, contract.DueDay);
        Check(PlanField.Type, position.Type, contract.Type);
        Check(PlanField.Category, position.CategoryId, contract.CategoryId);
        Check(PlanField.Pot, position.Pot, contract.Pot);
        Check(PlanField.Account, position.AccountId, contract.AccountId);
        Check(PlanField.CounterAccount, position.CounterAccountId, contract.CounterAccountId);
        Check(PlanField.PassThrough, position.PassThrough, contract.PassThrough);
        Check(PlanField.PaymentMethod, position.PaymentMethod, contract.PaymentMethod);
        return fields;
    }
}
