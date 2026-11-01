using Duofy.ApiClient;
using Duofy.Schemas;

namespace Duofy.Shared.Tests;

/// <summary>Eine Sparziel-API im Speicher: Sie merkt sich, was die Seite aufgerufen hat, und rechnet den Stand nicht selbst (den geben die Tests vor).</summary>
public sealed class FakeSavingsGoalApi : ISavingsGoalApi
{
    public List<SavingsGoalResponse> Goals { get; } = [];
    public List<CreateSavingsGoalRequest> Created { get; } = [];
    public List<(Guid Id, UpdateSavingsGoalRequest Request)> Updated { get; } = [];
    public List<Guid> Deleted { get; } = [];
    public List<Guid?> ListOwners { get; } = [];
    public string? Error { get; set; }

    public Task<ApiResult<IReadOnlyList<SavingsGoalResponse>>> ListAsync(Guid? owner = null, CancellationToken cancellationToken = default)
    {
        ListOwners.Add(owner);
        return Task.FromResult(ApiResult<IReadOnlyList<SavingsGoalResponse>>.Success(Goals.ToList()));
    }

    public Task<ApiResult<SavingsGoalResponse>> CreateAsync(CreateSavingsGoalRequest request, CancellationToken cancellationToken = default)
    {
        Created.Add(request);
        if (Error is not null)
        {
            return Task.FromResult(ApiResult<SavingsGoalResponse>.Failure(Error));
        }
        var saved = new SavingsGoalResponse(Guid.NewGuid(), request.Name!.Trim(), request.TargetAmount, request.TargetDate, request.AccountId, request.StartAmount ?? 0m, false, request.StartAmount ?? 0m);
        Goals.Add(saved);
        return Task.FromResult(ApiResult<SavingsGoalResponse>.Success(saved));
    }

    public Task<ApiResult<SavingsGoalResponse>> UpdateAsync(Guid id, UpdateSavingsGoalRequest request, CancellationToken cancellationToken = default)
    {
        Updated.Add((id, request));
        if (Error is not null)
        {
            return Task.FromResult(ApiResult<SavingsGoalResponse>.Failure(Error));
        }
        var index = Goals.FindIndex(g => g.Id == id);
        Goals[index] = Goals[index] with { Name = request.Name ?? Goals[index].Name };
        return Task.FromResult(ApiResult<SavingsGoalResponse>.Success(Goals[index]));
    }

    public Task<ApiResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Deleted.Add(id);
        if (Error is not null)
        {
            return Task.FromResult(ApiResult.Failure(Error));
        }
        Goals.RemoveAll(g => g.Id == id);
        return Task.FromResult(ApiResult.Success());
    }
}
