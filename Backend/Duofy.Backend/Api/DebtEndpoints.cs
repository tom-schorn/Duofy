using Duofy.Schemas;
using Duofy.Data;
using Duofy.Domain;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api;

/// <summary>
/// Schulden und Kredite (/api/v1/debts): lesen, anlegen, ändern, löschen (Issue 31). Ohne <c>owner</c> sind es die eigenen
/// Einträge; die eines anderen Mitglieds nur so weit, wie es im Bereich Sparen freigegeben hat (Spec 68). Ein Eintrag ohne
/// Freigabe sieht aus wie ein unbekannter: 404.
///
/// Das Gezahlte wird bei jeder Antwort aus den zugeordneten Ausgaben gerechnet, nie gespeichert.
/// </summary>
internal static class DebtEndpoints
{
    private const string LogCategory = "Duofy.Api.Debts";

    public static void MapDebtEndpoints(this IEndpointRouteBuilder v1)
    {
        var debts = v1.MapGroup("/debts").RequireAuthorization();
        debts.MapGet("", ListAsync);
        debts.MapPost("", CreateAsync);
        debts.MapPatch("/{id:guid}", UpdateAsync);
        debts.MapDelete("/{id:guid}", DeleteAsync);
    }

    private static IResult NotFound() => ApiErrors.NotFound(ErrorCodes.DebtNotFound);

    /// <summary>Die Zahlungen je Eintrag einer Person: die zugeordneten Ausgaben mit dem Tag der Buchung. Einträge ohne Zahlung fehlen.</summary>
    internal static async Task<Dictionary<Guid, List<DebtPayment>>> PaymentsAsync(DuofyDbContext db, Guid ownerId, CancellationToken cancellationToken)
    {
        var rows = await db.Bookings
            .AsNoTracking()
            .Where(b => b.OwnerId == ownerId)
            .SelectMany(b => b.Lines
                .Where(l => l.DebtId != null)
                .Select(l => new { DebtId = l.DebtId!.Value, b.PaymentDate, l.Amount, l.Pot, IsTransfer = b.CounterAccountId != null }))
            .ToListAsync(cancellationToken);
        return rows
            .Where(r => DebtPayments.IsPayment(r.IsTransfer, r.Pot))
            .GroupBy(r => r.DebtId)
            .ToDictionary(g => g.Key, g => g.Select(r => new DebtPayment(r.PaymentDate, r.Amount)).ToList());
    }

    /// <summary>Die Kennungen der Einträge der Person, die ganz bezahlt sind; der Plan verlangt für sie keine Rate mehr.</summary>
    internal static async Task<HashSet<Guid>> SettledIdsAsync(DuofyDbContext db, Guid ownerId, CancellationToken cancellationToken)
    {
        var debts = await db.Debts.AsNoTracking().Where(d => d.OwnerId == ownerId).ToListAsync(cancellationToken);
        if (debts.Count == 0)
        {
            return [];
        }
        var payments = await PaymentsAsync(db, ownerId, cancellationToken);
        return debts.Where(d => d.OpenOf(PaymentsOf(payments, d.Id)) <= 0m).Select(d => d.Id).ToHashSet();
    }

    private static IReadOnlyCollection<DebtPayment> PaymentsOf(Dictionary<Guid, List<DebtPayment>> payments, Guid debtId) =>
        payments.TryGetValue(debtId, out var list) ? list : [];

    /// <summary>Die Wirkung einer gespeicherten Buchung auf jeden Eintrag: die Summe ihrer zugeordneten Zeilen, soweit sie Zahlungen sind.</summary>
    internal static Dictionary<Guid, decimal> EffectsOf(Booking booking) => booking.Lines
        .Where(l => l.DebtId is not null)
        .GroupBy(l => l.DebtId!.Value)
        .ToDictionary(g => g.Key, g => g.Sum(l => DebtPayments.IsPayment(booking.IsTransfer, l.Pot) ? l.Amount : 0m));

