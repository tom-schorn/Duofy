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

    /// <summary>Das Gezahlte je Eintrag einer Person: die Summe der zugeordneten Ausgaben. Einträge ohne Zahlung fehlen und stehen bei 0.</summary>
    internal static async Task<Dictionary<Guid, decimal>> PaidAsync(DuofyDbContext db, Guid ownerId, CancellationToken cancellationToken)
    {
        var rows = await db.Bookings
            .AsNoTracking()
            .Where(b => b.OwnerId == ownerId)
            .SelectMany(b => b.Lines
                .Where(l => l.DebtId != null)
                .Select(l => new { DebtId = l.DebtId!.Value, l.Amount, l.Pot, IsTransfer = b.CounterAccountId != null }))
            .ToListAsync(cancellationToken);
        return rows
            .Where(r => DebtPayments.IsPayment(r.IsTransfer, r.Pot))
            .GroupBy(r => r.DebtId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));
    }

    /// <summary>Die Kennungen der Einträge der Person, die ganz bezahlt sind; der Plan verlangt für sie keine Rate mehr.</summary>
    internal static async Task<HashSet<Guid>> SettledIdsAsync(DuofyDbContext db, Guid ownerId, CancellationToken cancellationToken)
    {
        var debts = await db.Debts.AsNoTracking().Where(d => d.OwnerId == ownerId).ToListAsync(cancellationToken);
        if (debts.Count == 0)
        {
            return [];
        }
        var paid = await PaidAsync(db, ownerId, cancellationToken);
        return debts.Where(d => d.IsSettled(paid.GetValueOrDefault(d.Id))).Select(d => d.Id).ToHashSet();
    }

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

    private static DebtResponse ToResponse(Debt debt, decimal paid) => new(
        debt.Id, debt.Kind.ToString().ToLowerInvariant(), debt.Name, debt.Creditor, debt.OriginalAmount, debt.DueDate, debt.Note,
        paid, debt.OpenOf(paid), debt.IsSettled(paid));

    private static async Task<IResult> ListAsync(Guid? owner, IAccess access, DuofyDbContext db, CancellationToken cancellationToken)
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
        var paid = await PaidAsync(db, ownerId, cancellationToken);
        return Results.Ok(debts
            .OrderBy(d => d.DueDate is null)
            .ThenBy(d => d.DueDate)
            .ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(d => ToResponse(d, paid.GetValueOrDefault(d.Id)))
            .ToList());
    }

    private static async Task<IResult> CreateAsync(
        CreateDebtRequest request, IAccess access, DuofyDbContext db, TimeProvider time, ILoggerFactory loggers, CancellationToken cancellationToken)
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
        if (Check(request.Name, request.Creditor, request.OriginalAmount, request.Note) is { } error)
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
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Debts.Add(debt);
        await db.SaveChangesAsync(cancellationToken);

        loggers.CreateLogger(LogCategory).LogInformation("Eintrag {DebtId} für {OwnerId} von {UserId} angelegt.", debt.Id, ownerId, callerId);
        return Results.Created($"/api/v1/debts/{debt.Id}", ToResponse(debt, 0m));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, UpdateDebtRequest request, IAccess access, DuofyDbContext db, TimeProvider time, CancellationToken cancellationToken)
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
        if (Check(name, creditor, amount, note) is { } error)
        {
            return error;
        }

        debt.Name = name.Trim();
        debt.Creditor = creditor.Trim();
        debt.OriginalAmount = amount;
        debt.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        debt.DueDate = request.DueDate ?? (request.ClearDueDate == true ? null : debt.DueDate);
        debt.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        var paid = await PaidAsync(db, debt.OwnerId, cancellationToken);
        return Results.Ok(ToResponse(debt, paid.GetValueOrDefault(debt.Id)));
    }

    /// <summary>Löscht einen Eintrag ohne Zuordnungen; mit Zuordnungen antwortet die API 409, damit keine Zahlung still ihren Bezug verliert.</summary>
    private static async Task<IResult> DeleteAsync(Guid id, IAccess access, DuofyDbContext db, ILoggerFactory loggers, CancellationToken cancellationToken)
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
        var inUse = await db.Bookings.AnyAsync(b => b.OwnerId == debt.OwnerId && b.Lines.Any(l => l.DebtId == id), cancellationToken)
            || await db.Commitments.AnyAsync(c => c.OwnerId == debt.OwnerId && c.DebtId == id, cancellationToken);
        if (inUse)
        {
            return Results.Json(new ErrorResponse(ErrorCodes.DebtHasBookings), statusCode: StatusCodes.Status409Conflict);
        }

        db.Debts.Remove(debt);
        await db.SaveChangesAsync(cancellationToken);
        loggers.CreateLogger(LogCategory).LogInformation("Eintrag {DebtId} von {OwnerId} durch {UserId} gelöscht.", id, debt.OwnerId, callerId);
        return Results.NoContent();
    }

    /// <summary>Prüft Name, Gläubiger, Betrag und Notiz; liefert die Antwort auf den ersten Fehler, sonst null.</summary>
    private static IResult? Check(string? name, string? creditor, decimal amount, string? note)
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
        return null;
    }
}
