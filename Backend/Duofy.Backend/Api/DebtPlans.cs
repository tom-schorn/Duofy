using Duofy.Data;
using Duofy.Domain;
using Duofy.Schemas;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api;

/// <summary>
/// Der Ratenplan einer Schuld (Issue 137): Duofy legt dafür einen Vertrag der Art Schuld im Topf Sparen an, der zur Schuld
/// gehört, mit kleinerer letzter Rate, wo der Betrag nicht aufgeht. Ein geänderter Plan beendet den alten Vertrag vor dem
/// gewählten Monat und legt einen neuen an; vergangene Monate bleiben, wie sie waren.
///
/// Die Rechte prüft der Aufrufer: wer die Schuld ändern darf, darf ihren Plan ändern.
/// </summary>
internal static class DebtPlans
{
    /// <summary>Ein geprüfter, noch nicht gespeicherter Plan.</summary>
    internal sealed record Prepared(CheckedCommitment Value, InstallmentPlanResult Result, bool LastAmountByHand);

    /// <summary>Rechnet und prüft den Plan. <paramref name="open"/> ist der offene Betrag am ersten Fälligkeitstag.</summary>
    internal static async Task<(IResult? Error, Prepared? Plan)> PrepareAsync(
        DebtPlanRequest plan, Debt debt, decimal open, IAccountLookup accounts, ICategoryLookup categories, DuofyDbContext db, CancellationToken cancellationToken)
    {
        var result = InstallmentPlan.Compute(open, plan.Rate, plan.IntervalMonths, plan.FirstDueDate, plan.LastAmount);
        var code = result.Error switch
        {
            InstallmentPlanError.None => null,
            InstallmentPlanError.NothingOpen => ErrorCodes.DebtPlanNothingOpen,
            InstallmentPlanError.TooManyInstallments => ErrorCodes.DebtPlanTooManyInstallments,
            _ => ErrorCodes.DebtPlanInvalid,
        };
        if (code is not null)
        {
            return (ApiErrors.BadRequest(code), null);
        }

        var input = new CommitmentInput(
            CommitmentTypes.Debt, debt.Name, plan.Rate, plan.IntervalMonths, plan.FirstDueDate, result.LastDueDate,
            plan.CategoryId ?? CategoryCatalog.IdFor("finance.debt"), PotKeys.Savings, plan.AccountId, null, false,
            plan.PaymentMethod ?? PaymentMethods.Other, null, null);
        var defaultAccount = await CommitmentEndpoints.DefaultAccountAsync(db, debt.OwnerId, cancellationToken);
        var check = await CommitmentValidator.CheckAsync(input, debt.OwnerId, defaultAccount, [], accounts, categories, cancellationToken);
        return check.Error is { } error ? (ApiErrors.BadRequest(error), null) : (null, new Prepared(check.Value!, result, plan.LastAmount is not null));
    }

    /// <summary>Legt den Vertrag zum Plan an (noch nicht gespeichert) und merkt ihn als Plan der Schuld.</summary>
    internal static Commitment Create(Prepared prepared, Debt debt, DateTimeOffset now)
    {
        var value = prepared.Value;
        var commitment = new Commitment
        {
            Id = Guid.NewGuid(),
            OwnerId = debt.OwnerId,
            Name = value.Name,
            Amount = value.Amount,
            Type = CommitmentType.Debt,
            IntervalMonths = value.IntervalMonths,
            FirstDueDate = value.FirstDueDate,
            LastDueDate = value.LastDueDate,
            LastAmount = prepared.Result.LastAmount == value.Amount ? null : prepared.Result.LastAmount,
            LastAmountByHand = prepared.LastAmountByHand && prepared.Result.LastAmount != value.Amount,
            CategoryId = value.CategoryId,
            Pot = Pot.Savings,
            AccountId = value.AccountId,
            PaymentMethod = value.PaymentMethod,
            DebtId = debt.Id,
            IsDebtPlan = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        return commitment;
    }

    /// <summary>
    /// Der Betrag der Raten des alten Vertrags, die ab diesem Monat bis vor dem Monat <paramref name="before"/> noch zu zahlen
    /// sind: die fälligen Raten, deren Posten im Plan nicht abgehakt ist. Er mindert den offenen Betrag am ersten Tag des neuen Plans.
    /// </summary>
    internal static async Task<decimal> PlannedUntilAsync(
        Commitment old, DateOnly thisMonth, DateOnly before, DuofyDbContext db, CancellationToken cancellationToken)
    {
        var sum = 0m;
        for (var month = thisMonth; month < before; month = month.AddMonths(1))
        {
            if (!old.IsDueIn(month.Year, month.Month))
            {
                continue;
            }
            var ticked = await db.Plans.AnyAsync(
                p => p.OwnerId == old.OwnerId && p.Year == month.Year && p.Month == month.Month
                    && p.Positions.Any(x => x.CommitmentId == old.Id && x.DoneAt != null),
                cancellationToken);
            if (!ticked)
            {
                sum += old.AmountIn(month.Year, month.Month);
            }
        }
        return sum;
    }

    /// <summary>Beendet den Vertrag mit der letzten Rate bis zu diesem Tag; war er noch nie fällig, wird er sofort beendet.</summary>
    internal static void EndAt(Commitment commitment, DateOnly lastDay, DateTimeOffset now)
    {
        if (commitment.LastDueDate is { } end && end <= lastDay)
        {
            return;
        }
        commitment.LastDueDate = commitment.LastPaymentOnOrBefore(lastDay) ?? commitment.FirstDueDate.AddDays(-1);
        commitment.LastAmount = null;
        commitment.UpdatedAt = now;
    }
}