    /// <summary>
    /// Prüft die Zuordnung der Zeilen einer Buchung zu Schulden (Issue 31). Wer Einträge neu nennt, braucht für den Besitzer im
    /// Bereich Sparen mindestens Anlegen, wer die Wirkung auf einen Eintrag ändert oder löst, Ändern; sonst sieht der Eintrag
    /// aus wie ein unbekannter (404). Der Eintrag gehört dem Besitzer der Buchung (sonst 404), und die Zeile muss eine Ausgabe
    /// sein (sonst 400 debt_booking_invalid); sie gehört auch nicht zugleich einem Sparziel.
    /// </summary>
    internal static async Task<IResult?> CheckBookingAsync(
        SaveBookingRequest request, Guid ownerId, IReadOnlyDictionary<Guid, decimal> stored, IAccess access, DuofyDbContext db, CancellationToken cancellationToken)
    {
        var assigned = (request.Lines ?? []).Where(l => l.DebtId is not null).ToList();
        var isTransfer = request.Kind == BookingKinds.Transfer;
        var requested = assigned
            .GroupBy(l => l.DebtId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(l => PotKey.TryParse(l.Pot, out var pot) && DebtPayments.IsPayment(isTransfer, pot) ? l.Amount : 0m));
        if (requested.Count == 0 && stored.Count == 0)
        {
            return null;
        }
        var unchanged = stored.Count == requested.Count && requested.All(e => stored.TryGetValue(e.Key, out var before) && before == e.Value);
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
        var ids = requested.Keys.ToList();
        if (await db.Debts.AsNoTracking().CountAsync(d => d.OwnerId == ownerId && ids.Contains(d.Id), cancellationToken) != ids.Count)
        {
            return NotFound();
        }
        foreach (var line in assigned)
        {
            if (line.SavingsGoalId is not null || !PotKey.TryParse(line.Pot, out var pot) || !DebtPayments.IsPayment(isTransfer, pot))
            {
                return ApiErrors.BadRequest(ErrorCodes.DebtBookingInvalid);
            }
        }
        return null;
    }

