using Duofy.Schemas;
using Duofy.Data;
using Duofy.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api;

/// <summary>
/// Die Konten (/api/v1/accounts): lesen, anlegen und ändern. Löschen gibt es nicht; ein Konto
/// wird aufgelöst (active false) und kann wieder geöffnet werden.
///
/// Die Konten gehören einer Person. Ohne <c>owner</c> sind es die eigenen; die Konten eines anderen
/// Mitglieds nur so weit, wie es im Bereich Konten freigegeben hat (Spec 68), geprüft von <see cref="IAccess"/>.
/// Ein Konto ohne Freigabe sieht aus wie ein unbekanntes: 404 mit dem Code account_not_found. So verrät keine
/// Antwort, ob es die Kennung gibt.
/// </summary>
internal static class AccountEndpoints
{
    private const string LogCategory = "Duofy.Api.Accounts";

    public static void MapAccountEndpoints(this IEndpointRouteBuilder v1)
    {
        var accounts = v1.MapGroup("/accounts").RequireAuthorization();
        accounts.MapGet("", ListAsync);
        accounts.MapPost("", CreateAsync);
        accounts.MapPatch("/{id:guid}", UpdateAsync);
    }

    private static IResult NotFound() =>
        Results.Json(new ErrorResponse(ErrorCodes.AccountNotFound), statusCode: StatusCodes.Status404NotFound);

    private static AccountResponse ToResponse(Account account, DateOnly today) => new(
        account.Id,
        account.Name,
        AccountTypeKey.ToKey(account.Type),
        account.OpeningBalance,
        account.OpeningDate,
        account.CountsAsAvailable,
        account.Iban,
        account.IsDefault,
        account.Active,
        account.Balance(today),
        AccountIcons.Effective(AccountTypeKey.ToKey(account.Type), account.Icon));

    /// <summary>
    /// Alle Konten der Person, das Standardkonto zuerst, dann nach Namen. Mit <c>?active=true</c>
    /// nur die offenen, so wie Auswahllisten sie brauchen; ohne den Filter auch die aufgelösten. Mit
    /// <c>?owner=</c> die Konten dieser Person, wenn sie Ansehen freigegeben hat.
    /// </summary>
    private static async Task<IResult> ListAsync(
        bool? active,
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
        if ((await access.RequireAsync(ownerId, Area.Accounts, AccessLevel.View, cancellationToken)).Refusal(ApiErrors.OwnerNotFound) is { } refused)
        {
            return refused;
        }

        var accounts = await db.Accounts
            .AsNoTracking()
            .Where(a => a.OwnerId == ownerId && (active == null || a.Active == active))
            .ToListAsync(cancellationToken);

        var today = Berlin.Today(time);
        await AttachBookingsAsync(db, accounts, today, cancellationToken);
        var sorted = accounts
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(a => ToResponse(a, today))
            .ToList();
        return Results.Ok(sorted);
    }

    /// <summary>Legt ein Konto an, mit <c>ownerId</c> für ein anderes Mitglied, das Anlegen freigegeben hat.</summary>
    private static async Task<IResult> CreateAsync(
        CreateAccountRequest request,
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
        var ownerId = request.OwnerId ?? callerId;
        if ((await access.RequireAsync(ownerId, Area.Accounts, AccessLevel.Create, cancellationToken)).Refusal(ApiErrors.OwnerNotFound) is { } refused)
        {
            return refused;
        }

        var today = Berlin.Today(time);
        if (AccountInput.CheckName(request.Name, out var name) is { } nameError)
        {
            return ApiErrors.BadRequest(nameError);
        }
        if (!AccountTypeKey.TryParse(request.Type, out var type))
        {
            return ApiErrors.BadRequest(ErrorCodes.AccountTypeInvalid);
        }
        if (AccountInput.CheckOpeningBalance(request.OpeningBalance) is { } balanceError)
        {
            return ApiErrors.BadRequest(balanceError);
        }
        if (AccountInput.CheckOpeningDate(request.OpeningDate, today) is { } dateError)
        {
            return ApiErrors.BadRequest(dateError);
        }
        if (AccountInput.CheckIban(request.Iban, out var iban) is { } ibanError)
        {
            return ApiErrors.BadRequest(ibanError);
        }
        if (AccountInput.CheckIcon(request.Icon, out var icon) is { } iconError)
        {
            return ApiErrors.BadRequest(iconError);
        }

        var now = time.GetUtcNow();
        var account = new Account
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = name,
            Type = type,
            OpeningBalance = request.OpeningBalance,
            OpeningDate = request.OpeningDate,
            CountsAsAvailable = request.CountsAsAvailable ?? Account.CountsAsAvailableFor(type),
            Iban = iban,
            Icon = icon,
            Active = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        // Alte Markierung entfernen und neues Konto speichern geschieht zusammen oder gar nicht.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Ein neues Standardkonto nimmt einem bestehenden die Markierung weg; das ist eine Änderung an diesem
        // Konto und braucht bei einem anderen Mitglied dessen Freigabe Ändern (Spec 68).
        if (request.IsDefault == true
            && await db.Accounts.AnyAsync(a => a.OwnerId == ownerId && a.IsDefault, cancellationToken)
            && (await access.RequireAsync(ownerId, Area.Accounts, AccessLevel.Edit, cancellationToken)).Refusal(ApiErrors.OwnerNotFound) is { } notAllowed)
        {
            return notAllowed;
        }

        // Das erste Girokonto wird von selbst Standardkonto, solange es noch keines gibt (R4).
        var becomesDefault = request.IsDefault == true
            || (type == AccountType.Checking
                && !await db.Accounts.AnyAsync(a => a.OwnerId == ownerId && (a.IsDefault || a.Type == AccountType.Checking), cancellationToken));
        if (becomesDefault)
        {
            await ClearDefaultAsync(db, ownerId, cancellationToken);
            account.IsDefault = true;
        }

        db.Accounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        loggers.CreateLogger(LogCategory).LogInformation(
            "Konto {AccountId} für {OwnerId} von {UserId} angelegt.", account.Id, ownerId, callerId);
        return Results.Created($"/api/v1/accounts/{account.Id}", ToResponse(account, today));
    }

