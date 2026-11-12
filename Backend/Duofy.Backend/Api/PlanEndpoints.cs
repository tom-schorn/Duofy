using Duofy.Schemas;
using Duofy.Data;
using Duofy.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api;

/// <summary>
/// Der Monatsplan (/api/v1/plans/{year}/{month}): den Monat öffnen, einmalige Posten, Posten
/// ändern und löschen, abhaken, Unterschiede zu den Verträgen übernehmen und der Übertrag (Spec 66).
///
/// Ohne <c>owner</c> ist es der eigene Plan; mit <c>?owner=</c> der Plan eines anderen Mitglieds, so weit es
/// ihn freigegeben hat (Spec 68), geprüft von <see cref="IAccess"/>: der Plan und seine Posten im Bereich
/// Planung, Abhaken und den Haken zurücknehmen im Bereich Buchungen. Pläne werden über Besitzer, Jahr und Monat
/// gefunden, nie über eine Kennung aus der Anfrage; ein Posten ohne Freigabe sieht aus wie ein unbekannter:
/// 404 mit dem Code position_not_found.
/// </summary>
internal static class PlanEndpoints
{
    private const string LogCategory = "Duofy.Api.Plans";

    private static readonly string[] GermanMonths =
        ["Januar", "Februar", "März", "April", "Mai", "Juni", "Juli", "August", "September", "Oktober", "November", "Dezember"];

    public static void MapPlanEndpoints(this IEndpointRouteBuilder v1)
    {
        v1.MapGet("/plans", ListMonthsAsync).RequireAuthorization();
        v1.MapGet("/plans/overview", OverviewAsync).RequireAuthorization();

        var plan = v1.MapGroup("/plans/{year:int}/{month:int}").RequireAuthorization();
        plan.MapGet("", GetAsync);
        plan.MapPost("/positions", CreatePositionAsync);
        plan.MapPatch("/positions/{id:guid}", UpdatePositionAsync);
        plan.MapDelete("/positions/{id:guid}", DeletePositionAsync);
        plan.MapPost("/positions/{id:guid}/postpone", PostponeAsync);
        plan.MapPost("/positions/{id:guid}/postpone/undo", UndoPostponeAsync);
        plan.MapPost("/positions/{id:guid}/tick", TickAsync);
        plan.MapDelete("/positions/{id:guid}/tick", UntickAsync);
        plan.MapPost("/changes/apply", ApplyChangesAsync);
        plan.MapPost("/carry-over", CreateCarryOverAsync);
        plan.MapDelete("/carry-over", DeleteCarryOverAsync);
    }

    /// <summary>
    /// Der Besitzer des Plans aus <c>?owner=</c>, ohne Angabe die angemeldete Person, und die Antwort, wenn die
    /// Stufe im Bereich nicht reicht: unter Ansehen 404 mit <paramref name="notFoundCode"/>, sonst 403.
    /// </summary>
    private static async Task<(Guid OwnerId, IResult? Denied)> OwnerAsync(
        IAccess access, Guid? owner, Area area, AccessLevel needed, string notFoundCode, CancellationToken cancellationToken)
    {
        if (access.CallerId is not { } callerId)
        {
            return (Guid.Empty, Results.Unauthorized());
        }
        var ownerId = owner ?? callerId;
        return (ownerId, (await access.RequireAsync(ownerId, area, needed, cancellationToken)).Refusal(() => NotFound(notFoundCode)));
    }

    private static bool IsValidMonth(int year, int month) =>
        month is >= 1 and <= 12 && year is >= PlanRules.FirstYear and <= PlanRules.LastYear;

    private static IResult NotFound(string code) =>
        Results.Json(new ErrorResponse(code), statusCode: StatusCodes.Status404NotFound);

    private static IResult Conflict(string code) =>
        Results.Json(new ErrorResponse(code), statusCode: StatusCodes.Status409Conflict);

    /// <summary>
    /// Die Monate mit einem Plan und deren Frei-Betrag für die Plan-Übersicht (Spec 23); legt nichts an. Wie beim
    /// Monat gilt <c>?owner=</c> mit der Freigabe im Bereich Planung.
    /// </summary>
    private static async Task<IResult> ListMonthsAsync(
        Guid? owner, IAccess access, PlanStore plans, CancellationToken cancellationToken)
    {
        var (ownerId, denied) = await OwnerAsync(access, owner, Area.Plan, AccessLevel.View, ErrorCodes.OwnerNotFound, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }
        var seesBookings = (await access.LevelAsync(ownerId, Area.Book, cancellationToken)).Includes(AccessLevel.View);
        return Results.Ok(await plans.MonthsAsync(ownerId, seesBookings, cancellationToken));
    }

    /// <summary>
    /// Die Monate eines Jahres mit Verplanbar, Anstehend und den Töpfen in einem Aufruf (Issue #94, R10), dazu die vorhandenen
    /// künftigen Monate. Nur lesend; die Freigabe ist die der Monatsliste (<c>?owner=</c> mit Planung ansehen), das Jahr muss in
    /// <see cref="PlanRules"/> liegen, sonst 400.
    /// </summary>
    private static async Task<IResult> OverviewAsync(
        int? year, Guid? owner, IAccess access, PlanStore plans, CancellationToken cancellationToken)
    {
        var (ownerId, denied) = await OwnerAsync(access, owner, Area.Plan, AccessLevel.View, ErrorCodes.OwnerNotFound, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }
        if (year is not (>= PlanRules.FirstYear and <= PlanRules.LastYear))
        {
            return ApiErrors.BadRequest(ErrorCodes.MonthInvalid);
        }
        var seesBookings = (await access.LevelAsync(ownerId, Area.Book, cancellationToken)).Includes(AccessLevel.View);
        return Results.Ok(await plans.OverviewAsync(ownerId, year.Value, seesBookings, cancellationToken));
    }

