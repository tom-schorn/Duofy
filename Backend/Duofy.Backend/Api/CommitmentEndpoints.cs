using Duofy.Schemas;
using Duofy.Data;
using Duofy.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api;

/// <summary>
/// Die Verträge (/api/v1/commitments): lesen, anlegen, ändern und beenden. Löschen gibt es nicht;
/// ein Vertrag endet mit seiner letzten Zahlung, so bleiben alte Monate erklärbar.
///
/// Die Regeln stehen im <see cref="CommitmentValidator"/>. Ohne <c>owner</c> sind es die eigenen Verträge; die
/// eines anderen Mitglieds nur so weit, wie es im Bereich Verträge freigegeben hat (Spec 68), geprüft von
/// <see cref="IAccess"/>. Ein Vertrag ohne Freigabe sieht aus wie ein unbekannter: 404 mit dem Code
/// commitment_not_found. Beenden zählt als Löschen.
/// </summary>
internal static class CommitmentEndpoints
{
    private const string LogCategory = "Duofy.Api.Commitments";

    public static void MapCommitmentEndpoints(this IEndpointRouteBuilder v1)
    {
        var commitments = v1.MapGroup("/commitments").RequireAuthorization();
        commitments.MapGet("", ListAsync);
        commitments.MapPost("", CreateAsync);
        commitments.MapPatch("/{id:guid}", UpdateAsync);
        commitments.MapPost("/{id:guid}/end", EndAsync);
    }

    private static IResult NotFound() =>
        Results.Json(new ErrorResponse(ErrorCodes.CommitmentNotFound), statusCode: StatusCodes.Status404NotFound);

    private static CommitmentResponse ToResponse(Commitment commitment, DateOnly today) => new(
        commitment.Id,
        CommitmentTypeKey.ToKey(commitment.Type),
        commitment.Name,
        commitment.Amount,
        commitment.IntervalMonths,
        commitment.FirstDueDate,
        commitment.LastDueDate,
        commitment.CategoryId,
        PotKey.ToKey(commitment.Pot),
        commitment.AccountId,
        commitment.CounterAccountId,
        commitment.PassThrough,
        PaymentMethodKeys.ToKey(commitment.PaymentMethod),
        commitment.TargetAmount,
        commitment.TargetDate,
        commitment.NextPaymentOnOrAfter(today),
        Runs(commitment, today));

    /// <summary>
    /// Ob der Vertrag heute läuft (R8). Ein Vertrag, der vor seiner ersten Zahlung beendet wurde (letzte Zahlung
    /// vor der ersten), ist sofort beendet, auch wenn die erste Zahlung noch in der Zukunft liegt.
    /// </summary>
    private static bool Runs(Commitment commitment, DateOnly today) =>
        commitment.RunsOn(today) && (commitment.LastDueDate is null || commitment.LastDueDate >= commitment.FirstDueDate);

    /// <summary>
    /// Die laufenden Verträge der Person (R10), mit <c>?includeEnded=true</c> auch die beendeten.
    /// Sortiert nach der nächsten Zahlung, dann nach Namen; beendete ohne nächste Zahlung zuletzt. Mit
    /// <c>?owner=</c> die Verträge dieser Person, wenn sie Ansehen freigegeben hat.
    /// </summary>
    private static async Task<IResult> ListAsync(
        bool? includeEnded,
        Guid? owner,
        IAccess access,
        DuofyDbContext db,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (access.CallerId is not { } callerId)
        {
            return Results.Unauthorized();
        }
        var ownerId = owner ?? callerId;
        if ((await access.RequireAsync(ownerId, Area.Commitments, AccessLevel.View, cancellationToken)).Refusal(ApiErrors.OwnerNotFound) is { } refused)
        {
            return refused;
        }

        var today = Berlin.Today(time);
        var commitments = await db.Commitments
            .AsNoTracking()
            .Where(c => c.OwnerId == ownerId)
            // Dieselbe Regel wie in Runs: Wer vor der ersten Zahlung beendet wurde, fehlt sofort.
            .Where(c => includeEnded == true || c.LastDueDate == null || (c.LastDueDate >= today && c.LastDueDate >= c.FirstDueDate))
            .ToListAsync(cancellationToken);

        var sorted = commitments
            .Select(c => ToResponse(c, today))
            .OrderBy(c => c.NextDueDate is null)
            .ThenBy(c => c.NextDueDate)
            .ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return Results.Ok(sorted);
    }