    /// <summary>Die Kennungen der Verträge der Person, die an einer Schuld hängen; wer den Bereich Sparen nicht sieht, bekommt deren "nicht mehr fällig" nicht zu sehen.</summary>
    internal static async Task<HashSet<Guid>> LinkedCommitmentIdsAsync(DuofyDbContext db, Guid ownerId, CancellationToken cancellationToken) =>
        (await db.Commitments.AsNoTracking()
            .Where(c => c.OwnerId == ownerId && c.DebtId != null)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken)).ToHashSet();

    /// <summary>
    /// Die Antwort zu einem Eintrag. Der Vertrag dahinter ist der Ratenplan der Schuld oder, wenn es keinen gibt, ein laufender
    /// Vertrag, der mit ihr verknüpft ist; aus ihm folgen nächste Rate und voraussichtliches Ende.
    /// </summary>
    private static DebtResponse ToResponse(
        Debt debt, IReadOnlyCollection<DebtPayment> payments, IReadOnlyList<Commitment> linked, ISet<(Guid CommitmentId, int Year, int Month)> ticked, DateOnly today)
    {
        var paid = payments.Sum(p => p.Amount);
        var open = debt.OpenOf(payments);
        var contract = linked.FirstOrDefault(c => c.Id == debt.PlanCommitmentId)
            ?? linked.Where(c => c.DebtId == debt.Id && c.RunsOn(today)).OrderBy(c => c.NextPaymentOnOrAfter(today) ?? DateOnly.MaxValue).FirstOrDefault();
        DebtInstallmentResponse? next = null;
        DateOnly? end = null;
        DebtPlanResponse? plan = null;
        if (contract is not null)
        {
            if (open > 0m && NextOpenInstallment(contract, ticked, today) is { } due)
            {
                next = new DebtInstallmentResponse(due, contract.AmountIn(due.Year, due.Month));
            }
            end = contract.LastDueDate;
            plan = new DebtPlanResponse(
                contract.Id, contract.Id == debt.PlanCommitmentId, contract.Amount, contract.IntervalMonths, contract.FirstDueDate,
                contract.LastAmount, contract.AccountId, PaymentMethodKeys.ToKey(contract.PaymentMethod), contract.CategoryId, contract.LastAmountByHand);
        }
        return new DebtResponse(
            debt.Id, debt.Kind.ToString().ToLowerInvariant(), debt.Name, debt.Creditor, debt.OriginalAmount, debt.DueDate, debt.Note,
            paid, open, open <= 0m, debt.Reference, debt.AdjustedOpen, debt.AdjustedOpenOn, next, end, plan);
    }

    /// <summary>
    /// Die nächste Rate, die noch zu zahlen ist: Eine Rate, deren Posten im Plan schon abgehakt ist (früh gezahlt), zählt
    /// nicht mehr als nächste.
    /// </summary>
    private static DateOnly? NextOpenInstallment(Commitment contract, ISet<(Guid CommitmentId, int Year, int Month)> ticked, DateOnly today)
    {
        var due = contract.NextPaymentOnOrAfter(today);
        for (var guard = 0; due is { } day && guard < InstallmentPlan.MaxInstallments + 1; guard++)
        {
            if (!ticked.Contains((contract.Id, day.Year, day.Month)))
            {
                return day;
            }
            due = contract.NextPaymentOnOrAfter(day.AddDays(1));
        }
        return null;
    }

    /// <summary>Die abgehakten Posten der Person, die zu einem Vertrag gehören: Vertrag, Jahr und Monat.</summary>
    private static async Task<HashSet<(Guid CommitmentId, int Year, int Month)>> TickedAsync(DuofyDbContext db, Guid ownerId, CancellationToken cancellationToken) =>
        (await db.Plans.AsNoTracking()
            .Where(p => p.OwnerId == ownerId)
            .SelectMany(p => p.Positions
                .Where(x => x.CommitmentId != null && x.DoneAt != null)
                .Select(x => new { CommitmentId = x.CommitmentId!.Value, p.Year, p.Month }))
            .ToListAsync(cancellationToken))
        .Select(x => (x.CommitmentId, x.Year, x.Month))
        .ToHashSet();

    /// <summary>Die Verträge der Person, die an einer Schuld hängen, als Grundlage der Antworten.</summary>
    private static async Task<List<Commitment>> LinkedCommitmentsAsync(DuofyDbContext db, Guid ownerId, CancellationToken cancellationToken) =>
        await db.Commitments.AsNoTracking().Where(c => c.OwnerId == ownerId && c.DebtId != null).ToListAsync(cancellationToken);

    private static async Task<DebtResponse> ResponseAsync(Debt debt, DuofyDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        var payments = await PaymentsAsync(db, debt.OwnerId, cancellationToken);
        return ToResponse(
            debt, PaymentsOf(payments, debt.Id), await LinkedCommitmentsAsync(db, debt.OwnerId, cancellationToken),
            await TickedAsync(db, debt.OwnerId, cancellationToken), Berlin.Today(time));
    }

    private static async Task<IResult> ListAsync(Guid? owner, IAccess access, DuofyDbContext db, TimeProvider time, CancellationToken cancellationToken)
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
        var debts = await db.Debts.AsNoTracking().Where(d => d.OwnerId == ownerId).ToListAsync(cancellationToken);
        var payments = await PaymentsAsync(db, ownerId, cancellationToken);
        var linked = await LinkedCommitmentsAsync(db, ownerId, cancellationToken);
        var ticked = await TickedAsync(db, ownerId, cancellationToken);
        var today = Berlin.Today(time);
        return Results.Ok(debts
            .OrderBy(d => d.DueDate is null)
            .ThenBy(d => d.DueDate)
            .ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(d => ToResponse(d, PaymentsOf(payments, d.Id), linked, ticked, today))
            .ToList());
    }

    private static async Task<IResult> CreateAsync(
        CreateDebtRequest request, IAccess access, DuofyDbContext db, IAccountLookup accounts, ICategoryLookup categories,
        TimeProvider time, ILoggerFactory loggers, CancellationToken cancellationToken)
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
        if (request.Kind is not (DebtKinds.Claim or DebtKinds.Loan))
        {
            return ApiErrors.BadRequest(ErrorCodes.DebtKindInvalid);
        }
        if (Check(request.Name, request.Creditor, request.OriginalAmount, request.Note, request.Reference, request.AdjustedOpen) is { } error)
        {
            return error;
        }

        var now = time.GetUtcNow();
        var debt = new Debt
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Kind = request.Kind == DebtKinds.Loan ? DebtKind.Loan : DebtKind.Claim,
            Name = request.Name!.Trim(),
            Creditor = request.Creditor!.Trim(),
            OriginalAmount = request.OriginalAmount,
            DueDate = request.DueDate,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            Reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim(),
            AdjustedOpen = request.AdjustedOpen,
            AdjustedOpenOn = request.AdjustedOpen is null ? null : request.AdjustedOpenOn ?? Berlin.Today(time),
            CreatedAt = now,
            UpdatedAt = now,
        };

        DebtPlans.Prepared? prepared = null;
        if (request.Plan is { } planRequest)
        {
            var (planError, plan) = await DebtPlans.PrepareAsync(
                planRequest, debt, debt.AdjustedOpen ?? debt.OriginalAmount, accounts, categories, db, cancellationToken);
            if (planError is not null)
            {
                return planError;
            }
            prepared = plan;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Debts.Add(debt);
        await db.SaveChangesAsync(cancellationToken);
        if (prepared is not null)
        {
            // Schuld und Vertrag zeigen aufeinander; die Schuld wird zuerst gespeichert, danach der Vertrag und sein Verweis.
            var contract = DebtPlans.Create(prepared, debt, now);
            db.Commitments.Add(contract);
            await db.SaveChangesAsync(cancellationToken);
            debt.PlanCommitmentId = contract.Id;
            await db.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);

        loggers.CreateLogger(LogCategory).LogInformation("Eintrag {DebtId} für {OwnerId} von {UserId} angelegt.", debt.Id, ownerId, callerId);
        return Results.Created($"/api/v1/debts/{debt.Id}", await ResponseAsync(debt, db, time, cancellationToken));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, UpdateDebtRequest request, IAccess access, DuofyDbContext db, IAccountLookup accounts, ICategoryLookup categories,
        TimeProvider time, CancellationToken cancellationToken)
    {
        if (access.CallerId is null)
        {
            return Results.Unauthorized();
        }
        var debt = await db.Debts.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (debt is null)
        {
            return NotFound();
        }
        if ((await access.RequireAsync(debt.OwnerId, Area.Savings, AccessLevel.Edit, cancellationToken)).Refusal(NotFound) is { } refused)
        {
            return refused;
        }

        var name = request.Name ?? debt.Name;
        var creditor = request.Creditor ?? debt.Creditor;
        var amount = request.OriginalAmount ?? debt.OriginalAmount;
        var note = request.Note ?? (request.ClearNote == true ? null : debt.Note);
        var reference = request.Reference ?? (request.ClearReference == true ? null : debt.Reference);
        if (Check(name, creditor, amount, note, reference, request.AdjustedOpen) is { } error)
        {
            return error;
        }

        var now = time.GetUtcNow();
        var today = Berlin.Today(time);
        debt.Name = name.Trim();
        debt.Creditor = creditor.Trim();
        debt.OriginalAmount = amount;
        debt.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        debt.Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        debt.DueDate = request.DueDate ?? (request.ClearDueDate == true ? null : debt.DueDate);
        if (request.ClearAdjustedOpen == true)
        {
            debt.AdjustedOpen = null;
            debt.AdjustedOpenOn = null;
        }
        else if (request.AdjustedOpen is { } adjusted)
        {
            // Derselbe Betrag ohne neuen Tag ist keine neue Korrektur: Der Tag bleibt, sonst zählten frühere Zahlungen nicht mehr.
            if (adjusted != debt.AdjustedOpen || request.AdjustedOpenOn is not null || debt.AdjustedOpenOn is null)
            {
                debt.AdjustedOpenOn = request.AdjustedOpenOn ?? today;
            }
            debt.AdjustedOpen = adjusted;
        }
        debt.UpdatedAt = now;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (debt.PlanCommitmentId is { } planId && request.Name is not null
            && await db.Commitments.FirstOrDefaultAsync(c => c.Id == planId && c.OwnerId == debt.OwnerId, cancellationToken) is { } planContract
            && planContract.Name != debt.Name)
        {
            // Der Ratenplan heißt wie die Schuld; künftige Posten folgen dem Vertrag.
            planContract.Name = debt.Name;
            planContract.UpdatedAt = now;
        }
        if (request.LinkedCommitmentId is { } linkId
            && await LinkAsync(debt, linkId, access, db, cancellationToken) is { } badLink)
        {
            return badLink;
        }
        if (request.ClearPlan == true)
        {
            await EndPlanAsync(debt, today, now, db, cancellationToken);
        }
        else if (request.Plan is { } planRequest
            && await ChangePlanAsync(debt, planRequest, today, now, accounts, categories, time, db, cancellationToken) is { } badPlan)
        {
            return badPlan;
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(await ResponseAsync(debt, db, time, cancellationToken));
    }

    /// <summary>
    /// Legt den Ratenplan an oder ändert ihn. Ein bestehender Plan braucht den Monat, ab dem die Änderung gilt: der alte Vertrag
    /// endet davor, ein neuer beginnt. Der offene Betrag am ersten Tag des neuen Plans ist der heutige abzüglich der Raten, die
    /// bis dahin noch nach dem alten Plan zu zahlen sind.
    /// </summary>
    private static async Task<IResult?> ChangePlanAsync(
        Debt debt, DebtPlanRequest request, DateOnly today, DateTimeOffset now, IAccountLookup accounts, ICategoryLookup categories,
        TimeProvider time, DuofyDbContext db, CancellationToken cancellationToken)
    {
        var payments = await PaymentsAsync(db, debt.OwnerId, cancellationToken);
        var open = debt.OpenOf(PaymentsOf(payments, debt.Id));
        Commitment? old = debt.PlanCommitmentId is { } oldId
            ? await db.Commitments.FirstOrDefaultAsync(c => c.Id == oldId && c.OwnerId == debt.OwnerId, cancellationToken)
            : null;
        var thisMonth = new DateOnly(today.Year, today.Month, 1);
        DateOnly? fromStart = null;
        if (old is not null)
        {
            if (request.FromMonth is not { } from)
            {
                return ApiErrors.BadRequest(ErrorCodes.DebtPlanFromMonthRequired);
            }
            fromStart = new DateOnly(from.Year, from.Month, 1);
            if (fromStart < thisMonth || request.FirstDueDate < fromStart)
            {
                return ApiErrors.BadRequest(ErrorCodes.DebtPlanStartInvalid);
            }
            open -= await DebtPlans.PlannedUntilAsync(old, thisMonth, fromStart.Value, db, cancellationToken);
        }

        var (error, prepared) = await DebtPlans.PrepareAsync(request, debt, open, accounts, categories, db, cancellationToken);
        if (error is not null)
        {
            return error;
        }
        if (old is not null)
        {
            DebtPlans.EndAt(old, fromStart!.Value.AddDays(-1), now);
        }
        var contract = DebtPlans.Create(prepared!, debt, now);
        db.Commitments.Add(contract);
        await db.SaveChangesAsync(cancellationToken);
        debt.PlanCommitmentId = contract.Id;
        return null;
    }

    /// <summary>Entfernt den Ratenplan: Der Vertrag endet mit der letzten Rate bis zum Ende des laufenden Monats.</summary>
    private static async Task EndPlanAsync(Debt debt, DateOnly today, DateTimeOffset now, DuofyDbContext db, CancellationToken cancellationToken)
    {
        if (debt.PlanCommitmentId is not { } planId)
        {
            return;
        }
        var contract = await db.Commitments.FirstOrDefaultAsync(c => c.Id == planId && c.OwnerId == debt.OwnerId, cancellationToken);
        if (contract is not null)
        {
            DebtPlans.EndAt(contract, new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)), now);
        }
        debt.PlanCommitmentId = null;
    }

    /// <summary>
    /// Verknüpft einen bestehenden Vertrag mit der Schuld (R3): Er ist eine Schuld oder ein gewöhnlicher Vertrag ohne Zielkonto und
    /// Sparziel, gehört der Person und ist nicht schon der Ratenplan einer Schuld. Das braucht auch in den Verträgen Ändern.
    /// </summary>
    private static async Task<IResult?> LinkAsync(Debt debt, Guid commitmentId, IAccess access, DuofyDbContext db, CancellationToken cancellationToken)
    {
        if ((await access.RequireAsync(debt.OwnerId, Area.Commitments, AccessLevel.Edit, cancellationToken)).Outcome != AccessOutcome.Allowed)
        {
            return ApiErrors.NotFound(ErrorCodes.CommitmentNotFound);
        }
        var contract = await db.Commitments.FirstOrDefaultAsync(c => c.Id == commitmentId && c.OwnerId == debt.OwnerId, cancellationToken);
        if (contract is null)
        {
            return ApiErrors.NotFound(ErrorCodes.CommitmentNotFound);
        }
        if (contract.Type is not (CommitmentType.Contract or CommitmentType.Debt) || contract.CounterAccountId is not null
            || contract.SavingsGoalId is not null || (contract.DebtId is { } other && other != debt.Id)
            || await db.Debts.AnyAsync(d => d.PlanCommitmentId == commitmentId, cancellationToken))
        {
            return ApiErrors.BadRequest(ErrorCodes.CommitmentDebtInvalid);
        }
        contract.DebtId = debt.Id;
        contract.UpdatedAt = debt.UpdatedAt;
        return null;
    }

    /// <summary>Löscht einen Eintrag ohne Zuordnungen; mit Zuordnungen antwortet die API 409, damit keine Zahlung still ihren Bezug verliert.</summary>
    private static async Task<IResult> DeleteAsync(Guid id, IAccess access, DuofyDbContext db, TimeProvider time, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        if (access.CallerId is not { } callerId)
        {
            return Results.Unauthorized();
        }
        var debt = await db.Debts.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (debt is null)
        {
            return NotFound();
        }
        if ((await access.RequireAsync(debt.OwnerId, Area.Savings, AccessLevel.Delete, cancellationToken)).Refusal(NotFound) is { } refused)
        {
            return refused;
        }
        // Beendete Ratenpläne der Schuld halten sie nicht fest; sie werden gelöst. Ein laufender Vertrag oder eine Buchung schon.
        var today = Berlin.Today(time);
        var contracts = await db.Commitments.Where(c => c.OwnerId == debt.OwnerId && c.DebtId == id).ToListAsync(cancellationToken);
        var running = contracts.Any(c => !(c.IsDebtPlan && c.LastDueDate is { } end && (end < today || end < c.FirstDueDate)));
        if (running || await db.Bookings.AnyAsync(b => b.OwnerId == debt.OwnerId && b.Lines.Any(l => l.DebtId == id), cancellationToken))
        {
            return Results.Json(new ErrorResponse(ErrorCodes.DebtHasBookings), statusCode: StatusCodes.Status409Conflict);
        }
        foreach (var ended in contracts)
        {
            ended.DebtId = null;
        }

        db.Debts.Remove(debt);
        await db.SaveChangesAsync(cancellationToken);
        loggers.CreateLogger(LogCategory).LogInformation("Eintrag {DebtId} von {OwnerId} durch {UserId} gelöscht.", id, debt.OwnerId, callerId);
        return Results.NoContent();
    }

    /// <summary>Prüft Name, Gläubiger, Betrag und Notiz; liefert die Antwort auf den ersten Fehler, sonst null.</summary>
    private static IResult? Check(string? name, string? creditor, decimal amount, string? note, string? reference = null, decimal? adjustedOpen = null)
    {
        var trimmedName = name?.Trim() ?? "";
        if (trimmedName.Length == 0)
        {
            return ApiErrors.BadRequest(ErrorCodes.DebtNameRequired);
        }
        if (trimmedName.Length > DebtRules.NameMaxLength)
        {
            return ApiErrors.BadRequest(ErrorCodes.DebtNameTooLong);
        }
        var trimmedCreditor = creditor?.Trim() ?? "";
        if (trimmedCreditor.Length == 0)
        {
            return ApiErrors.BadRequest(ErrorCodes.DebtCreditorRequired);
        }
        if (trimmedCreditor.Length > DebtRules.CreditorMaxLength)
        {
            return ApiErrors.BadRequest(ErrorCodes.DebtCreditorTooLong);
        }
        if (amount <= 0 || amount > DebtRules.MaxAmount || decimal.Round(amount, 2) != amount)
        {
            return ApiErrors.BadRequest(ErrorCodes.DebtAmountInvalid);
        }
        if (note is { Length: > DebtRules.NoteMaxLength })
        {
            return ApiErrors.BadRequest(ErrorCodes.DebtNoteTooLong);
        }
        if (reference is { Length: > DebtRules.ReferenceMaxLength })
        {
            return ApiErrors.BadRequest(ErrorCodes.DebtReferenceTooLong);
        }
        if (adjustedOpen is { } open && (open < 0 || open > DebtRules.MaxAmount || decimal.Round(open, 2) != open))
        {
            return ApiErrors.BadRequest(ErrorCodes.DebtAdjustedOpenInvalid);
        }
        return null;
    }
}
