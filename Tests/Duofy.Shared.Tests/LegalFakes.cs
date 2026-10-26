using Duofy.ApiClient;
using Duofy.Schemas;

namespace Duofy.Shared.Tests;

/// <summary>Spielt die Rechtstexte des Servers: Der Test legt fest, welche Texte es gibt.</summary>
public sealed class FakeLegalApi : ILegalApi
{
    /// <summary>Die Texte, die der gespielte Server anbietet, nach Namen (imprint, privacy, terms).</summary>
    public Dictionary<string, string> Documents { get; } = [];

    /// <summary>Wenn gesetzt, scheitern beide Aufrufe mit diesem Fehlercode.</summary>
    public string? Failure { get; set; }

    public List<string> Read { get; } = [];

    public Task<ApiResult<LegalListResponse>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Failure is not null
            ? ApiResult<LegalListResponse>.Failure(Failure)
            : ApiResult<LegalListResponse>.Success(new LegalListResponse(
                LegalDocumentNames.All.Where(Documents.ContainsKey).ToList())));

    public Task<ApiResult<LegalTextResponse>> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        Read.Add(name);
        if (Failure is not null)
        {
            return Task.FromResult(ApiResult<LegalTextResponse>.Failure(Failure));
        }
        return Task.FromResult(Documents.TryGetValue(name, out var text)
            ? ApiResult<LegalTextResponse>.Success(new LegalTextResponse(text))
            : ApiResult<LegalTextResponse>.Failure(ClientErrorCodes.NotFound));
    }
}
