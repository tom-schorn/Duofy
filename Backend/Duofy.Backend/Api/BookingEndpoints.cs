using Duofy.Schemas;
using Duofy.Data;
using Duofy.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api;

/// <summary>
/// Die Buchungen (/api/v1/bookings): einen Monat lesen, anlegen, ändern und löschen.
///
/// Die Regeln stehen alle im <see cref="BookingValidator"/>; die Endpunkte prüfen damit, speichern
/// und lesen. Ohne <c>owner</c> sind es die eigenen Buchungen; die eines anderen Mitglieds nur so weit, wie es
/// im Bereich Buchungen freigegeben hat (Spec 68), geprüft von <see cref="IAccess"/>. Wer für jemanden bucht
/// oder eine Buchung ändert, muss auch dessen Konten ansehen dürfen; sonst ist das Konto unbekannt. Eine
/// Buchung ohne Freigabe sieht aus wie eine unbekannte: 404.
/// </summary>
internal static class BookingEndpoints
{
    private const string LogCategory = "Duofy.Api.Bookings";

    // Weiter zurück oder voraus blättert niemand; so bleibt die Abfrage immer ein echter Monat.
    private const int FirstYear = 2000;
    private const int LastYear = 2100;

    public static void MapBookingEndpoints(this IEndpointRouteBuilder v1)
    {
        var bookings = v1.MapGroup("/bookings").RequireAuthorization();
        bookings.MapGet("", ListMonthAsync);
        bookings.MapPost("", CreateAsync);
        bookings.MapPut("/{id:guid}", UpdateAsync);
        bookings.MapDelete("/{id:guid}", DeleteAsync);
    }

    /// <summary>
    /// True, wenn die angemeldete Person die Konten von <paramref name="ownerId"/> ansehen darf. Ohne das ist jedes
    /// Konto in einer Buchung für sie unbekannt, so wie es die Prüfung der Buchung auch meldet.
    /// </summary>
    private static async Task<bool> SeesAccountsAsync(IAccess access, Guid ownerId, CancellationToken cancellationToken) =>
        (await access.RequireAsync(ownerId, Area.Accounts, AccessLevel.View, cancellationToken)).Outcome == AccessOutcome.Allowed;

    /// <summary>Die Buchungen mit ihren Zeilen in der gespeicherten Reihenfolge.</summary>
    internal static IQueryable<Booking> WithLines(DuofyDbContext db) =>
        db.Bookings.Include(b => b.Lines.OrderBy(l => EF.Property<int>(l, DuofyDbContext.LinePosition)));

    /// <summary>
    /// Die Art steht nicht in der Tabelle; sie ergibt sich aus der Buchung: mit Gegenkonto eine
    /// Umbuchung, sonst entscheidet der Topf der Zeilen.
    /// </summary>
    private static string KindOf(Booking booking) =>
        booking.IsTransfer ? BookingKinds.Transfer
        : booking.Lines.Count > 0 && booking.Lines[0].Pot == Pot.Income ? BookingKinds.Income
        : BookingKinds.Expense;

    internal static BookingResponse ToResponse(Booking booking) => new(
        booking.Id,
        KindOf(booking),
        booking.AccountId,
        booking.CounterAccountId,
        booking.PaymentDate,
        booking.Amount,
        booking.Note,
        booking.Lines.Select(l => new BookingLineResponse(l.Id, l.Amount, PotKey.ToKey(l.Pot), l.CategoryId, l.PositionId)).ToList(),
        booking.PlanYear,
        booking.PlanMonth,
        booking.TargetPot is { } target ? PotKey.ToKey(target) : null);

