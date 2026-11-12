using Duofy.ApiClient;
using Duofy.Schemas;
using Duofy.Shared.Services;

namespace Duofy.Shared.Tests;

// Ersatzteile für die Buchungsseite: Sie merken sich, was die Seite aufgerufen hat, und liefern
// die Antworten, die der Test vorgibt. Es ist kein Netz und keine Datenbank im Spiel.

/// <summary>Eine Uhr, die immer dieselbe Zeit zeigt: der 21. Oktober 2026, 10 Uhr in Berlin.</summary>
public sealed class FixedTimeProvider : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
}

public sealed class FakeAccountChoices : IAccountChoices
{
    public List<AccountChoice> Accounts { get; } = [];

    /// <summary>Gesetzt, scheitert das Laden mit diesem Fehlercode.</summary>
    public string? Error { get; set; }

    /// <summary>Der Besitzer jeder Abfrage, null für die eigenen Konten.</summary>
    public List<Guid?> Owners { get; } = [];

    public Task<ApiResult<IReadOnlyList<AccountChoice>>> ListAsync(Guid? owner = null, CancellationToken cancellationToken = default)
    {
        Owners.Add(owner);
        return Task.FromResult(Error is null
            ? ApiResult<IReadOnlyList<AccountChoice>>.Success(Accounts.ToList())
            : ApiResult<IReadOnlyList<AccountChoice>>.Failure(Error));
    }
}

public sealed class FakeCategoryApi : ICategoryApi
{
    public static readonly CategoryResponse Groceries =
        new(Guid.NewGuid(), "household.groceries", "Lebensmittel", "household", PotKeys.Needs, true);

    public static readonly CategoryResponse PersonalCare =
        new(Guid.NewGuid(), "household.personal_care", "Körperpflege", "household", PotKeys.Needs, true);

    public static readonly CategoryResponse Restaurant =
        new(Guid.NewGuid(), "leisure.dining", "Essen gehen", "leisure", PotKeys.Wants, true);

    public static readonly CategoryResponse Salary =
        new(Guid.NewGuid(), "income.earned", "Gehalt & Lohn", "income", PotKeys.Income, true);

    public List<CategoryResponse> All { get; } = [Groceries, PersonalCare, Restaurant, Salary];
    public List<SaveCategoryRequest> Created { get; } = [];
    public ApiResult<CategoryResponse>? CreateResult { get; set; }

    /// <summary>Der Besitzer jeder Abfrage, null für die eigenen Kategorien.</summary>
    public List<Guid?> Owners { get; } = [];

    /// <summary>Gesetzt, wartet jede Abfrage darauf (für Tests, die den Dialog während des Ladens bedienen).</summary>
    public Task? ListGate { get; set; }

    public async Task<ApiResult<IReadOnlyList<CategoryResponse>>> ListAsync(Guid? owner = null, CancellationToken cancellationToken = default)
    {
        Owners.Add(owner);
        if (ListGate is { } gate)
        {
            await gate;
        }
        return ApiResult<IReadOnlyList<CategoryResponse>>.Success(All.ToList());
    }

    /// <summary>Der Besitzer jedes Anlegens, null für die eigenen Kategorien.</summary>
    public List<Guid?> CreatedOwners { get; } = [];

    public Task<ApiResult<CategoryResponse>> CreateAsync(SaveCategoryRequest request, Guid? owner = null, CancellationToken cancellationToken = default)
    {
        Created.Add(request);
        CreatedOwners.Add(owner);
        if (CreateResult is { } result)
        {
            return Task.FromResult(result);
        }
        var category = new CategoryResponse(Guid.NewGuid(), null, request.Name!, request.Group!, request.Pot!, false);
        All.Add(category);
        return Task.FromResult(ApiResult<CategoryResponse>.Success(category));
    }

    public Task<ApiResult<CategoryResponse>> UpdateAsync(Guid id, SaveCategoryRequest request, Guid? owner = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult<CategoryResponse>.Failure(ClientErrorCodes.Unexpected));

    public Task<ApiResult> DeleteAsync(Guid id, Guid? owner = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult.Failure(ClientErrorCodes.Unexpected));
}

/// <summary>
/// Eine Buchungs-API im Speicher. Sie wählt den Monat und das Konto aus wie die echte API und
/// rechnet die Summen; so zeigen die Seitentests, was die Seite aus den Antworten macht.
/// </summary>
public sealed class FakeBookingApi : IBookingApi
{
    public List<BookingResponse> Bookings { get; } = [];
    public List<(int Year, int Month, Guid? AccountId)> ListCalls { get; } = [];
    public List<SaveBookingRequest> Created { get; } = [];
    public List<(Guid Id, SaveBookingRequest Request)> Updated { get; } = [];
    public List<Guid> Deleted { get; } = [];
    public string? CreateError { get; set; }

    /// <summary>Der Besitzer jeder Abfrage eines Monats, null für die eigenen Buchungen.</summary>
    public List<Guid?> ListOwners { get; } = [];

