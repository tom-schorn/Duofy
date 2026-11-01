using Duofy.Schemas;
using Duofy.Data;
using Duofy.Domain;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api;

/// <summary>
/// Die Sparziele (/api/v1/savings-goals): lesen, anlegen, ändern, löschen (Issue 30). Ohne <c>owner</c> sind es die
/// eigenen Ziele; die eines anderen Mitglieds nur so weit, wie es im Bereich Sparen freigegeben hat (Spec 68), geprüft
/// von <see cref="IAccess"/>. Ein Ziel ohne Freigabe sieht aus wie ein unbekanntes: 404.
///
/// Der Stand eines Ziels wird bei jeder Antwort aus den zugeordneten Buchungszeilen gerechnet, nie gespeichert.
/// </summary>
internal static class SavingsGoalEndpoints
{
    private const string LogCategory = "Duofy.Api.SavingsGoals";

    public static void MapSavingsGoalEndpoints(this IEndpointRouteBuilder v1)
    {
        var goals = v1.MapGroup("/savings-goals").RequireAuthorization();
        goals.MapGet("", ListAsync);
        goals.MapPost("", CreateAsync);
        goals.MapPatch("/{id:guid}", UpdateAsync);
        goals.MapDelete("/{id:guid}", DeleteAsync);
    }

    private static IResult NotFound() => ApiErrors.NotFound(ErrorCodes.SavingsGoalNotFound);

