using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>Der Client für /api/v1/debts, siehe <see cref="IDebtApi"/>.</summary>
internal sealed class DebtApi(ApiConnection api) : IDebtApi
{
    private const string Path = "api/v1/debts";

    public async Task<ApiResult<IReadOnlyList<DebtResponse>>> ListAsync(Guid? owner = null, CancellationToken cancellationToken = default)
    {
        var result = await api.SendAsync<List<DebtResponse>>(
            HttpMethod.Get, OwnerQuery.With(Path, owner), body: null, anonymous: false, cancellationToken);
        return result.Succeeded
            ? ApiResult<IReadOnlyList<DebtResponse>>.Success(result.Value!)
            : ApiResult<IReadOnlyList<DebtResponse>>.Failure(result.ErrorCode!);
    }

    public Task<ApiResult<DebtResponse>> CreateAsync(CreateDebtRequest request, CancellationToken cancellationToken = default) =>
        api.SendAsync<DebtResponse>(HttpMethod.Post, Path, request, anonymous: false, cancellationToken);

    public Task<ApiResult<DebtResponse>> UpdateAsync(Guid id, UpdateDebtRequest request, CancellationToken cancellationToken = default) =>
        api.SendAsync<DebtResponse>(HttpMethod.Patch, $"{Path}/{id}", request, anonymous: false, cancellationToken);

    public Task<ApiResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.SendAsync(HttpMethod.Delete, $"{Path}/{id}", body: null, anonymous: false, cancellationToken);
}