    public Task<ApiResult<BookingMonthResponse>> ListMonthAsync(
        int year, int month, Guid? accountId, Guid? owner = null, CancellationToken cancellationToken = default)
    {
        ListCalls.Add((year, month, accountId));
        ListOwners.Add(owner);

        var shown = Bookings
            .Where(b => b.Date.Year == year && b.Date.Month == month)
            .Where(b => accountId is null || b.AccountId == accountId || b.CounterAccountId == accountId)
            .OrderByDescending(b => b.Date)
            .ToList();

        decimal income = 0, expenses = 0;
        foreach (var booking in shown)
        {
            if (booking.Kind == BookingKinds.Transfer)
            {
                if (accountId is null)
                {
                    continue;
                }
                if (booking.CounterAccountId == accountId)
                {
                    income += booking.Amount;
                }
                else
                {
                    expenses += booking.Amount;
                }
            }
            else if (booking.Kind == BookingKinds.Income)
            {
                income += booking.Amount;
            }
            else
            {
                expenses += booking.Amount;
            }
        }
        return Task.FromResult(ApiResult<BookingMonthResponse>.Success(new BookingMonthResponse(shown, income, expenses)));
    }

    /// <summary>Die Fehlercodes, mit denen die API Anfragen abgewiesen hat, in der Reihenfolge (weder in Created noch in Updated).</summary>
    public List<string> Refused { get; } = [];

    /// <summary>
    /// Was die echte API zu Topf und Kategorie verlangt (BookingValidator): Jede Zeile einer Ausgabe oder Einnahme braucht Topf und
    /// Kategorie, eine Umbuchung beide Töpfe und keine Kategorie. So zeigen die Tests nur Buchungen, die die API auch annähme.
    /// </summary>
    private static string? CheckPotsAndCategories(SaveBookingRequest request)
    {
        var lines = request.Lines ?? [];
        if (request.Kind == BookingKinds.Transfer)
        {
            return lines.Count == 0 || lines[0].Pot is null || request.TargetPot is null ? ErrorCodes.TransferPotsRequired : null;
        }
        foreach (var line in lines)
        {
            if (line.Pot is null)
            {
                return ErrorCodes.PotRequired;
            }
            if (line.CategoryId is null)
            {
                return ErrorCodes.CategoryRequired;
            }
        }
        return null;
    }

    public Task<ApiResult<BookingResponse>> CreateAsync(SaveBookingRequest request, CancellationToken cancellationToken = default)
    {
        if (CheckPotsAndCategories(request) is { } refusal)
        {
            Refused.Add(refusal);
            return Task.FromResult(ApiResult<BookingResponse>.Failure(refusal));
        }
        Created.Add(request);
        if (CreateError is not null)
        {
            return Task.FromResult(ApiResult<BookingResponse>.Failure(CreateError));
        }
        var saved = ToResponse(Guid.NewGuid(), request);
        Bookings.Add(saved);
        return Task.FromResult(ApiResult<BookingResponse>.Success(saved));
    }

    public Task<ApiResult<BookingResponse>> UpdateAsync(Guid id, SaveBookingRequest request, CancellationToken cancellationToken = default)
    {
        if (CheckPotsAndCategories(request) is { } refusal)
        {
            Refused.Add(refusal);
            return Task.FromResult(ApiResult<BookingResponse>.Failure(refusal));
        }
        Updated.Add((id, request));
        var saved = ToResponse(id, request);
        Bookings.RemoveAll(b => b.Id == id);
        Bookings.Add(saved);
        return Task.FromResult(ApiResult<BookingResponse>.Success(saved));
    }

    public Task<ApiResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Deleted.Add(id);
        Bookings.RemoveAll(b => b.Id == id);
        return Task.FromResult(ApiResult.Success());
    }

    public static BookingResponse ToResponse(Guid id, SaveBookingRequest request) => new(
        id, request.Kind!, request.AccountId, request.CounterAccountId, request.Date, request.Amount, request.Note,
        (request.Lines ?? []).Select(l => new BookingLineResponse(Guid.NewGuid(), l.Amount, l.Pot!, l.CategoryId)).ToList());

    /// <summary>Eine Ausgabe mit einer Zeile, für Tests der Liste.</summary>
    public static BookingResponse Expense(Guid account, DateOnly date, decimal amount, CategoryResponse category, string? note = null) => new(
        Guid.NewGuid(), BookingKinds.Expense, account, null, date, amount, note,
        [new BookingLineResponse(Guid.NewGuid(), amount, category.SuggestedPot, category.Id)]);

    public static BookingResponse Income(Guid account, DateOnly date, decimal amount) => new(
        Guid.NewGuid(), BookingKinds.Income, account, null, date, amount, null,
        [new BookingLineResponse(Guid.NewGuid(), amount, PotKeys.Income, FakeCategoryApi.Salary.Id)]);

    public static BookingResponse Transfer(Guid from, Guid to, DateOnly date, decimal amount) => new(
        Guid.NewGuid(), BookingKinds.Transfer, from, to, date, amount, null,
        [new BookingLineResponse(Guid.NewGuid(), amount, PotKeys.Savings, null)], TargetPot: PotKeys.Wants);
}