    /// <summary>
    /// Der Stand je Ziel einer Person: die Summe der Einzahlungen minus der Entnahmen aus den zugeordneten
    /// Buchungszeilen (<see cref="SavingsFlow"/>). Ziele ohne Zeile fehlen in der Antwort und stehen bei 0.
    /// </summary>
    internal static async Task<Dictionary<Guid, decimal>> BalancesAsync(DuofyDbContext db, Guid ownerId, CancellationToken cancellationToken)
    {
        var rows = await db.Bookings
            .AsNoTracking()
            .Where(b => b.OwnerId == ownerId)
            .SelectMany(b => b.Lines
                .Where(l => l.SavingsGoalId != null)
                .Select(l => new { GoalId = l.SavingsGoalId!.Value, l.Amount, l.Pot, IsTransfer = b.CounterAccountId != null, b.TargetPot }))
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.GoalId)
            .ToDictionary(
                g => g.Key,
                g => SavingsFlow.BalanceOf(g.Select(r => SavingsFlow.EffectOf(r.IsTransfer, r.TargetPot, r.Pot, r.Amount) ?? 0m)));
    }

    /// <summary>Die Kennungen der Ziele der Person, die ihren Zielbetrag erreicht haben; der Plan verlangt für sie keine Rate mehr.</summary>
    internal static async Task<HashSet<Guid>> ReachedGoalIdsAsync(DuofyDbContext db, Guid ownerId, CancellationToken cancellationToken)
    {
        var goals = await db.SavingsGoals.AsNoTracking()
            .Where(g => g.OwnerId == ownerId && g.TargetAmount != null)
            .ToListAsync(cancellationToken);
        if (goals.Count == 0)
        {
            return [];
        }
        var balances = await BalancesAsync(db, ownerId, cancellationToken);
        return goals.Where(g => g.IsReached(g.StartAmount + balances.GetValueOrDefault(g.Id))).Select(g => g.Id).ToHashSet();
    }

    /// <summary>
    /// Prüft die Zuordnung der Zeilen einer Buchung zu Sparzielen (Issue 30). Wer Ziele neu nennt, braucht für den
    /// Besitzer im Bereich Sparen mindestens Anlegen, wer eine gespeicherte Zuordnung ändert oder löst, Ändern; sonst
    /// sieht das Ziel aus wie ein unbekanntes (404), damit niemand über Buchungen fremde Ziele abtastet oder deren Stand
    /// verstellt. Das Ziel gehört dem Besitzer der Buchung (sonst 404), und die Zeile muss Geld in den Topf Sparen
    /// hinein- oder aus ihm herausbewegen (sonst 400 savings_goal_booking_invalid). Null, wenn alles passt.
    /// </summary>
    /// <param name="stored">Die Wirkung der Buchung auf jedes Ziel bisher (siehe <see cref="EffectsOf"/>); leer bei einer neuen Buchung.</param>
    internal static async Task<IResult?> CheckBookingAsync(
        SaveBookingRequest request, Guid ownerId, IReadOnlyDictionary<Guid, decimal> stored, IAccess access, DuofyDbContext db, CancellationToken cancellationToken)
    {
        var assigned = (request.Lines ?? []).Where(l => l.SavingsGoalId is not null).ToList();
        var requested = assigned.Select(l => l.SavingsGoalId!.Value).ToHashSet();
        var isTransferRequest = request.Kind == BookingKinds.Transfer;
        Pot? requestedTarget = isTransferRequest && PotKey.TryParse(request.TargetPot, out var targetOfRequest) ? targetOfRequest : null;
        var requestedEffects = assigned
            .GroupBy(l => l.SavingsGoalId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(l => PotKey.TryParse(l.Pot, out var linePot)
                ? SavingsFlow.EffectOf(isTransferRequest, requestedTarget, linePot, l.Amount) ?? 0m
                : 0m));
        if (requested.Count == 0 && stored.Count == 0)
        {
            return null;
        }
        // Gleiche Ziele mit gleicher Wirkung ändern keinen Stand; alles andere verlangt Rechte im Bereich Sparen.
        var unchanged = stored.Count == requestedEffects.Count
            && requestedEffects.All(e => stored.TryGetValue(e.Key, out var before) && before == e.Value);
        if (!unchanged)
        {
            var needed = stored.Count > 0 ? AccessLevel.Edit : AccessLevel.Create;
            if ((await access.RequireAsync(ownerId, Area.Savings, needed, cancellationToken)).Outcome != AccessOutcome.Allowed)
            {
                return NotFound();
            }
        }
        if (requested.Count == 0)
        {
            return null;
        }
        var owned = await db.SavingsGoals.AsNoTracking()
            .Where(g => g.OwnerId == ownerId && requested.Contains(g.Id))
            .CountAsync(cancellationToken);
        if (owned != requested.Count)
        {
            return NotFound();
        }

        var isTransfer = request.Kind == BookingKinds.Transfer;
        Pot? targetPot = isTransfer && PotKey.TryParse(request.TargetPot, out var target) ? target : null;
        foreach (var line in assigned)
        {
            if (!PotKey.TryParse(line.Pot, out var pot) || SavingsFlow.EffectOf(isTransfer, targetPot, pot, line.Amount) is null)
            {
                return ApiErrors.BadRequest(ErrorCodes.SavingsGoalBookingInvalid);
            }
        }
        return null;
    }

    /// <summary>Die Wirkung einer gespeicherten Buchung auf jedes Ziel: die Summe der Änderungen ihrer zugeordneten Zeilen.</summary>
    internal static Dictionary<Guid, decimal> EffectsOf(Booking booking) => booking.Lines
        .Where(l => l.SavingsGoalId is not null)
        .GroupBy(l => l.SavingsGoalId!.Value)
        .ToDictionary(g => g.Key, g => g.Sum(l => SavingsFlow.EffectOf(booking.IsTransfer, booking.TargetPot, l.Pot, l.Amount) ?? 0m));

    /// <summary>
    /// Die Verträge der Person, aus denen der Plan seine Posten nimmt. Ein Vertrag, dessen Sparziel erreicht ist, fehlt
    /// hier: Für den Plan ist er nicht mehr fällig, und der Abgleich bietet an, den Posten zu entfernen (Issue 30).
    /// </summary>
    internal static async Task<List<Commitment>> PlanCommitmentsAsync(DuofyDbContext db, Guid ownerId, CancellationToken cancellationToken)
    {
        var commitments = await db.Commitments.AsNoTracking().Where(c => c.OwnerId == ownerId).ToListAsync(cancellationToken);
        // Ein Vertrag, dessen Sparziel erreicht oder dessen Schuld bezahlt ist, wird im Plan nicht mehr verlangt (Issue 30 und 31).
        var reached = commitments.Any(c => c.SavingsGoalId is not null) ? await ReachedGoalIdsAsync(db, ownerId, cancellationToken) : [];
        var settled = commitments.Any(c => c.DebtId is not null) ? await DebtEndpoints.SettledIdsAsync(db, ownerId, cancellationToken) : [];
        return commitments
            .Where(c => (c.SavingsGoalId is not { } goalId || !reached.Contains(goalId)) && (c.DebtId is not { } debtId || !settled.Contains(debtId)))
            .ToList();
    }

    /// <summary>
    /// Die Verträge der Person, die zum Bereich Sparen gehören (alte Sparziele und Schulden). Der Plan-Abgleich lässt ihre
    /// Unterschiede weg, wenn die angemeldete Person den Bereich Sparen nicht ansehen darf; die Verträge selbst bleiben im
    /// Vergleich, sonst sähe ihr Posten wie "nicht mehr fällig" aus.
    /// </summary>
    internal static async Task<HashSet<Guid>> SavingsTypeCommitmentIdsAsync(DuofyDbContext db, Guid ownerId, CancellationToken cancellationToken) =>
        (await db.Commitments.AsNoTracking()
            .Where(c => c.OwnerId == ownerId && (c.Type == CommitmentType.SavingsGoal || c.Type == CommitmentType.Debt))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken)).ToHashSet();

    // Der Stand ist der Startwert plus die Summe der zugeordneten Einzahlungen und Entnahmen (<paramref name="flow"/>).
    /// <summary>
    /// Die Verträge mit Sparziel. Ihr Posten verschwindet aus dem Plan, sobald das Ziel erreicht ist; wer den Bereich Sparen nicht
    /// ansehen darf, bekommt dieses "nicht mehr fällig" nicht zu sehen, denn es verriete den Stand des Ziels.
    /// </summary>
    internal static async Task<HashSet<Guid>> GoalLinkedCommitmentIdsAsync(DuofyDbContext db, Guid ownerId, CancellationToken cancellationToken) =>
        (await db.Commitments.AsNoTracking()
            .Where(c => c.OwnerId == ownerId && (c.SavingsGoalId != null || c.DebtId != null))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken)).ToHashSet();

    private static SavingsGoalResponse ToResponse(SavingsGoal goal, decimal flow, bool seesAccounts = true)
    {
        var balance = goal.StartAmount + flow;
        return new(goal.Id, goal.Name, goal.TargetAmount, goal.TargetDate, seesAccounts ? goal.AccountId : null, balance, goal.IsReached(balance), goal.StartAmount);
    }

    /// <summary>Ob die angemeldete Person die Konten dieses Besitzers ansehen darf; sonst bleibt auch das Konto eines Ziels verborgen.</summary>
    private static async Task<bool> SeesAccountsAsync(IAccess access, Guid ownerId, CancellationToken cancellationToken) =>
        (await access.LevelAsync(ownerId, Area.Accounts, cancellationToken)).Includes(AccessLevel.View);

    private static async Task<IResult> ListAsync(
        Guid? owner,
        IAccess access,
        DuofyDbContext db,
        CancellationToken cancellationToken)
    {
        if (access.CallerId is not { } callerId)
        {
            return Results.Unauthorized();
        }
        var ownerId = owner ?? callerId;
        if ((await access.RequireAsync(ownerId, Area.Savings, AccessLevel.View, cancellationToken)).Refusal(ApiErrors.OwnerNotFound) is { } refused)
        {
            return refused;
        }

        var goals = await db.SavingsGoals.AsNoTracking().Where(g => g.OwnerId == ownerId).ToListAsync(cancellationToken);
        var balances = await BalancesAsync(db, ownerId, cancellationToken);
        var seesAccounts = await SeesAccountsAsync(access, ownerId, cancellationToken);
        return Results.Ok(goals
            .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(g => g.CreatedAt)
            .Select(g => ToResponse(g, balances.GetValueOrDefault(g.Id), seesAccounts))
            .ToList());
    }

    private static async Task<IResult> CreateAsync(
        CreateSavingsGoalRequest request,
        IAccess access,
        DuofyDbContext db,
        IAccountLookup accounts,
        TimeProvider time,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (access.CallerId is not { } callerId)
        {
            return Results.Unauthorized();
        }
        var ownerId = request.OwnerId ?? callerId;
        if ((await access.RequireAsync(ownerId, Area.Savings, AccessLevel.Create, cancellationToken)).Refusal(ApiErrors.OwnerNotFound) is { } refused)
        {
            return refused;
        }

        if (await CheckAsync(request.Name, request.TargetAmount, request.StartAmount, request.AccountId, alreadyStored: false, ownerId, access, accounts, cancellationToken) is { } error)
        {
            return error;
        }

        var now = time.GetUtcNow();
        var goal = new SavingsGoal
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = request.Name!.Trim(),
            TargetAmount = request.TargetAmount,
            StartAmount = request.StartAmount ?? 0m,
            TargetDate = request.TargetDate,
            AccountId = request.AccountId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.SavingsGoals.Add(goal);
        await db.SaveChangesAsync(cancellationToken);

        loggers.CreateLogger(LogCategory).LogInformation("Sparziel {GoalId} für {OwnerId} von {UserId} angelegt.", goal.Id, ownerId, callerId);
        return Results.Created($"/api/v1/savings-goals/{goal.Id}", ToResponse(goal, 0m, await SeesAccountsAsync(access, ownerId, cancellationToken)));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateSavingsGoalRequest request,
        IAccess access,
        DuofyDbContext db,
        IAccountLookup accounts,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (access.CallerId is null)
        {
            return Results.Unauthorized();
        }
        var goal = await db.SavingsGoals.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (goal is null)
        {
            return NotFound();
        }
        if ((await access.RequireAsync(goal.OwnerId, Area.Savings, AccessLevel.Edit, cancellationToken)).Refusal(NotFound) is { } refused)
        {
            return refused;
        }

        var name = request.Name ?? goal.Name;
        var target = request.TargetAmount ?? (request.ClearTargetAmount == true ? null : goal.TargetAmount);
        var accountId = request.AccountId ?? (request.ClearAccountId == true ? null : goal.AccountId);
        var start = request.StartAmount ?? goal.StartAmount;
        if (await CheckAsync(name, target, start, accountId, alreadyStored: accountId == goal.AccountId, goal.OwnerId, access, accounts, cancellationToken) is { } error)
        {
            return error;
        }

        goal.Name = name.Trim();
        goal.TargetAmount = target;
        goal.StartAmount = start;
        goal.TargetDate = request.TargetDate ?? (request.ClearTargetDate == true ? null : goal.TargetDate);
        goal.AccountId = accountId;
        goal.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        var balances = await BalancesAsync(db, goal.OwnerId, cancellationToken);
        return Results.Ok(ToResponse(goal, balances.GetValueOrDefault(goal.Id), await SeesAccountsAsync(access, goal.OwnerId, cancellationToken)));
    }

    /// <summary>
    /// Löscht ein Ziel ohne Zuordnungen. Hängen Buchungszeilen oder Verträge daran, antwortet die API mit 409, damit kein
    /// Stand still verschwindet; die Zuordnung lässt sich vorher an der Buchung oder am Vertrag lösen.
    /// </summary>
    private static async Task<IResult> DeleteAsync(
        Guid id,
        IAccess access,
        DuofyDbContext db,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (access.CallerId is not { } callerId)
        {
            return Results.Unauthorized();
        }
        var goal = await db.SavingsGoals.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (goal is null)
        {
            return NotFound();
        }
        if ((await access.RequireAsync(goal.OwnerId, Area.Savings, AccessLevel.Delete, cancellationToken)).Refusal(NotFound) is { } refused)
        {
            return refused;
        }
        var inUse = await db.Bookings.AnyAsync(b => b.OwnerId == goal.OwnerId && b.Lines.Any(l => l.SavingsGoalId == id), cancellationToken)
            || await db.Commitments.AnyAsync(c => c.OwnerId == goal.OwnerId && c.SavingsGoalId == id, cancellationToken);
        if (inUse)
        {
            return Results.Json(new ErrorResponse(ErrorCodes.SavingsGoalHasBookings), statusCode: StatusCodes.Status409Conflict);
        }

        db.SavingsGoals.Remove(goal);
        await db.SaveChangesAsync(cancellationToken);
        loggers.CreateLogger(LogCategory).LogInformation("Sparziel {GoalId} von {OwnerId} durch {UserId} gelöscht.", id, goal.OwnerId, callerId);
        return Results.NoContent();
    }

    /// <summary>Prüft Name, Zielbetrag und Konto; liefert die Antwort auf den ersten Fehler, sonst null.</summary>
    private static async Task<IResult?> CheckAsync(
        string? name, decimal? target, decimal? start, Guid? accountId, bool alreadyStored, Guid ownerId, IAccess access, IAccountLookup accounts, CancellationToken cancellationToken)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            return ApiErrors.BadRequest(ErrorCodes.SavingsGoalNameRequired);
        }
        if (trimmed.Length > SavingsGoalRules.NameMaxLength)
        {
            return ApiErrors.BadRequest(ErrorCodes.SavingsGoalNameTooLong);
        }
        if (target is { } amount && (amount <= 0 || amount > SavingsGoalRules.MaxAmount || decimal.Round(amount, 2) != amount))
        {
            return ApiErrors.BadRequest(ErrorCodes.SavingsGoalTargetInvalid);
        }
        if (start is { } startAmount && (startAmount < 0 || startAmount > SavingsGoalRules.MaxAmount || decimal.Round(startAmount, 2) != startAmount))
        {
            return ApiErrors.BadRequest(ErrorCodes.SavingsGoalStartInvalid);
        }
        if (accountId is { } id)
        {
            // Wer die Konten der Person nicht ansehen darf, kann auch keines wählen: Es sieht aus wie ein unbekanntes.
            if (!await SeesAccountsAsync(access, ownerId, cancellationToken)
                || await accounts.FindAsync(ownerId, id, cancellationToken) is not { } account)
            {
                return ApiErrors.BadRequest(ErrorCodes.AccountNotFound);
            }
            if (!account.Active && !alreadyStored)
            {
                return ApiErrors.BadRequest(ErrorCodes.AccountClosed);
            }
        }
        return null;
    }
}
