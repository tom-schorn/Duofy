using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>Der Client für /api/v1/accounts, siehe <see cref="IAccountApi"/>.</summary>
internal sealed class AccountApi(ApiConnection api) : IAccountApi
{
    private const string Path = "api/v1/accounts";

    public async Task<ApiResult<IReadOnlyList<AccountResponse>>> ListAsync(
        bool activeOnly = false, Guid? owner = null, CancellationToken cancellationToken = default)
    {
        var result = await api.SendAsync<List<AccountResponse>>(
            HttpMethod.Get, OwnerQuery.With(activeOnly ? Path + "?active=true" : Path, owner), body: null, anonymous: false, cancellationToken);
        return result.Succeeded
            ? ApiResult<IReadOnlyList<AccountResponse>>.Success(result.Value!)
            : ApiResult<IReadOnlyList<AccountResponse>>.Failure(result.ErrorCode!);
    }

    public Task<ApiResult<AccountResponse>> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default) =>
        api.SendAsync<AccountResponse>(HttpMethod.Post, Path, request, anonymous: false, cancellationToken);

    public Task<ApiResult<AccountResponse>> UpdateAsync(Guid id, UpdateAccountRequest request, CancellationToken cancellationToken = default) =>
        api.SendAsync<AccountResponse>(HttpMethod.Patch, $"{Path}/{id}", request, anonymous: false, cancellationToken);
}