    /// <summary>
    /// Ändert die Felder, die in der Anfrage stehen; die anderen bleiben. Es gelten dieselben
    /// Regeln wie beim Anlegen. Auflösen nimmt die Markierung als Standardkonto weg (R5). Das Konto eines
    /// anderen Mitglieds braucht dessen Freigabe Ändern.
    /// </summary>
    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateAccountRequest request,
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

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (account is null)
        {
            return NotFound();
        }
        if ((await access.RequireAsync(account.OwnerId, Area.Accounts, AccessLevel.Edit, cancellationToken)).Refusal(NotFound) is { } refused)
        {
            return refused;
        }
        var ownerId = account.OwnerId;

        var today = Berlin.Today(time);
        var name = account.Name;
        if (request.Name is not null && AccountInput.CheckName(request.Name, out name) is { } nameError)
        {
            return ApiErrors.BadRequest(nameError);
        }
        var type = account.Type;
        if (request.Type is not null && !AccountTypeKey.TryParse(request.Type, out type))
        {
            return ApiErrors.BadRequest(ErrorCodes.AccountTypeInvalid);
        }
        if (request.OpeningBalance is { } openingBalance && AccountInput.CheckOpeningBalance(openingBalance) is { } balanceError)
        {
            return ApiErrors.BadRequest(balanceError);
        }
        if (request.OpeningDate is { } openingDate)
        {
            if (AccountInput.CheckOpeningDate(openingDate, today) is { } dateError)
            {
                return ApiErrors.BadRequest(dateError);
            }
            // Buchungen am Starttag oder davor gibt es nicht (sie stecken im Startbetrag). Der Starttag
            // darf deshalb nur auf einen Tag vor jeder Buchung des Kontos rücken (R8).
            if (await db.Bookings.AnyAsync(
                    b => (b.AccountId == id || b.CounterAccountId == id) && b.PaymentDate <= openingDate, cancellationToken))
            {
                return ApiErrors.BadRequest(ErrorCodes.OpeningDateAfterBooking);
            }
        }
        var iban = account.Iban;
        if (request.Iban is not null && AccountInput.CheckIban(request.Iban, out iban) is { } ibanError)
        {
            return ApiErrors.BadRequest(ibanError);
        }

        var storedIcon = account.Icon;
        if (request.Icon is not null && AccountInput.CheckIcon(request.Icon, out storedIcon) is { } iconError)
        {
            return ApiErrors.BadRequest(iconError);
        }

        var active = request.Active ?? account.Active;
        if (request.IsDefault == true && !active)
        {
            return ApiErrors.BadRequest(ErrorCodes.AccountClosed);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (request.IsDefault == true && !account.IsDefault)
        {
            await ClearDefaultAsync(db, ownerId, cancellationToken);
        }

        var wasActive = account.Active;
        account.Name = name;
        account.Type = type;
        account.OpeningBalance = request.OpeningBalance ?? account.OpeningBalance;
        account.OpeningDate = request.OpeningDate ?? account.OpeningDate;
        account.CountsAsAvailable = request.CountsAsAvailable ?? account.CountsAsAvailable;
        account.Iban = iban;
        account.Icon = storedIcon;
        account.Active = active;
        account.IsDefault = active && (request.IsDefault ?? account.IsDefault);
        account.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await AttachBookingsAsync(db, [account], today, cancellationToken);
        if (wasActive != active)
        {
            loggers.CreateLogger(LogCategory).LogInformation(
                active ? "Konto {AccountId} von {OwnerId} durch {UserId} wieder geöffnet." : "Konto {AccountId} von {OwnerId} durch {UserId} aufgelöst.",
                account.Id, ownerId, callerId);
        }
        return Results.Ok(ToResponse(account, today));
    }

