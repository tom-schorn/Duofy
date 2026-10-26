using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>
/// Die Rechtstexte des Servers über die Duofy-API (/api/v1/legal), Spec 70. Beides geht ohne Anmeldung und
/// schickt kein Token mit; ein Text, den es auf diesem Server nicht gibt, ergibt
/// <see cref="ClientErrorCodes.NotFound"/>.
/// </summary>
public interface ILegalApi
{
    /// <summary>Die Namen der Texte, die dieser Server anbietet (<see cref="LegalDocumentNames"/>).</summary>
    Task<ApiResult<LegalListResponse>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Der Text eines Rechtstexts, reiner Text mit Zeilenumbrüchen.</summary>
    Task<ApiResult<LegalTextResponse>> GetAsync(string name, CancellationToken cancellationToken = default);
}
