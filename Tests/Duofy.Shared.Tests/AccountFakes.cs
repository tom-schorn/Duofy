using Duofy.ApiClient;
using Duofy.Schemas;

namespace Duofy.Shared.Tests;

/// <summary>
/// Eine Konten-API im Speicher. Sie merkt sich, was die Seite aufgerufen hat, und wendet
/// Änderungen an wie die echte API (Standardkonto, Auflösen); den Stand rechnet sie nicht.
/// </summary>
public sealed class FakeAccountApi : IAccountApi
{
    public List<AccountResponse> Accounts { get; } = [];
    public List<CreateAccountRequest> Created { get; } = [];
    public List<(Guid Id, UpdateAccountRequest Request)> Updated { get; } = [];
    public string? Error { get; set; }
    public int ListCalls { get; private set; }

    /// <summary>Der Besitzer jeder Abfrage der Liste, null für die eigenen Konten.</summary>
    public List<Guid?> ListOwners { get; } = [];

    public Task<ApiResult<IReadOnlyList<AccountResponse>>> ListAsync(bool activeOnly = false, Guid? owner = null, CancellationToken cancellationToken = default)
    {
        ListCalls++;
        ListOwners.Add(owner);
        return Task.FromResult(ApiResult<IReadOnlyList<AccountResponse>>.Success(
            Accounts.Where(a => !activeOnly || a.Active).ToList()));
    }

    public Task<ApiResult<AccountResponse>> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        Created.Add(request);
        if (Error is not null)
        {
            return Task.FromResult(ApiResult<AccountResponse>.Failure(Error));
        }
        var saved = new AccountResponse(
            Guid.NewGuid(), request.Name!, request.Type!, request.OpeningBalance, request.OpeningDate,
            request.CountsAsAvailable ?? true, request.Iban, false, true, request.OpeningBalance);
        Accounts.Add(saved);
        return Task.FromResult(ApiResult<AccountResponse>.Success(saved));
    }

    public Task<ApiResult<AccountResponse>> UpdateAsync(Guid id, UpdateAccountRequest request, CancellationToken cancellationToken = default)
    {
        Updated.Add((id, request));
        if (Error is not null)
        {
            return Task.FromResult(ApiResult<AccountResponse>.Failure(Error));
        }
        var index = Accounts.FindIndex(a => a.Id == id);
        if (request.IsDefault == true)
        {
            for (var i = 0; i < Accounts.Count; i++)
            {
                Accounts[i] = Accounts[i] with { IsDefault = false };
            }
        }
        var old = Accounts[index];
        var active = request.Active ?? old.Active;
        var saved = old with
        {
            Name = request.Name ?? old.Name,
            Type = request.Type ?? old.Type,
            OpeningBalance = request.OpeningBalance ?? old.OpeningBalance,
            OpeningDate = request.OpeningDate ?? old.OpeningDate,
            CountsAsAvailable = request.CountsAsAvailable ?? old.CountsAsAvailable,
            Iban = request.Iban is null ? old.Iban : request.Iban.Length == 0 ? null : request.Iban,
            Active = active,
            IsDefault = active && (request.IsDefault ?? old.IsDefault),
        };
        Accounts[index] = saved;
        return Task.FromResult(ApiResult<AccountResponse>.Success(saved));
    }

    public static AccountResponse Account(string name, string type = AccountTypes.Checking, decimal balance = 0m,
        bool isDefault = false, bool active = true) =>
        new(Guid.NewGuid(), name, type, balance, new DateOnly(2026, 10, 1), type != AccountTypes.Savings, null, isDefault, active, balance);
}
