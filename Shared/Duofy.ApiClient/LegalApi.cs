using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>Der Client für /api/v1/legal, siehe <see cref="ILegalApi"/>.</summary>
internal sealed class LegalApi(ApiConnection api) : ILegalApi
{
    private const string Legal = "api/v1/legal";

    public Task<ApiResult<LegalListResponse>> ListAsync(CancellationToken cancellationToken = default) =>
        api.SendAsync<LegalListResponse>(HttpMethod.Get, Legal, body: null, anonymous: true, cancellationToken);

    // Der Name wird maskiert, damit er nie den Pfad ändern kann; die API kennt ohnehin nur drei Namen.
    public Task<ApiResult<LegalTextResponse>> GetAsync(string name, CancellationToken cancellationToken = default) =>
        api.SendAsync<LegalTextResponse>(
            HttpMethod.Get, $"{Legal}/{Uri.EscapeDataString(name)}", body: null, anonymous: true, cancellationToken);
}
