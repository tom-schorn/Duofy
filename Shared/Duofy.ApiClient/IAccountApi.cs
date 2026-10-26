using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>
/// Konten über die Duofy-API (/api/v1/accounts). Ohne Anmeldung ist der Fehlercode
/// <see cref="ClientErrorCodes.NotSignedIn"/>; eine unbekannte oder fremde Kennung ergibt
/// <see cref="ClientErrorCodes.NotFound"/>. Löschen gibt es nicht, nur Auflösen.
/// </summary>
public interface IAccountApi
{
    /// <summary>
    /// Liefert die Konten der Person mit ihrem heutigen Stand, das Standardkonto zuerst.
    /// Mit <paramref name="activeOnly"/> nur die offenen, wie eine Auswahlliste sie braucht. Mit
    /// <paramref name="owner"/> die Konten dieses Mitglieds, wenn es sie freigegeben hat (Spec 68).
    /// </summary>
    Task<ApiResult<IReadOnlyList<AccountResponse>>> ListAsync(bool activeOnly = false, Guid? owner = null, CancellationToken cancellationToken = default);

    /// <summary>Legt ein Konto an. Verletzt es eine Regel, ist der Fehlercode der Code der API, zum Beispiel <c>iban_invalid</c>.</summary>
    Task<ApiResult<AccountResponse>> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default);

    /// <summary>Ändert die Felder eines Kontos, die in der Anfrage stehen; mit <c>Active = false</c> wird es aufgelöst.</summary>
    Task<ApiResult<AccountResponse>> UpdateAsync(Guid id, UpdateAccountRequest request, CancellationToken cancellationToken = default);
}
