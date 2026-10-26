using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>Der Client für /api/v1/instance, siehe <see cref="IInstanceApi"/>.</summary>
internal sealed class InstanceApi(ApiConnection api) : IInstanceApi
{
    public Task<ApiResult<InstanceResponse>> GetAsync(CancellationToken cancellationToken = default) =>
        api.SendAsync<InstanceResponse>(HttpMethod.Get, "api/v1/instance", body: null, anonymous: true, cancellationToken);
}