    /// <summary>
    /// Die Buchungen eines Monats, die neueste zuerst, mit den Summen (R8). Mit <c>accountId</c>
    /// nur die Buchungen, die dieses Konto berühren; Umbuchungen zählen dann als Ein- oder Ausgang. Mit
    /// <c>?owner=</c> die Buchungen dieser Person, wenn sie Ansehen freigegeben hat; der Filter nach einem ihrer
    /// Konten braucht dazu, dass sie auch ihre Konten zeigt.
    /// </summary>
    private static async Task<IResult> ListMonthAsync(
        int year,
        int month,
        Guid? accountId,
        Guid? owner,
        IAccess access,
        DuofyDbContext db,
        IAccountLookup accounts,
        CancellationToken cancellationToken)
    {
        if (access.CallerId is not { } callerId)
        {
            return Results.Unauthorized();
        }
        var ownerId = owner ?? callerId;
        if ((await access.RequireAsync(ownerId, Area.Book, AccessLevel.View, cancellationToken)).Refusal(ApiErrors.OwnerNotFound) is { } refused)
        {
            return refused;
        }
        if (month is < 1 or > 12 || year is < FirstYear or > LastYear)
        {
            return ApiErrors.BadRequest(ErrorCodes.MonthInvalid);
        }
        if (accountId is { } filter
            && (!await SeesAccountsAsync(access, ownerId, cancellationToken) || await accounts.FindAsync(ownerId, filter, cancellationToken) is null))
        {
            return Results.Json(new ErrorResponse(ErrorCodes.AccountNotFound), statusCode: StatusCodes.Status404NotFound);
        }

        var first = new DateOnly(year, month, 1);
        var next = first.AddMonths(1);
        var bookings = await WithLines(db)
            .AsNoTracking()
            .Where(b => b.OwnerId == ownerId && b.PaymentDate >= first && b.PaymentDate < next)
            .Where(b => accountId == null || b.AccountId == accountId || b.CounterAccountId == accountId)
            .OrderByDescending(b => b.PaymentDate)
            .ThenByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        var sums = MonthBook.SumsOf(bookings, accountId);
        return Results.Ok(new BookingMonthResponse(bookings.Select(ToResponse).ToList(), sums.Income, sums.Expenses));
    }

    /// <summary>Legt eine Buchung an, mit <c>ownerId</c> für ein anderes Mitglied, das Anlegen freigegeben hat.</summary>
    private static async Task<IResult> CreateAsync(
        SaveBookingRequest request,
        IAccess access,
        DuofyDbContext db,
        IAccountLookup accounts,
        ICategoryLookup categories,
        IPositionLookup positions,
        TimeProvider time,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (access.CallerId is not { } callerId)
        {
            return Results.Unauthorized();
        }
        var ownerId = request.OwnerId ?? callerId;
        if ((await access.RequireAsync(ownerId, Area.Book, AccessLevel.Create, cancellationToken)).Refusal(ApiErrors.OwnerNotFound) is { } notAllowed)
        {
            return notAllowed;
        }
        if (!await SeesAccountsAsync(access, ownerId, cancellationToken))
        {
            return Results.NotFound();
        }
        var check = await BookingValidator.CheckAsync(request, ownerId, accounts, categories, positions, time, cancellationToken);
        if (Refusal(check) is { } refused)
        {
            return refused;
        }

        var booking = Add(db, ownerId, request, check, time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        loggers.CreateLogger(LogCategory).LogInformation(
            "Buchung {BookingId} für {OwnerId} von {UserId} angelegt.", booking.Id, ownerId, callerId);
        return Results.Created($"/api/v1/bookings/{booking.Id}", ToResponse(booking));
    }

    /// <summary>
    /// Ändert eine Buchung; jedes Feld darf sich ändern, mit denselben Regeln wie beim Anlegen (R7). Die Buchung
    /// bleibt bei ihrem Besitzer; bei einem anderen Mitglied braucht das dessen Freigabe Ändern. Die beiden
    /// Buchungen eines Übertrags ändern sich nur über den Plan (Spec 66, R9): 409 carry_over_booking.
    /// </summary>
    private static async Task<IResult> UpdateAsync(
        Guid id,
        SaveBookingRequest request,
        IAccess access,
        DuofyDbContext db,
        IAccountLookup accounts,
        ICategoryLookup categories,
        IPositionLookup positions,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (access.CallerId is null)
        {
            return Results.Unauthorized();
        }

        var booking = await WithLines(db).FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (booking is null)
        {
            return Results.NotFound();
        }
        var ownerId = booking.OwnerId;
        if ((await access.RequireAsync(ownerId, Area.Book, AccessLevel.Edit, cancellationToken)).Refusal(() => Results.NotFound()) is { } notAllowed)
        {
            return notAllowed;
        }
        if (!await SeesAccountsAsync(access, ownerId, cancellationToken))
        {
            return Results.NotFound();
        }
        if (await IsCarryOverAsync(db, ownerId, id, cancellationToken))
        {
            return CarryOverConflict();
        }
        var check = await BookingValidator.CheckAsync(request, ownerId, accounts, categories, positions, time, cancellationToken);
        if (Refusal(check) is { } refused)
        {
            return refused;
        }

        // Die alten Zeilen fallen weg (die Datenbank löscht sie), die neuen kommen in der Reihenfolge der Anfrage.
        booking.Lines.Clear();
        Apply(booking, request, check, time.GetUtcNow());
        NumberLines(db, booking);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToResponse(booking));
    }

    /// <summary>
    /// Löscht eine Buchung; bei einem anderen Mitglied braucht das dessen Freigabe Löschen. Eine Buchung des
    /// Übertrags verschwindet nur mit dem Übertrag im Plan (Spec 66, R9): 409 carry_over_booking.
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

