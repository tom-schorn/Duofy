using Duofy.Schemas;

namespace Duofy.Shared.Services;

/// <summary>
/// Der Entwurf des Ratenplans im Schulden-Dialog (Issue 137). Er hält die Eingaben des Schritts "Ratenplan", rechnet die
/// Vorschau (Anzahl, letzte Rate, Tag der letzten Rate) mit demselben Code wie die API und baut die Anfrage. Beim Speichern
/// rechnet die API noch einmal genau, auch mit den Raten, die bis zum Änderungsmonat noch nach dem alten Plan fällig sind.
/// </summary>
public sealed class DebtPlanDraft
{
    private DebtPlanDraft()
    {
    }

    /// <summary>True, wenn die Schuld einen Ratenplan haben soll.</summary>
    public bool Enabled { get; set; }

    /// <summary>True, wenn die Schuld schon einen eigenen Ratenplan hat; dann braucht eine Änderung den Monat.</summary>
    public bool HasOwnPlan { get; private init; }

    /// <summary>True, wenn der bestehende Plan entfernt werden soll.</summary>
    public bool Remove { get; set; }

    /// <summary>Die Höhe einer Rate.</summary>
    public decimal? Rate { get; set; }

    /// <summary>Alle wie viele Monate.</summary>
    public int? IntervalMonths { get; set; } = 1;

    /// <summary>
    /// Der Tag der ersten Rate. Bei einem bestehenden Plan ist es der nächste Fälligkeitstag ab dem Monat der Änderung, denn
    /// der neue Plan beginnt dort und die Rechnung kennt nur noch den Rest.
    /// </summary>
    public DateOnly? FirstDueDate { get; set; }

    /// <summary>Das zahlende Konto; leer heißt Standardkonto.</summary>
    public Guid? AccountId { get; set; }

    /// <summary>Die Zahlungsart, ein Schlüssel aus <see cref="PaymentMethods"/>.</summary>
    public string PaymentMethod { get; set; } = PaymentMethods.DirectDebit;

    /// <summary>Die Kategorie; leer heißt die eingebaute Kategorie Schulden.</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>
    /// Die letzte Rate von Hand; leer heißt: Duofy rechnet sie. Ein bestehender Plan lädt sie nur, wenn sie von Hand gesetzt war,
    /// nicht die vom alten Plan gerechnete.
    /// </summary>
    public decimal? LastAmount { get; set; }

    /// <summary>Ab welchem Monat eine Änderung gilt (nur bei bestehendem Plan).</summary>
    public DateOnly? FromMonth { get; set; }

    // Der Plan, wie er beim Öffnen war; daran erkennt der Entwurf, ob etwas geändert wurde.
    private DebtPlanResponse? original;
    private DateOnly? originalFirstDueDate;
    private decimal? originalLastAmount;

    /// <summary>True, wenn bei einem bestehenden Plan etwas von dem geändert wurde, was einen neuen Vertrag braucht.</summary>
    public bool Changed => original is null
        || Rate != original.Rate || IntervalMonths != original.IntervalMonths || FirstDueDate != originalFirstDueDate
        || AccountId != original.AccountId || PaymentMethod != original.PaymentMethod || CategoryId != original.CategoryId
        || LastAmount != originalLastAmount;

    /// <summary>Die erste Rate des neuen Plans: nie vor dem Monat der Änderung, auch wenn dieser später gewählt wurde.</summary>
    public DateOnly? StartDate => FirstDueDate is { } first && HasOwnPlan && FromMonth is { } from && IntervalMonths is >= 1 and <= CommitmentRules.MaxIntervalMonths
        ? NextDueFrom(first, IntervalMonths.Value, new DateOnly(from.Year, from.Month, 1))
        : FirstDueDate;

    /// <summary>Der erste Fälligkeitstag ab dem Monatsersten <paramref name="notBefore"/>, im Abstand von <paramref name="intervalMonths"/> vom Tag <paramref name="first"/>.</summary>
    internal static DateOnly NextDueFrom(DateOnly first, int intervalMonths, DateOnly notBefore)
    {
        if (first >= notBefore)
        {
            return first;
        }
        var months = (notBefore.Year - first.Year) * 12 + notBefore.Month - first.Month;
        var steps = (months + intervalMonths - 1) / intervalMonths;
        return first.AddMonths(steps * intervalMonths);
    }

    /// <summary>True, wenn der Dialog den bestehenden Plan entfernen soll.</summary>
    public bool ClearsPlan => HasOwnPlan && Remove;

    /// <summary>Ein Entwurf für eine Schuld ohne Plan: aus, monatlich, erste Rate am Ersten des nächsten Monats.</summary>
    public static DebtPlanDraft ForNew(DateOnly today) => new()
    {
        FirstDueDate = new DateOnly(today.Year, today.Month, 1).AddMonths(1),
    };

    /// <summary>Ein Entwurf nach dem Stand der Schuld: Ihr eigener Plan ist geladen; ein verknüpfter Vertrag bleibt außen vor.</summary>
    public static DebtPlanDraft From(DebtResponse debt, DateOnly today)
    {
        if (debt.Plan is not { IsOwnPlan: true } plan)
        {
            return ForNew(today);
        }
        var thisMonth = new DateOnly(today.Year, today.Month, 1);
        var fromMonth = plan.FirstDueDate > thisMonth ? plan.FirstDueDate : thisMonth;
        var first = NextDueFrom(plan.FirstDueDate, plan.IntervalMonths, new DateOnly(fromMonth.Year, fromMonth.Month, 1));
        var byHand = plan.LastAmountByHand ? plan.LastAmount : null;
        return new DebtPlanDraft
        {
            Enabled = true,
            HasOwnPlan = true,
            original = plan,
            Rate = plan.Rate,
            IntervalMonths = plan.IntervalMonths,
            originalFirstDueDate = first,
            originalLastAmount = byHand,
            FirstDueDate = first,
            AccountId = plan.AccountId,
            PaymentMethod = plan.PaymentMethod,
            CategoryId = plan.CategoryId,
            LastAmount = byHand,
            FromMonth = fromMonth,
        };
    }

    /// <summary>Die Vorschau für den offenen Betrag am ersten Tag des Plans.</summary>
    public InstallmentPlanResult Preview(decimal open) => InstallmentPlan.Compute(
        open, Rate ?? 0m, IntervalMonths ?? 0, StartDate ?? default, LastAmount);

    /// <summary>
    /// Die Anfrage für den Plan; null, wenn kein Plan gewünscht ist, der bestehende entfernt wird oder er unverändert bleibt
    /// (sonst entstünde bei jedem Speichern ein neuer Vertrag).
    /// </summary>
    public DebtPlanRequest? ToRequest() => !Enabled || Remove || (HasOwnPlan && !Changed) || StartDate is not { } first
        ? null
        : new DebtPlanRequest(
            Rate ?? 0m, IntervalMonths ?? 0, first, AccountId, PaymentMethod, CategoryId, LastAmount,
            HasOwnPlan && FromMonth is { } from ? new DateOnly(from.Year, from.Month, 1) : null);
}