    /// <summary>Legt einen Vertrag an, mit <c>ownerId</c> für ein anderes Mitglied, das Anlegen freigegeben hat.</summary>
    private static async Task<IResult> CreateAsync(
        CreateCommitmentRequest request,
        IAccess access,
        DuofyDbContext db,
        IAccountLookup accounts,
        ICategoryLookup categories,
        TimeProvider time,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (access.CallerId is not { } callerId)
        {
            return Results.Unauthorized();
        }
        var ownerId = request.OwnerId ?? callerId;
        if ((await access.RequireAsync(ownerId, Area.Commitments, AccessLevel.Create, cancellationToken)).Refusal(ApiErrors.OwnerNotFound) is { } refused)
        {
            return refused;
        }

        var input = new CommitmentInput(
            request.Type, request.Name, request.Amount, request.IntervalMonths, request.FirstDueDate, request.LastDueDate,
            request.CategoryId, request.Pot, request.AccountId, request.CounterAccountId, request.PassThrough ?? false,
            request.PaymentMethod, request.TargetAmount, request.TargetDate);
        var check = await CommitmentValidator.CheckAsync(
            input, ownerId, await DefaultAccountAsync(db, ownerId, cancellationToken), [], accounts, categories, cancellationToken);
        if (check.Error is { } error)
        {
            return ApiErrors.BadRequest(error);
        }

        var now = time.GetUtcNow();
        var value = check.Value!;
        var commitment = new Commitment
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = value.Name,
            Amount = value.Amount,
            Type = value.Type,
            IntervalMonths = value.IntervalMonths,
            FirstDueDate = value.FirstDueDate,
            CreatedAt = now,
        };
        Apply(commitment, value, now);
        db.Commitments.Add(commitment);
        await db.SaveChangesAsync(cancellationToken);

