using Duofy.ApiClient;
using Duofy.Schemas;

namespace Duofy.Shared.Tests;

/// <summary>Eine Schulden-API im Speicher: Sie merkt sich, was die Seite aufgerufen hat; Gezahltes und Offenes geben die Tests vor.</summary>
public sealed class FakeDebtApi : IDebtApi
{
    public List<DebtResponse> Debts { get; } = [];
    public List<CreateDebtRequest> Created { get; } = [];
    public List<(Guid Id, UpdateDebtRequest Request)> Updated { get; } = [];
    public List<Guid> Deleted { get; } = [];
    public List<Guid?> ListOwners { get; } = [];
    public string? Error { get; set; }

    public Task<ApiResult<IReadOnlyList<DebtResponse>>> ListAsync(Guid? owner = null, CancellationToken cancellationToken = default)
    {
        ListOwners.Add(owner);
        return Task.FromResult(ApiResult<IReadOnlyList<DebtResponse>>.Success(Debts.ToList()));
    }

    public Task<ApiResult<DebtResponse>> CreateAsync(CreateDebtRequest request, CancellationToken cancellationToken = default)
    {
        Created.Add(request);
        if (Error is not null)
        {
            return Task.FromResult(ApiResult<DebtResponse>.Failure(Error));
        }
        var saved = new DebtResponse(Guid.NewGuid(), request.Kind!, request.Name!.Trim(), request.Creditor!.Trim(), request.OriginalAmount,
            request.DueDate, request.Note, 0m, request.OriginalAmount, false);
        Debts.Add(saved);
        return Task.FromResult(ApiResult<DebtResponse>.Success(saved));
    }

    public Task<ApiResult<DebtResponse>> UpdateAsync(Guid id, UpdateDebtRequest request, CancellationToken cancellationToken = default)
    {
        Updated.Add((id, request));
        if (Error is not null)
        {
            return Task.FromResult(ApiResult<DebtResponse>.Failure(Error));
        }
        var index = Debts.FindIndex(d => d.Id == id);
        Debts[index] = Debts[index] with { Name = request.Name ?? Debts[index].Name };
        return Task.FromResult(ApiResult<DebtResponse>.Success(Debts[index]));
    }

    public Task<ApiResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Deleted.Add(id);
        if (Error is not null)
        {
            return Task.FromResult(ApiResult.Failure(Error));
        }
        Debts.RemoveAll(d => d.Id == id);
        return Task.FromResult(ApiResult.Success());
    }
}