    /// <summary>Öffnet den Monat (R1); beim ersten Mal wird er aus den Verträgen angelegt.</summary>
    private static async Task<IResult> GetAsync(
        int year, int month, Guid? owner, IAccess access, PlanStore plans, CancellationToken cancellationToken)
    {
        var (ownerId, denied) = await OwnerAsync(access, owner, Area.Plan, AccessLevel.View, ErrorCodes.OwnerNotFound, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }
        if (!IsValidMonth(year, month))
        {
            return ApiErrors.BadRequest(ErrorCodes.MonthInvalid);
        }

        var plan = await plans.GetOrCreateAsync(ownerId, year, month, cancellationToken);
        return Results.Ok(await plans.ResponseAsync(plan, await PlanSight.OfAsync(access, ownerId, cancellationToken), cancellationToken));
    }

    /// <summary>Ein einmaliger Posten (R3): gilt nur in diesem Monat und hat keinen Vertrag.</summary>
    private static async Task<IResult> CreatePositionAsync(
        int year,
        int month,
        CreatePositionRequest request,
        Guid? owner,
        IAccess access,
        PlanStore plans,
        DuofyDbContext db,
        IAccountLookup accounts,
        ICategoryLookup categories,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var (ownerId, denied) = await OwnerAsync(access, owner, Area.Plan, AccessLevel.Create, ErrorCodes.OwnerNotFound, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }
        if (!IsValidMonth(year, month))
        {
            return ApiErrors.BadRequest(ErrorCodes.MonthInvalid);
        }

        var input = new PositionInput(request.Label, request.Amount, request.DueDay, request.CategoryId, request.Pot, request.AccountId);
        var check = await CheckPositionAsync(input, year, month, ownerId, storedAccountId: null, accounts, categories, cancellationToken);
        if (check.Error is { } error)
        {
            return ApiErrors.BadRequest(error);
        }

        var plan = await plans.GetOrCreateAsync(ownerId, year, month, cancellationToken);
        var value = check.Value!;
        var position = new PlanPosition
        {
            Id = Guid.NewGuid(),
            Label = value.Label,
            AmountPlanned = value.Amount,
            DueDay = value.DueDay,
            Type = TypeFor(value.Pot),
        };
        Apply(position, value);
        plan.Positions.Add(position);
        await db.SaveChangesAsync(cancellationToken);

        loggers.CreateLogger(LogCategory).LogInformation(
            "Einmaliger Posten {PositionId} im Plan {Year}-{Month} von {UserId} angelegt.", position.Id, year, month, ownerId);
        return Results.Created($"/api/v1/plans/{year}/{month}/positions/{position.Id}", PlanStore.ToResponse(position, 0m, year, month));
    }

