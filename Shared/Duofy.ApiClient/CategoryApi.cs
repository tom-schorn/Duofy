using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>Der Client für /api/v1/categories, siehe <see cref="ICategoryApi"/>.</summary>
internal sealed class CategoryApi(ApiConnection api) : ICategoryApi
{
    private const string Path = "api/v1/categories";

    public async Task<ApiResult<IReadOnlyList<CategoryResponse>>> ListAsync(Guid? owner = null, CancellationToken cancellationToken = default)
    {
        var result = await api.SendAsync<List<CategoryResponse>>(HttpMethod.Get, OwnerQuery.With(Path, owner), body: null, anonymous: false, cancellationToken);
        return result.Succeeded
            ? ApiResult<IReadOnlyList<CategoryResponse>>.Success(result.Value!)
            : ApiResult<IReadOnlyList<CategoryResponse>>.Failure(result.ErrorCode!);
    }

    public Task<ApiResult<CategoryResponse>> CreateAsync(SaveCategoryRequest request, Guid? owner = null, CancellationToken cancellationToken = default) =>
        api.SendAsync<CategoryResponse>(HttpMethod.Post, OwnerQuery.With(Path, owner), request, anonymous: false, cancellationToken);

    public Task<ApiResult<CategoryResponse>> UpdateAsync(Guid id, SaveCategoryRequest request, Guid? owner = null, CancellationToken cancellationToken = default) =>
        api.SendAsync<CategoryResponse>(HttpMethod.Put, OwnerQuery.With($"{Path}/{id}", owner), request, anonymous: false, cancellationToken);

    public Task<ApiResult> DeleteAsync(Guid id, Guid? owner = null, CancellationToken cancellationToken = default) =>
        api.SendAsync(HttpMethod.Delete, OwnerQuery.With($"{Path}/{id}", owner), body: null, anonymous: false, cancellationToken);
}