    /// <summary>
    /// Holt die Buchungen bis heute, die die Konten berühren, und hängt sie an die Konten, damit
    /// <see cref="Account.Balance"/> den Stand ausrechnen kann. Eine Umbuchung zwischen zwei der
    /// Konten hängt an beiden.
    /// </summary>
    private static async Task AttachBookingsAsync(
        DuofyDbContext db, IReadOnlyCollection<Account> accounts, DateOnly today, CancellationToken cancellationToken)
    {
        var ids = accounts.Select(a => a.Id).ToList();
        var bookings = await db.Bookings
            .AsNoTracking()
            .Include(b => b.Lines)
            .Where(b => ids.Contains(b.AccountId) || (b.CounterAccountId != null && ids.Contains(b.CounterAccountId.Value)))
            .Where(b => b.PaymentDate <= today)
            .ToListAsync(cancellationToken);

        foreach (var account in accounts)
        {
            account.Bookings = bookings.Where(b => b.AccountId == account.Id || b.CounterAccountId == account.Id).ToList();
        }
    }

    /// <summary>
    /// Nimmt allen Konten der Person die Markierung als Standardkonto. Das geht direkt in der
    /// Datenbank und vor dem Setzen der neuen Markierung; sonst gäbe es kurz zwei
    /// Standardkonten, und der eindeutige Index würde das Speichern ablehnen.
    /// </summary>
    private static Task ClearDefaultAsync(DuofyDbContext db, Guid ownerId, CancellationToken cancellationToken) =>
        db.Accounts
            .Where(a => a.OwnerId == ownerId && a.IsDefault)
            .ExecuteUpdateAsync(set => set.SetProperty(a => a.IsDefault, false), cancellationToken);
}

/// <summary>
/// Prüft die Eingaben eines Kontos. Jede Methode liefert den Fehlercode oder null, wenn alles stimmt.
/// </summary>
internal static class AccountInput
{
    /// <summary>Der Name, ohne Leerzeichen am Anfang und Ende, 1 bis 100 Zeichen.</summary>
    public static string? CheckName(string? raw, out string name)
    {
        name = (raw ?? "").Trim();
        if (name.Length == 0)
        {
            return ErrorCodes.AccountNameRequired;
        }
        return name.Length > MoneyAccountRules.NameMaxLength ? ErrorCodes.AccountNameTooLong : null;
    }

    /// <summary>Höchstens zwei Nachkommastellen und innerhalb der Spalte numeric(12,2); negativ ist erlaubt.</summary>
    public static string? CheckOpeningBalance(decimal amount) =>
        decimal.Round(amount, 2) == amount && Math.Abs(amount) <= MoneyAccountRules.MaxOpeningBalance
            ? null
            : ErrorCodes.OpeningBalanceInvalid;

    /// <summary>Der Starttag darf heute sein, aber nicht in der Zukunft.</summary>
    public static string? CheckOpeningDate(DateOnly date, DateOnly today) =>
        date > today ? ErrorCodes.OpeningDateInFuture : null;

    /// <summary>Eine leere Eingabe heißt "kein eigenes Symbol" (null, das Standardsymbol der Art); sonst muss es ein bekannter Name sein.</summary>
    public static string? CheckIcon(string? raw, out string? icon)
    {
        icon = null;
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }
        if (!AccountIcons.IsKnown(raw))
        {
            return ErrorCodes.AccountIconInvalid;
        }
        icon = raw;
        return null;
    }

    /// <summary>Eine leere Eingabe heißt "keine IBAN" (null); sonst muss sie gültig sein.</summary>
    public static string? CheckIban(string? raw, out string? iban)
    {
        iban = null;
        if (Iban.IsEmpty(raw))
        {
            return null;
        }
        if (!Iban.TryNormalize(raw, out var normalized))
        {
            return ErrorCodes.IbanInvalid;
        }
        iban = normalized;
        return null;
    }
}

/// <summary>
/// Übersetzt eine <see cref="AccountType"/> in ihren Schlüssel in der API ("checking") und zurück.
/// Es gilt genau die kleingeschriebene Schreibweise.
/// </summary>
internal static class AccountTypeKey
{
    public static string ToKey(AccountType type) => type.ToString().ToLowerInvariant();

    public static bool TryParse(string? key, out AccountType type)
    {
        foreach (var candidate in Enum.GetValues<AccountType>())
        {
            if (ToKey(candidate) == key)
            {
                type = candidate;
                return true;
            }
        }
        type = default;
        return false;
    }
}
