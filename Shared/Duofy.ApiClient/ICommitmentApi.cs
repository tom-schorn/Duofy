using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>
/// Verträge über die Duofy-API (/api/v1/commitments). Ohne Anmeldung ist der Fehlercode
/// <see cref="ClientErrorCodes.NotSignedIn"/>; eine unbekannte oder fremde Kennung ergibt
/// <see cref="ClientErrorCodes.NotFound"/>. Löschen gibt es nicht, nur Beenden.
/// </summary>
public interface ICommitmentApi
{
    /// <summary>
    /// Liefert die laufenden Verträge der Person, sortiert nach der nächsten Zahlung.
    /// Mit <paramref name="includeEnded"/> auch die beendeten; mit <paramref name="owner"/> die Verträge dieses
    /// Mitglieds, wenn es sie freigegeben hat (Spec 68).
    /// </summary>
    Task<ApiResult<IReadOnlyList<CommitmentResponse>>> ListAsync(bool includeEnded = false, Guid? owner = null, CancellationToken cancellationToken = default);

    /// <summary>Legt einen Vertrag an. Verletzt er eine Regel, ist der Fehlercode der Code der API, zum Beispiel <c>commitment_pot_invalid</c>.</summary>
    Task<ApiResult<CommitmentResponse>> CreateAsync(CreateCommitmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Ändert die Felder eines Vertrags, die in der Anfrage stehen.</summary>
    Task<ApiResult<CommitmentResponse>> UpdateAsync(Guid id, UpdateCommitmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Beendet einen Vertrag: Die letzte Zahlung wird die letzte bis heute.</summary>
    Task<ApiResult<CommitmentResponse>> EndAsync(Guid id, CancellationToken cancellationToken = default);
}