        var ownerId = await db.Bookings.Where(b => b.Id == id).Select(b => (Guid?)b.OwnerId).FirstOrDefaultAsync(cancellationToken);
        if (ownerId is null)
        {
            return Results.NotFound();
        }
        if ((await access.RequireAsync(ownerId.Value, Area.Book, AccessLevel.Delete, cancellationToken)).Refusal(() => Results.NotFound()) is { } refused)
        {
            return refused;
        }
        if (await IsCarryOverAsync(db, ownerId.Value, id, cancellationToken))
        {
            return CarryOverConflict();
        }

        var deleted = await db.Bookings
            .Where(b => b.Id == id && b.OwnerId == ownerId)
            .ExecuteDeleteAsync(cancellationToken);
        if (deleted == 0)
        {
            return Results.NotFound();
        }

        loggers.CreateLogger(LogCategory).LogInformation("Buchung {BookingId} von {OwnerId} durch {UserId} gelöscht.", id, ownerId, callerId);
        return Results.NoContent();
    }

    /// <summary>True, wenn die Buchung eine der beiden Buchungen eines Übertrags im Plan der Person ist.</summary>
    private static Task<bool> IsCarryOverAsync(DuofyDbContext db, Guid ownerId, Guid bookingId, CancellationToken cancellationToken) =>
        db.Plans.AnyAsync(
            p => p.OwnerId == ownerId && (p.CarryOverIncomeBookingId == bookingId || p.CarryOverExpenseBookingId == bookingId),
            cancellationToken);

    private static IResult CarryOverConflict() =>
        Results.Json(new ErrorResponse(ErrorCodes.CarryOverBooking), statusCode: StatusCodes.Status409Conflict);

    /// <summary>Die Antwort auf eine abgelehnte Prüfung; null, wenn die Buchung gültig ist.</summary>
    internal static IResult? Refusal(BookingCheck check) => check.Outcome switch
    {
        BookingCheckOutcome.Valid => null,
        BookingCheckOutcome.NotFound => Results.NotFound(),
        _ => ApiErrors.BadRequest(check.Code!),
    };

    /// <summary>
    /// Legt eine geprüfte Buchung im Kontext an, noch ohne zu speichern. Der Plan nutzt das für die
    /// Buchungen beim Abhaken und beim Übertrag, damit sie genauso entstehen wie über das Plus.
    /// </summary>
    internal static Booking Add(DuofyDbContext db, Guid ownerId, SaveBookingRequest request, BookingCheck check, DateTimeOffset now)
    {
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            PaymentDate = request.Date,
            Amount = request.Amount,
            CreatedAt = now,
        };
        Apply(booking, request, check, now);
        db.Bookings.Add(booking);
        NumberLines(db, booking);
        return booking;
    }

    /// <summary>Überträgt die geprüfte Anfrage auf die Buchung, mit neuen Zeilen und dem Planmonat aus der Prüfung.</summary>
    private static void Apply(Booking booking, SaveBookingRequest request, BookingCheck check, DateTimeOffset now)
    {
        booking.AccountId = request.AccountId;
        booking.CounterAccountId = request.Kind == BookingKinds.Transfer ? request.CounterAccountId : null;
        // Der Zieltopf ist geprüft; TryParse kann hier nicht mehr scheitern.
        booking.TargetPot = request.Kind == BookingKinds.Transfer && PotKey.TryParse(request.TargetPot, out var targetPot) ? targetPot : null;
        booking.PaymentDate = request.Date;
        booking.Amount = request.Amount;
        booking.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        booking.PlanYear = check.PlanYear;
        booking.PlanMonth = check.PlanMonth;
        booking.UpdatedAt = now;
        foreach (var line in request.Lines ?? [])
        {
            // Der Topf ist geprüft; TryParse kann hier nicht mehr scheitern.
            PotKey.TryParse(line.Pot, out var pot);
            // Ohne Kennung: EF Core erkennt die Zeile so als neu und vergibt die Kennung selbst.
            booking.Lines.Add(new BookingLine { Amount = line.Amount, Pot = pot, CategoryId = line.CategoryId, PositionId = line.PositionId });
        }
    }

    /// <summary>Hält die Reihenfolge der Zeilen in der Schatten-Spalte "position" fest (0 bis 19).</summary>
    private static void NumberLines(DuofyDbContext db, Booking booking)
    {
        // Beim Ändern hängen die neuen Zeilen nur in der Liste; erst DetectChanges meldet sie EF Core als neu.
        db.ChangeTracker.DetectChanges();
        for (var i = 0; i < booking.Lines.Count; i++)
        {
            db.Entry(booking.Lines[i]).Property(DuofyDbContext.LinePosition).CurrentValue = i;
        }
    }
}