    /// <summary>
    /// Ändert die Felder, die in der Anfrage stehen, mit denselben Regeln wie beim Anlegen. Ein Posten
    /// aus einem Vertrag gilt danach als von Hand geändert; der Abgleich überschreibt ihn nicht mehr still (R2).
    /// Ein Posten mit Buchungen wechselt nicht zwischen Einnahmen und den Ausgabe-Töpfen (R10).
    /// </summary>
    private static async Task<IResult> UpdatePositionAsync(
        int year,
        int month,
        Guid id,
        UpdatePositionRequest request,
        Guid? owner,
        IAccess access,
        PlanStore plans,
        DuofyDbContext db,
        IAccountLookup accounts,
        ICategoryLookup categories,
        CancellationToken cancellationToken)
    {
        var (ownerId, denied) = await OwnerAsync(access, owner, Area.Plan, AccessLevel.Edit, ErrorCodes.PositionNotFound, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }
        if (await FindPositionAsync(plans, ownerId, year, month, id, cancellationToken) is not { } found)
        {
            return NotFound(ErrorCodes.PositionNotFound);
        }
        var position = found.Position;

        var input = new PositionInput(
            request.Label ?? position.Label,
            request.Amount ?? position.AmountPlanned,
            request.DueDay ?? position.DueDay,
            request.CategoryId ?? position.CategoryId,
            request.Pot ?? PotKey.ToKey(position.Pot),
            request.AccountId ?? (request.ClearAccountId == true ? null : position.AccountId));
        var check = await CheckPositionAsync(input, year, month, ownerId, position.AccountId, accounts, categories, cancellationToken);
        if (check.Error is { } error)
        {
            return ApiErrors.BadRequest(error);
        }

        var value = check.Value!;
        // Eine Einnahme hat kein Zielkonto, wie beim Vertrag. Abgelehnt wird nur das Umstellen: Ein schon so gespeicherter
        // Posten lässt sich weiter ändern und zurück auf einen anderen Topf stellen.
        if (value.Pot == Pot.Income && position.Pot != Pot.Income && position.CounterAccountId is not null)
        {
            return ApiErrors.BadRequest(ErrorCodes.PositionTargetInvalid);
        }
        // Einnahme-Zeilen füllen nur Einnahme-Posten und die übrigen nur die anderen (R6). Wechselt ein Posten mit
        // Buchungen die Art seines Topfs, passten seine Zeilen nicht mehr; zwischen den Ausgabe-Töpfen geht es.
        if ((value.Pot == Pot.Income) != (position.Pot == Pot.Income) && await plans.HasBookingsAsync(position.Id, cancellationToken))
        {
            return Conflict(ErrorCodes.PositionHasBookings);
        }
        position.Label = value.Label;
        position.AmountPlanned = value.Amount;
        position.DueDay = value.DueDay;
        if (position.CommitmentId is null)
        {
            position.Type = TypeFor(value.Pot);
        }
        else
        {
            position.ChangedByHand = true;
        }
        Apply(position, value);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await plans.PositionResponseAsync(ownerId, position, year, month, await PlanSight.OfAsync(access, ownerId, cancellationToken), cancellationToken));
    }

    /// <summary>
    /// Verschiebt einen offenen, unbezahlten Posten in den nächsten Monat (Spec 136, R1 bis R3, R5): Der Monat des Postens
    /// behält ihn als verschoben, der nächste bekommt eine Kopie. Es braucht Planung Bearbeiten wie beim Ändern eines Postens.
    /// </summary>
    private static async Task<IResult> PostponeAsync(
        int year,
        int month,
        Guid id,
        Guid? owner,
        IAccess access,
        PlanStore plans,
        DuofyDbContext db,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var (ownerId, denied) = await OwnerAsync(access, owner, Area.Plan, AccessLevel.Edit, ErrorCodes.PositionNotFound, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }
        if (!IsValidMonth(year, month))
        {
            return NotFound(ErrorCodes.PositionNotFound);
        }
        var target = PlanStore.Shifted(year, month, 1);
        if (!IsValidMonth(target.Year, target.Month))
        {
            return ApiErrors.BadRequest(ErrorCodes.MonthInvalid);
        }

        // Vorab prüfen, damit ein abgelehnter Aufruf den nächsten Monat nicht anlegt; unter der Sperre wird noch einmal geprüft.
        if (await FindPositionAsync(plans, ownerId, year, month, id, cancellationToken) is not { } probe)
        {
            return NotFound(ErrorCodes.PositionNotFound);
        }
        if (await PostponeRefusalAsync(plans, probe.Position, cancellationToken) is { } early)
        {
            return early;
        }
        await plans.GetOrCreateAsync(ownerId, target.Year, target.Month, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await plans.LockAsync(ownerId, year, month, cancellationToken);
        if (await FindPositionAsync(plans, ownerId, year, month, id, cancellationToken) is not { } found)
        {
            return NotFound(ErrorCodes.PositionNotFound);
        }
        var position = found.Position;
        if (await PostponeRefusalAsync(plans, position, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        var next = await plans.GetOrCreateAsync(ownerId, target.Year, target.Month, cancellationToken);
        var copy = position.PostponedCopy(target.Year, target.Month);
        next.Positions.Add(copy);
        next.SortPositions();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        loggers.CreateLogger(LogCategory).LogInformation(
            "Posten {PositionId} im Plan {Year}-{Month} von {UserId} in {TargetYear}-{TargetMonth} verschoben (Kopie {CopyId}).",
            id, year, month, ownerId, target.Year, target.Month, copy.Id);
        var sight = await PlanSight.OfAsync(access, ownerId, cancellationToken);
        return Results.Ok(new PostponePositionResponse(
            await plans.PositionResponseAsync(ownerId, position, year, month, sight, cancellationToken),
            await plans.PositionResponseAsync(ownerId, copy, target.Year, target.Month, sight, cancellationToken)));
    }

    /// <summary>Warum ein Posten nicht verschoben werden darf (R3), oder null.</summary>
    private static async Task<IResult?> PostponeRefusalAsync(PlanStore plans, PlanPosition position, CancellationToken cancellationToken)
    {
        if (position.IsMoved)
        {
            return Conflict(ErrorCodes.PositionAlreadyMoved);
        }
        if (position.IsDone || await plans.HasBookingsAsync(position.Id, cancellationToken))
        {
            return Conflict(ErrorCodes.PositionPaid);
        }
        return null;
    }

    /// <summary>
    /// Nimmt das Verschieben zurück (Spec 136, R4): löscht die Kopie im nächsten Monat, wenn sie unbezahlt ist, und der
    /// alte Posten zählt wieder. Aufgerufen wird es am alten Posten.
    /// </summary>
    private static async Task<IResult> UndoPostponeAsync(
        int year,
        int month,
        Guid id,
        Guid? owner,
        IAccess access,
        PlanStore plans,
        DuofyDbContext db,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var (ownerId, denied) = await OwnerAsync(access, owner, Area.Plan, AccessLevel.Edit, ErrorCodes.PositionNotFound, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await plans.LockAsync(ownerId, year, month, cancellationToken);
        if (await FindPositionAsync(plans, ownerId, year, month, id, cancellationToken) is not { } found)
        {
            return NotFound(ErrorCodes.PositionNotFound);
        }
        var position = found.Position;
        if (position.MovedToPositionId is not { } copyId)
        {
            return Conflict(ErrorCodes.PositionNotMoved);
        }

        var target = PlanStore.Shifted(year, month, 1);
        var copy = await db.PlanPositions.FirstOrDefaultAsync(p => p.Id == copyId, cancellationToken);
        if (copy is not null)
        {
            if (copy.IsMoved)
            {
                // Die Kopie wurde selbst weitergeschoben; erst deren Verschieben zurücknehmen.
                return Conflict(ErrorCodes.PositionCopyMoved);
            }
            if (copy.IsDone || await plans.HasBookingsAsync(copy.Id, cancellationToken))
            {
                return Conflict(ErrorCodes.PositionCopyPaid);
            }
            db.PlanPositions.Remove(copy);
        }
        position.MovedToPositionId = null;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        loggers.CreateLogger(LogCategory).LogInformation(
            "Verschieben von Posten {PositionId} im Plan {Year}-{Month} von {UserId} zurückgenommen (Kopie in {TargetYear}-{TargetMonth} gelöscht).",
            id, year, month, ownerId, target.Year, target.Month);
        return Results.Ok(await plans.PositionResponseAsync(ownerId, position, year, month, await PlanSight.OfAsync(access, ownerId, cancellationToken), cancellationToken));
    }

    /// <summary>Löscht einen Posten ohne Buchungen (R10). Der Vertrag läuft weiter.</summary>
    private static async Task<IResult> DeletePositionAsync(
        int year,
        int month,
        Guid id,
        Guid? owner,
        IAccess access,
        PlanStore plans,
        DuofyDbContext db,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var (ownerId, denied) = await OwnerAsync(access, owner, Area.Plan, AccessLevel.Delete, ErrorCodes.PositionNotFound, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }
        if (await FindPositionAsync(plans, ownerId, year, month, id, cancellationToken) is not { } found)
        {
            return NotFound(ErrorCodes.PositionNotFound);
        }
        var (plan, position) = found;
        // Löschen ließe die Kopie ohne Herkunft zurück; erst das Verschieben zurücknehmen.
        if (position.IsMoved)
        {
            return Conflict(ErrorCodes.PositionMoved);
        }
        if (await plans.HasBookingsAsync(position.Id, cancellationToken))
        {
            return Conflict(ErrorCodes.PositionHasBookings);
        }

        plan.Positions.Remove(position);
        await db.SaveChangesAsync(cancellationToken);
        loggers.CreateLogger(LogCategory).LogInformation(
            "Posten {PositionId} im Plan {Year}-{Month} von {UserId} gelöscht.", id, year, month, ownerId);
        return Results.NoContent();
    }

    /// <summary>
    /// Hakt einen Posten von Hand ab (R4, R5). Ohne Buchungen am Posten entsteht eine Buchung,
    /// vorbelegt aus dem Posten (eine Umbuchung, wenn er ein Zielkonto hat); mit Buchungen nur der
    /// Ausgleich, wenn ein Betrag über 0 kommt. Ein schon abgehakter Posten bleibt, wie er ist.
    /// </summary>
    private static async Task<IResult> TickAsync(
        int year,
        int month,
        Guid id,
        [FromBody] TickPositionRequest? request,
        Guid? owner,
        IAccess access,
        PlanStore plans,
        DuofyDbContext db,
        IAccountLookup accounts,
        ICategoryLookup categories,
        IPositionLookup positions,
        TimeProvider time,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var (ownerId, denied) = await OwnerAsync(access, owner, Area.Plan, AccessLevel.View, ErrorCodes.PositionNotFound, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }
        // Abhaken legt eine Buchung an: Es braucht die Freigabe der Buchungen und, wie jede Buchung, die Konten.
        if ((await access.RequireAsync(ownerId, Area.Book, AccessLevel.Edit, cancellationToken)).Refusal(() => NotFound(ErrorCodes.PositionNotFound)) is { } noBook)
        {
            return noBook;
        }
        if ((await access.RequireAsync(ownerId, Area.Accounts, AccessLevel.View, cancellationToken)).Outcome != AccessOutcome.Allowed)
        {
            return NotFound(ErrorCodes.AccountNotFound);
        }

        // Erst sperren, dann lesen: Ein zweites gleichzeitiges Abhaken wartet und findet den Posten dann abgehakt (A22).
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await plans.LockAsync(ownerId, year, month, cancellationToken);
        if (await FindPositionAsync(plans, ownerId, year, month, id, cancellationToken) is not { } found)
        {
            return NotFound(ErrorCodes.PositionNotFound);
        }
        var position = found.Position;
        if (position.IsDone)
        {
            return Results.Ok(await plans.PositionResponseAsync(ownerId, position, year, month, await PlanSight.OfAsync(access, ownerId, cancellationToken), cancellationToken));
        }

        // Ein verschobener Posten zählt hier nicht mehr; bezahlt wird seine Kopie im nächsten Monat (Spec 136).
        if (position.IsMoved)
        {
            return Conflict(ErrorCodes.PositionMoved);
        }

        request ??= new TickPositionRequest();
        var hasBookings = await plans.HasBookingsAsync(position.Id, cancellationToken);
        var amount = request.Amount ?? (hasBookings ? 0m : position.AmountPlanned);
        var now = time.GetUtcNow();

        if (amount != 0m)
        {
            // Eine Umbuchung kann nicht aus dem Topf Einnahmen kommen; die Person bekommt gesagt, wie der alte Posten zu reparieren ist.
            if (position.Pot == Pot.Income && position.CounterAccountId is not null)
            {
                return ApiErrors.BadRequest(ErrorCodes.PositionIncomeWithTarget);
            }
            var accountId = request.AccountId ?? position.AccountId ?? await plans.DefaultAccountAsync(ownerId, cancellationToken);
            if (accountId is null)
            {
                return ApiErrors.BadRequest(ErrorCodes.DefaultAccountRequired);
            }

            // Hängt der Vertrag hinter dem Posten an einem Sparziel, fließt die Rate dem Ziel zu (Issue 32).
            var links = position.CommitmentId is { } commitmentId
                ? await db.Commitments.AsNoTracking()
                    .Where(c => c.Id == commitmentId && c.OwnerId == ownerId)
                    .Select(c => new { c.SavingsGoalId, c.DebtId })
                    .FirstOrDefaultAsync(cancellationToken)
                : null;
            var goalId = position.CounterAccountId is not null ? links?.SavingsGoalId : null;
            // Eine Rate, die eine Schuld zurückzahlt, ist eine Ausgabe und trägt die Schuld an ihrer Zeile (Issue 31).
            var debtId = position.CounterAccountId is null && position.Pot != Pot.Income ? links?.DebtId : null;
            // Eine Rate an ein Sparziel oder auf eine Schuld verändert deren Stand; das braucht im Bereich Sparen Anlegen. Ohne
            // diese Freigabe bricht das Abhaken ab, statt die Verknüpfung still wegzulassen.
            if ((goalId is not null || debtId is not null)
                && (await access.RequireAsync(ownerId, Area.Savings, AccessLevel.Create, cancellationToken)).Refusal(() => NotFound(ErrorCodes.PositionNotFound)) is { } noSavings)
            {
                return noSavings;
            }
            var booking = TickBooking(position, year, month, amount, accountId.Value, request.Date ?? TickDate(position, year, month, Berlin.Today(time)), goalId, debtId);
            var check = await BookingValidator.CheckAsync(booking, ownerId, accounts, categories, positions, time, cancellationToken);
            if (BookingEndpoints.Refusal(check) is { } refused)
            {
                return refused;
            }
            position.TickBookingId = BookingEndpoints.Add(db, ownerId, booking, check, now).Id;
        }

        position.DoneAt = now;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        loggers.CreateLogger(LogCategory).LogInformation(
            "Posten {PositionId} von {UserId} abgehakt, Buchung {BookingId}.", position.Id, ownerId, position.TickBookingId);
        return Results.Ok(await plans.PositionResponseAsync(ownerId, position, year, month, await PlanSight.OfAsync(access, ownerId, cancellationToken), cancellationToken));
    }

    /// <summary>Nimmt den Haken zurück; eine Buchung, die beim Abhaken entstanden ist, wird gelöscht (R5).</summary>
    private static async Task<IResult> UntickAsync(
        int year,
        int month,
        Guid id,
        Guid? owner,
        IAccess access,
        PlanStore plans,
        DuofyDbContext db,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var (ownerId, denied) = await OwnerAsync(access, owner, Area.Plan, AccessLevel.View, ErrorCodes.PositionNotFound, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }
        // Den Haken zurücknehmen löscht die Buchung vom Abhaken: Es braucht die Freigabe Löschen der Buchungen.
        if ((await access.RequireAsync(ownerId, Area.Book, AccessLevel.Delete, cancellationToken)).Refusal(() => NotFound(ErrorCodes.PositionNotFound)) is { } noBook)
        {
            return noBook;
        }
        if (await FindPositionAsync(plans, ownerId, year, month, id, cancellationToken) is not { } found)
        {
            return NotFound(ErrorCodes.PositionNotFound);
        }
        var position = found.Position;

        if (position.TickBookingId is { } bookingId)
        {
            // Die Buchung vom Abhaken kann einem Sparziel oder einer Schuld zugeordnet sein; sie zu löschen ändert deren Stand und
            // braucht im Bereich Sparen Löschen, genau wie das Löschen einer solchen Buchung.
            if (await db.Bookings.AnyAsync(b => b.Id == bookingId && b.OwnerId == ownerId && b.Lines.Any(l => l.SavingsGoalId != null || l.DebtId != null), cancellationToken)
                && (await access.RequireAsync(ownerId, Area.Savings, AccessLevel.Delete, cancellationToken)).Refusal(() => NotFound(ErrorCodes.PositionNotFound)) is { } noSavings)
            {
                return noSavings;
            }
            await db.Bookings.Where(b => b.Id == bookingId && b.OwnerId == ownerId).ExecuteDeleteAsync(cancellationToken);
            loggers.CreateLogger(LogCategory).LogInformation(
                "Haken an Posten {PositionId} von {UserId} entfernt, Buchung {BookingId} gelöscht.", position.Id, ownerId, bookingId);
        }
        position.TickBookingId = null;
        position.DoneAt = null;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await plans.PositionResponseAsync(ownerId, position, year, month, await PlanSight.OfAsync(access, ownerId, cancellationToken), cancellationToken));
    }

    /// <summary>
    /// Übernimmt Unterschiede zu den Verträgen (R2). Ohne Liste alle, die nicht geschützt sind; mit
    /// Liste genau die genannten, auch geschützte. Ein Posten mit Buchungen wird nie entfernt: in
    /// "alle übernehmen" bleibt er stehen, ausdrücklich gewählt antwortet die API 409.
    /// </summary>
    private static async Task<IResult> ApplyChangesAsync(
        int year,
        int month,
        [FromBody] ApplyPlanChangesRequest? request,
        Guid? owner,
        IAccess access,
        PlanStore plans,
        DuofyDbContext db,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var (ownerId, denied) = await OwnerAsync(access, owner, Area.Plan, AccessLevel.Edit, ErrorCodes.OwnerNotFound, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }
        if (!IsValidMonth(year, month))
        {
            return ApiErrors.BadRequest(ErrorCodes.MonthInvalid);
        }

        // Die Unterschiede kommen aus den Verträgen; wer sie nicht sehen darf, übernimmt sie auch nicht.
        if ((await access.RequireAsync(ownerId, Area.Commitments, AccessLevel.View, cancellationToken)).Refusal(ApiErrors.OwnerNotFound) is { } noCommitments)
        {
            return noCommitments;
        }

        var plan = await plans.GetOrCreateAsync(ownerId, year, month, cancellationToken);
        var commitments = await SavingsGoalEndpoints.PlanCommitmentsAsync(db, ownerId, cancellationToken);
        // Unterschiede aus Sparzielen und Schulden unter den Verträgen übernimmt nur, wer den Bereich Sparen ansehen darf.
        var seesSavings = (await PlanSight.OfAsync(access, ownerId, cancellationToken)).Savings;
        HashSet<Guid> hidden = seesSavings ? [] : await SavingsGoalEndpoints.SavingsTypeCommitmentIdsAsync(db, ownerId, cancellationToken);
        HashSet<Guid> goalLinked = seesSavings ? [] : await SavingsGoalEndpoints.GoalLinkedCommitmentIdsAsync(db, ownerId, cancellationToken);
        var changes = PlanChanges.Compare(plan, commitments, await plans.FirstOpeningAsync(ownerId, cancellationToken))
            .Where(change => !hidden.Contains(change.CommitmentId)
                && !(change.Kind == PlanChangeKind.NoLongerDue && goalLinked.Contains(change.CommitmentId))).ToList();
        var items = request?.Items;
        var chosen = items is null
            ? changes.Where(c => !c.Protected).ToList()
            : changes.Where(c => items.Any(i => c.PositionId is { } positionId
                ? i.PositionId == positionId
                : i.CommitmentId == c.CommitmentId)).ToList();

        // Erst entscheiden, was übernommen wird, dann prüfen, ob dafür ein Posten entfernt wird: Das ist Löschen
        // und braucht die Stufe Löschen (Spec 68, A18). Fehlt sie, bleibt der ganze Plan, wie er ist.
        var applying = new List<PlanChange>();
        var movedIds = plan.Positions.Where(p => p.IsMoved).Select(p => p.Id).ToHashSet();
        foreach (var change in chosen)
        {
            // Ein verschobener Posten ändert sich nicht über die Verträge; erst das Verschieben zurücknehmen.
            if (change.PositionId is { } changedId && movedIds.Contains(changedId))
            {
                if (items is not null)
                {
                    return Conflict(ErrorCodes.PositionMoved);
                }
                continue;
            }
            if (change.Kind == PlanChangeKind.NoLongerDue && await plans.HasBookingsAsync(change.PositionId!.Value, cancellationToken))
            {
                if (items is not null)
                {
                    return Conflict(ErrorCodes.PositionHasBookings);
                }
                continue;
            }
            applying.Add(change);
        }
        if (applying.Any(c => c.Kind == PlanChangeKind.NoLongerDue)
            && (await access.RequireAsync(ownerId, Area.Plan, AccessLevel.Delete, cancellationToken)).Refusal(ApiErrors.OwnerNotFound) is { } noDelete)
        {
            return noDelete;
        }
        foreach (var change in applying)
        {
            PlanChanges.Apply(plan, change, commitments);
        }
        await db.SaveChangesAsync(cancellationToken);

        loggers.CreateLogger(LogCategory).LogInformation(
            "{Count} Unterschiede im Plan {Year}-{Month} von {UserId} übernommen.", applying.Count, year, month, ownerId);
        return Results.Ok(await plans.ResponseAsync(plan, await PlanSight.OfAsync(access, ownerId, cancellationToken), cancellationToken));
    }

    /// <summary>
    /// Der Übertrag in diesen Monat (R9). Für jedes Konto mit echtem Stand gleicht eine Buchung am
    /// letzten Tag des Vormonats den Unterschied zum gerechneten Stand aus. Der Übertrag selbst ist
    /// ein Paar auf dem Standardkonto, das sich im Kontostand aufhebt: Beide Buchungen stehen am letzten
    /// Tag des Vormonats, damit der Stand an jedem Tag stimmt; "Übertrag nach ..." im Planmonat des
    /// Vormonats und "Übertrag aus ..." im Planmonat dieses Monats. Paare von vor dem 1.11.2026 haben
    /// "Übertrag aus ..." noch am Ersten und bleiben so. Bei einem positiven Rest ist das erste eine Ausgabe und das zweite eine Einnahme,
    /// bei einem negativen umgekehrt, damit der neue Monat mit dem Minus beginnt; die Beträge sind
    /// immer positiv. Ein Rest von 0 bucht kein Paar, nur die Ausgleichsbuchungen. Höchstens ein
    /// Übertrag je Monat. Ein Ausgleich nach unten liegt im Topf Wünsche, das Paar in Grundbedarf.
    /// </summary>
    private static async Task<IResult> CreateCarryOverAsync(
        int year,
        int month,
        CreateCarryOverRequest request,
        Guid? owner,
        IAccess access,
        PlanStore plans,
        DuofyDbContext db,
        IAccountLookup accounts,
        TimeProvider time,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var (ownerId, denied) = await OwnerAsync(access, owner, Area.Plan, AccessLevel.Edit, ErrorCodes.OwnerNotFound, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }
        if (!IsValidMonth(year, month))
        {
            return ApiErrors.BadRequest(ErrorCodes.MonthInvalid);
        }
        if (!IsValidCarryOverAmount(request.Amount))
        {
            return ApiErrors.BadRequest(ErrorCodes.AmountInvalid);
        }
        var balances = request.Balances ?? [];
        if (balances.GroupBy(b => b.AccountId).Any(g => g.Count() > 1))
        {
            return ApiErrors.BadRequest(ErrorCodes.CarryOverBalanceDuplicate);
        }
        // Der Übertrag legt Buchungen an, wie das Abhaken: Es braucht die Freigabe Ändern der Buchungen. Er bucht
        // auf die Konten; ohne sie ansehen zu dürfen, sind sie wie beim Abhaken nicht zu finden (Spec 68, A19).
        if ((await access.RequireAsync(ownerId, Area.Book, AccessLevel.Edit, cancellationToken)).Refusal(ApiErrors.OwnerNotFound) is { } noBook)
        {
            return noBook;
        }
        if ((await access.RequireAsync(ownerId, Area.Accounts, AccessLevel.View, cancellationToken)).Outcome != AccessOutcome.Allowed)
        {
            return NotFound(ErrorCodes.AccountNotFound);
        }

        // Den Plan erst anlegen (außerhalb der Transaktion, wegen des Wettlaufs beim Anlegen), dann sperren und neu
        // lesen: Ein zweiter gleichzeitiger Übertrag wartet und findet den ersten schon gebucht (A22).
        var plan = await plans.GetOrCreateAsync(ownerId, year, month, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await plans.LockAsync(ownerId, year, month, cancellationToken);
        await db.Entry(plan).ReloadAsync(cancellationToken);
        if (plan.HasCarryOver)
        {
            return Conflict(ErrorCodes.CarryOverExists);
        }
        // Das Paar steht auf dem Standardkonto; ein Rest von 0 bucht keines und braucht deshalb auch keins.
        var defaultAccount = await plans.DefaultAccountAsync(ownerId, cancellationToken);
        if (request.Amount != 0m && defaultAccount is null)
        {
            return ApiErrors.BadRequest(ErrorCodes.DefaultAccountRequired);
        }

        var first = new DateOnly(year, month, 1);
        var lastOfPrevious = first.AddDays(-1);
        var accountInfos = new List<AccountInfo>();
        var touched = balances.Select(b => b.AccountId);
        if (request.Amount != 0m)
        {
            touched = touched.Append(defaultAccount!.Value);
        }
        foreach (var accountId in touched.Distinct())
        {
            if (await accounts.FindAsync(ownerId, accountId, cancellationToken) is not { } info)
            {
                return ApiErrors.BadRequest(ErrorCodes.AccountNotFound);
            }
            if (!info.Active)
            {
                return ApiErrors.BadRequest(ErrorCodes.AccountClosed);
            }
            if (lastOfPrevious <= info.OpeningDate)
            {
                return ApiErrors.BadRequest(ErrorCodes.DateBeforeOpening);
            }
            accountInfos.Add(info);
        }
        if (balances.Any(b => b.Balance is not { } stand || decimal.Round(stand, 2) != stand || Math.Abs(stand) > MoneyAccountRules.MaxOpeningBalance))
        {
            return ApiErrors.BadRequest(ErrorCodes.AmountInvalid);
        }

        var now = time.GetUtcNow();
        var computed = (await plans.BalancesOnAsync(ownerId, lastOfPrevious, cancellationToken)).ToDictionary(a => a.Id, a => a.Balance(lastOfPrevious));
        var previous = (lastOfPrevious.Year, lastOfPrevious.Month);
        foreach (var real in balances)
        {
            var difference = real.Balance!.Value - computed.GetValueOrDefault(real.AccountId);
            if (difference != 0m)
            {
                Add(db, ownerId, real.AccountId, lastOfPrevious, previous, Math.Abs(difference), income: difference > 0, "Ausgleich", now, expensePot: PotKeys.Wants);
            }
        }

        if (request.Amount != 0m)
        {
            var positive = request.Amount > 0m;
            var amount = Math.Abs(request.Amount);
            var outgoing = Add(db, ownerId, defaultAccount!.Value, lastOfPrevious, previous, amount,
                income: !positive, $"Übertrag nach {GermanMonths[month - 1]}", now);
            var incoming = Add(db, ownerId, defaultAccount.Value, lastOfPrevious, (year, month), amount,
                income: positive, $"Übertrag aus {GermanMonths[lastOfPrevious.Month - 1]}", now);
            plan.CarryOverIncomeBookingId = positive ? incoming.Id : outgoing.Id;
            plan.CarryOverExpenseBookingId = positive ? outgoing.Id : incoming.Id;
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        loggers.CreateLogger(LogCategory).LogInformation(
            "Übertrag in den Plan {Year}-{Month} von {UserId} gebucht.", year, month, ownerId);
        return Results.Created($"/api/v1/plans/{year}/{month}/carry-over", await plans.ResponseAsync(plan, await PlanSight.OfAsync(access, ownerId, cancellationToken), cancellationToken));
    }

    /// <summary>Entfernt den Übertrag: Beide Buchungen des Paars werden gelöscht, die Ausgleichsbuchungen bleiben.</summary>
    private static async Task<IResult> DeleteCarryOverAsync(
        int year,
        int month,
        Guid? owner,
        IAccess access,
        PlanStore plans,
        DuofyDbContext db,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var (ownerId, denied) = await OwnerAsync(access, owner, Area.Plan, AccessLevel.Delete, ErrorCodes.CarryOverNotFound, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }
        // Das Entfernen löscht die beiden Buchungen des Übertrags: Es braucht die Freigabe Löschen der Buchungen.
        if ((await access.RequireAsync(ownerId, Area.Book, AccessLevel.Delete, cancellationToken)).Refusal(() => NotFound(ErrorCodes.CarryOverNotFound)) is { } noBook)
        {
            return noBook;
        }
        var plan = IsValidMonth(year, month) ? await plans.FindAsync(ownerId, year, month, cancellationToken) : null;
        if (plan is not { HasCarryOver: true })
        {
            return NotFound(ErrorCodes.CarryOverNotFound);
        }

        // Ein Befehl löscht beide Buchungen; die Datenbank leert dabei die Verweise im Plan (SET NULL).
        Guid?[] pair = [plan.CarryOverIncomeBookingId, plan.CarryOverExpenseBookingId];
        await db.Bookings.Where(b => b.OwnerId == ownerId && pair.Contains(b.Id)).ExecuteDeleteAsync(cancellationToken);

        loggers.CreateLogger(LogCategory).LogInformation("Übertrag im Plan {Year}-{Month} von {UserId} entfernt.", year, month, ownerId);
        return Results.NoContent();
    }

    /// <summary>Der Posten im Plan der Person für diesen Monat; null, wenn es ihn dort nicht gibt.</summary>
    private static async Task<(Plan Plan, PlanPosition Position)?> FindPositionAsync(
        PlanStore plans, Guid ownerId, int year, int month, Guid id, CancellationToken cancellationToken)
    {
        if (!IsValidMonth(year, month) || await plans.FindAsync(ownerId, year, month, cancellationToken) is not { } plan)
        {
            return null;
        }
        return plan.Positions.FirstOrDefault(p => p.Id == id) is { } position ? (plan, position) : null;
    }

    /// <summary>
    /// Der Tag der Buchung beim Abhaken: heute, oder der Fälligkeitstag, wenn er schon vorbei ist.
    /// So landet die Miete eines vergangenen Monats an ihrem Zahltag und nie in der Zukunft.
    /// </summary>
    private static DateOnly TickDate(PlanPosition position, int year, int month, DateOnly today)
    {
        var due = new DateOnly(year, month, Math.Min(position.DueDay, DateTime.DaysInMonth(year, month)));
        return due < today ? due : today;
    }

    /// <summary>
    /// Die Buchung zum Abhaken, vorbelegt aus dem Posten. Ihre Zeile füllt immer den eigenen Posten.
    /// Mit Zielkonto ist sie eine Umbuchung; Quell- und Zieltopf sind beide der Topf des Postens (Spec 72, R9), bei einem Vertrag mit Sparziel ist der Zieltopf Sparen,
    /// und eine Umbuchung darf jeden Posten mit eigenem Zielkonto füllen (R6), von Hand genauso.
    /// </summary>
    private static SaveBookingRequest TickBooking(
        PlanPosition position, int year, int month, decimal amount, Guid accountId, DateOnly date, Guid? savingsGoalId = null, Guid? debtId = null)
    {
        if (position.CounterAccountId is { } counter)
        {
            var pot = PotKey.ToKey(position.Pot);
            // Mit Sparziel geht das Geld in den Topf Sparen und die Zeile trägt das Ziel; sonst bleibt der Zieltopf der des Postens.
            var targetPot = savingsGoalId is null ? pot : PotKeys.Savings;
            return new SaveBookingRequest(BookingKinds.Transfer, accountId, counter, date, amount, position.Label,
                [new SaveBookingLine(amount, pot, null, position.Id, savingsGoalId)], year, month, TargetPot: targetPot);
        }

        var kind = position.Pot == Pot.Income ? BookingKinds.Income : BookingKinds.Expense;
        return new SaveBookingRequest(kind, accountId, null, date, amount, position.Label,
            [new SaveBookingLine(amount, PotKey.ToKey(position.Pot), position.CategoryId, position.Id, DebtId: debtId)], year, month);
    }

    /// <summary>Eine Buchung des Übertrags oder Ausgleichs, ohne die Prüfung für Buchungen von Hand (die Daten sind schon geprüft).</summary>
    private static Booking Add(
        DuofyDbContext db, Guid ownerId, Guid accountId, DateOnly date, (int Year, int Month) planMonth, decimal amount,
        bool income, string note, DateTimeOffset now, string expensePot = PotKeys.Needs)
    {
        var line = income
            ? new SaveBookingLine(amount, PotKeys.Income, CategoryCatalog.IdFor(CarryOverIncomeCategory))
            : new SaveBookingLine(amount, expensePot, CategoryCatalog.IdFor(CarryOverExpenseCategory));
        var request = new SaveBookingRequest(
            income ? BookingKinds.Income : BookingKinds.Expense, accountId, null, date, amount, note, [line], planMonth.Year, planMonth.Month);
        return BookingEndpoints.Add(db, ownerId, request, BookingCheck.ValidIn(planMonth.Year, planMonth.Month), now);
    }

    /// <summary>Die eingebaute Kategorie der Einnahmen aus Übertrag und Ausgleich: Sonstige Einnahmen.</summary>
    private const string CarryOverIncomeCategory = "income.other";

    /// <summary>Die eingebaute Kategorie der Ausgaben aus Übertrag und Ausgleich: Ausgleich.</summary>
    private const string CarryOverExpenseCategory = "finance.settlement";

    /// <summary>Ein einmaliger Posten im Topf Einnahmen ist eine Einnahme, im Topf Sparen ein Sparziel, sonst ein gewöhnlicher Vertrag.</summary>
    private static CommitmentType TypeFor(Pot pot) => pot switch
    {
        Pot.Income => CommitmentType.Income,
        Pot.Savings => CommitmentType.SavingsGoal,
        _ => CommitmentType.Contract,
    };

    private static void Apply(PlanPosition position, CheckedPosition value)
    {
        position.CategoryId = value.CategoryId;
        position.Pot = value.Pot;
        position.AccountId = value.AccountId;
    }

    private sealed record PositionInput(string? Label, decimal Amount, int DueDay, Guid? CategoryId, string? Pot, Guid? AccountId);

    private sealed record CheckedPosition(string Label, decimal Amount, int DueDay, Guid CategoryId, Pot Pot, Guid? AccountId);

    private readonly record struct PositionCheck(string? Error, CheckedPosition? Value);

    /// <summary>
    /// Die Regeln eines Postens (R3): Bezeichnung 1 bis 100 Zeichen, Betrag größer als null, Tag im
    /// Monat, Topf und Kategorie wie bei Verträgen (Einnahmen nur mit Einnahme-Kategorien), das Konto
    /// gehört der Person und ist offen. Wie bei Verträgen erst die Form, dann fremde Kennungen.
    /// </summary>
    private static async Task<PositionCheck> CheckPositionAsync(
        PositionInput input, int year, int month, Guid ownerId, Guid? storedAccountId,
        IAccountLookup accounts, ICategoryLookup categories, CancellationToken cancellationToken)
    {
        if (!IsValidMonth(year, month))
        {
            return new(ErrorCodes.MonthInvalid, null);
        }
        var label = (input.Label ?? "").Trim();
        if (label.Length == 0)
        {
            return new(ErrorCodes.PositionLabelRequired, null);
        }
        if (label.Length > PlanRules.LabelMaxLength)
        {
            return new(ErrorCodes.PositionLabelTooLong, null);
        }
        if (!IsValidAmount(input.Amount))
        {
            return new(ErrorCodes.AmountInvalid, null);
        }
        if (input.DueDay < 1 || input.DueDay > DateTime.DaysInMonth(year, month))
        {
            return new(ErrorCodes.PositionDayInvalid, null);
        }
        if (input.Pot is null)
        {
            return new(ErrorCodes.PotRequired, null);
        }
        if (!PotKey.TryParse(input.Pot, out var pot))
        {
            return new(ErrorCodes.PositionPotInvalid, null);
        }
        if (input.CategoryId is not { } categoryId)
        {
            return new(ErrorCodes.CategoryRequired, null);
        }

        if (input.AccountId is { } accountId)
        {
            if (await accounts.FindAsync(ownerId, accountId, cancellationToken) is not { } account)
            {
                return new(ErrorCodes.AccountNotFound, null);
            }
            if (!account.Active && accountId != storedAccountId)
            {
                return new(ErrorCodes.AccountClosed, null);
            }
        }
        if (await categories.FindAsync(ownerId, categoryId, cancellationToken) is not { } category)
        {
            return new(ErrorCodes.CategoryNotFound, null);
        }
        if (category.IsIncome != (pot == Pot.Income))
        {
            return new(ErrorCodes.PositionCategoryInvalid, null);
        }

        return new(null, new CheckedPosition(label, input.Amount, input.DueDay, categoryId, pot, input.AccountId));
    }

    /// <summary>Größer als null, höchstens der größte Betrag der Spalte und höchstens zwei Nachkommastellen.</summary>
    private static bool IsValidAmount(decimal amount) =>
        amount > 0 && IsValidCarryOverAmount(amount);

    /// <summary>Der Rest des Vormonats darf auch 0 oder negativ sein, sonst gelten dieselben Grenzen wie für einen Betrag.</summary>
    private static bool IsValidCarryOverAmount(decimal amount) =>
        Math.Abs(amount) <= BookingRules.MaxAmount && decimal.Round(amount, 2) == amount;
}
