using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>Der Client für /api/v1/commitments, siehe <see cref="ICommitmentApi"/>.</summary>
internal sealed class CommitmentApi(ApiConnection api) : ICommitmentApi
{
    private const string Path = "api/v1/commitments";

    public async Task<ApiResult<IReadOnlyList<CommitmentResponse>>> ListAsync(
        bool includeEnded = false, Guid? owner = null, CancellationToken cancellationToken = default)
    {
        var result = await api.SendAsync<List<CommitmentResponse>>(
            HttpMethod.Get, OwnerQuery.With(includeEnded ? Path + "?includeEnded=true" : Path, owner), body: null, anonymous: false, cancellationToken);
        return result.Succeeded
            ? ApiResult<IReadOnlyList<CommitmentResponse>>.Success(result.Value!)
            : ApiResult<IReadOnlyList<CommitmentResponse>>.Failure(result.ErrorCode!);
    }

    public Task<ApiResult<CommitmentResponse>> CreateAsync(CreateCommitmentRequest request, CancellationToken cancellationToken = default) =>
        api.SendAsync<CommitmentResponse>(HttpMethod.Post, Path, request, anonymous: false, cancellationToken);

    public Task<ApiResult<CommitmentResponse>> UpdateAsync(Guid id, UpdateCommitmentRequest request, CancellationToken cancellationToken = default) =>
        api.SendAsync<CommitmentResponse>(HttpMethod.Patch, $"{Path}/{id}", request, anonymous: false, cancellationToken);

    public Task<ApiResult<CommitmentResponse>> EndAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.SendAsync<CommitmentResponse>(HttpMethod.Post, $"{Path}/{id}/end", body: null, anonymous: false, cancellationToken);
}
