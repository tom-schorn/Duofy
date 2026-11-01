using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>Der Client für /api/v1/savings-goals, siehe <see cref="ISavingsGoalApi"/>.</summary>
internal sealed class SavingsGoalApi(ApiConnection api) : ISavingsGoalApi
{
    private const string Path = "api/v1/savings-goals";

    public async Task<ApiResult<IReadOnlyList<SavingsGoalResponse>>> ListAsync(Guid? owner = null, CancellationToken cancellationToken = default)
    {
        var result = await api.SendAsync<List<SavingsGoalResponse>>(
            HttpMethod.Get, OwnerQuery.With(Path, owner), body: null, anonymous: false, cancellationToken);
        return result.Succeeded
            ? ApiResult<IReadOnlyList<SavingsGoalResponse>>.Success(result.Value!)
            : ApiResult<IReadOnlyList<SavingsGoalResponse>>.Failure(result.ErrorCode!);
    }

    public Task<ApiResult<SavingsGoalResponse>> CreateAsync(CreateSavingsGoalRequest request, CancellationToken cancellationToken = default) =>
        api.SendAsync<SavingsGoalResponse>(HttpMethod.Post, Path, request, anonymous: false, cancellationToken);

    public Task<ApiResult<SavingsGoalResponse>> UpdateAsync(Guid id, UpdateSavingsGoalRequest request, CancellationToken cancellationToken = default) =>
        api.SendAsync<SavingsGoalResponse>(HttpMethod.Patch, $"{Path}/{id}", request, anonymous: false, cancellationToken);

    public Task<ApiResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.SendAsync(HttpMethod.Delete, $"{Path}/{id}", body: null, anonymous: false, cancellationToken);
}
