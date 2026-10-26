using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>
/// Was der Server anbietet (/api/v1/instance, Spec 70): Anmeldewege, Registrierungsmodus, ob der Einrichtungscode
/// gerade gilt, und die Rechtstexte. Geht ohne Anmeldung und schickt kein Token mit.
/// </summary>
public interface IInstanceApi
{
    /// <summary>Die Auskunft des gewählten Servers.</summary>
    Task<ApiResult<InstanceResponse>> GetAsync(CancellationToken cancellationToken = default);
}