        loggers.CreateLogger(LogCategory).LogInformation(
            "Vertrag {CommitmentId} für {OwnerId} von {UserId} angelegt.", commitment.Id, ownerId, callerId);
        return Results.Created($"/api/v1/commitments/{commitment.Id}", ToResponse(commitment, Berlin.Today(time)));
    }

    /// <summary>
    /// Ändert die Felder, die in der Anfrage stehen, und leert die mit gesetztem Clear-Schalter.
    /// Danach wird der ganze Vertrag mit denselben Regeln wie beim Anlegen geprüft. Der Vertrag eines anderen
    /// Mitglieds braucht dessen Freigabe Ändern.
    /// </summary>
    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateCommitmentRequest request,
        IAccess access,
        DuofyDbContext db,
        IAccountLookup accounts,
        ICategoryLookup categories,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (access.CallerId is null)
        {
            return Results.Unauthorized();
        }

        var commitment = await db.Commitments.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (commitment is null)
        {
            return NotFound();
        }
        if ((await access.RequireAsync(commitment.OwnerId, Area.Commitments, AccessLevel.Edit, cancellationToken)).Refusal(NotFound) is { } refused)
        {
            return refused;
        }
        var ownerId = commitment.OwnerId;

        // Ein geschickter Wert gilt immer; sonst leert der Clear-Schalter das Feld, und ohne beides bleibt es.
        var clearTarget = request.ClearTarget == true;
        var input = new CommitmentInput(
            request.Type ?? CommitmentTypeKey.ToKey(commitment.Type),
            request.Name ?? commitment.Name,
            request.Amount ?? commitment.Amount,
            request.IntervalMonths ?? commitment.IntervalMonths,
            request.FirstDueDate ?? commitment.FirstDueDate,
            request.LastDueDate ?? (request.ClearLastDueDate == true ? null : commitment.LastDueDate),
            request.CategoryId ?? commitment.CategoryId,
            request.Pot ?? PotKey.ToKey(commitment.Pot),
            request.AccountId ?? (request.ClearAccountId == true ? null : commitment.AccountId),
            request.CounterAccountId ?? (request.ClearCounterAccountId == true ? null : commitment.CounterAccountId),
            request.PassThrough ?? commitment.PassThrough,
            request.PaymentMethod ?? PaymentMethodKeys.ToKey(commitment.PaymentMethod),
            request.TargetAmount ?? (clearTarget ? null : commitment.TargetAmount),
            request.TargetDate ?? (clearTarget ? null : commitment.TargetDate));
        Guid[] stored = [.. new[] { commitment.AccountId, commitment.CounterAccountId }.OfType<Guid>()];

        // Eine neue oder geleerte letzte Zahlung beendet den Vertrag oder holt ihn zurück; das braucht wie
        // Beenden die Freigabe Löschen (Spec 68, A17). Dieselbe letzte Zahlung noch einmal ändert nichts.
        if (input.LastDueDate != commitment.LastDueDate
            && (await access.RequireAsync(ownerId, Area.Commitments, AccessLevel.Delete, cancellationToken)).Refusal(NotFound) is { } notEnding)
        {
            return notEnding;
        }

        var check = await CommitmentValidator.CheckAsync(
            input, ownerId, await DefaultAccountAsync(db, ownerId, cancellationToken), stored, accounts, categories, cancellationToken);
        if (check.Error is { } error)
        {
            return ApiErrors.BadRequest(error);
        }

        Apply(commitment, check.Value!, time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToResponse(commitment, Berlin.Today(time)));
    }

    /// <summary>
    /// Beendet einen Vertrag (R8): Die letzte Zahlung wird die letzte bis heute (Berlin). Gab es
    /// noch keine, wird sie der Tag vor der ersten Zahlung; der Vertrag war dann nie fällig.
    /// Rückgängig geht das, indem man die letzte Zahlung im Dialog wieder leert. Bei einem anderen Mitglied
    /// braucht das dessen Freigabe Löschen.
    /// </summary>
    private static async Task<IResult> EndAsync(
        Guid id,
        IAccess access,
        DuofyDbContext db,
        TimeProvider time,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (access.CallerId is not { } callerId)
        {
            return Results.Unauthorized();
        }

        var commitment = await db.Commitments.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (commitment is null)
        {
            return NotFound();
        }
        if ((await access.RequireAsync(commitment.OwnerId, Area.Commitments, AccessLevel.Delete, cancellationToken)).Refusal(NotFound) is { } refused)
        {
            return refused;
        }

        var today = Berlin.Today(time);
        commitment.LastDueDate = commitment.LastPaymentOnOrBefore(today) ?? commitment.FirstDueDate.AddDays(-1);
        commitment.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        loggers.CreateLogger(LogCategory).LogInformation(
            "Vertrag {CommitmentId} von {OwnerId} durch {UserId} beendet, letzte Zahlung {LastDueDate}.",
            commitment.Id, commitment.OwnerId, callerId, commitment.LastDueDate);
        return Results.Ok(ToResponse(commitment, today));
    }

    /// <summary>Das heutige Standardkonto der Person, falls es eines gibt.</summary>
    private static Task<Guid?> DefaultAccountAsync(DuofyDbContext db, Guid ownerId, CancellationToken cancellationToken) =>
        db.Accounts
            .AsNoTracking()
            .Where(a => a.OwnerId == ownerId && a.IsDefault)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Überträgt die geprüften Werte auf den Vertrag.</summary>
    private static void Apply(Commitment commitment, CheckedCommitment value, DateTimeOffset now)
    {
        commitment.Type = value.Type;
        commitment.Name = value.Name;
        commitment.Amount = value.Amount;
        commitment.IntervalMonths = value.IntervalMonths;
        commitment.FirstDueDate = value.FirstDueDate;
        commitment.LastDueDate = value.LastDueDate;
        commitment.CategoryId = value.CategoryId;
        commitment.Pot = value.Pot;
        commitment.AccountId = value.AccountId;
        commitment.CounterAccountId = value.CounterAccountId;
        commitment.PassThrough = value.PassThrough;
        commitment.PaymentMethod = value.PaymentMethod;
        commitment.TargetAmount = value.TargetAmount;
        commitment.TargetDate = value.TargetDate;
        commitment.UpdatedAt = now;
    }
}
