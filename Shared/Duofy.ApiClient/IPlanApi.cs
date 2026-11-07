using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>
/// Monatspläne über die Duofy-API (/api/v1/plans/{year}/{month}). Ohne Anmeldung ist der Fehlercode
/// <see cref="ClientErrorCodes.NotSignedIn"/>; ein unbekannter oder fremder Posten ergibt
/// <see cref="ClientErrorCodes.NotFound"/>. Bei 400 und 409 ist der Fehlercode der Code der API,
/// zum Beispiel <c>position_has_bookings</c>.
///
/// Jede Methode nimmt einen Besitzer (Spec 68): ohne ihn der eigene Plan, mit ihm der Plan dieses Mitglieds,
/// so weit es ihn freigegeben hat; ein fehlender Schritt kommt als Fehlercode wie <c>no_edit_granted</c>.
/// </summary>
public interface IPlanApi
{
    /// <summary>Öffnet den Monat; beim ersten Mal legt die API ihn aus den Verträgen an.</summary>
    Task<ApiResult<PlanResponse>> GetAsync(int year, int month, Guid? owner = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Die Monate mit einem Plan und deren Frei-Betrag, der neueste zuerst; legt nichts an (Spec 23).
    /// </summary>
    Task<ApiResult<IReadOnlyList<PlanMonthResponse>>> ListMonthsAsync(Guid? owner = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Die Monate eines Jahres mit Verplanbar, Anstehend und den Töpfen in einem Aufruf, dazu die vorhandenen künftigen
    /// Monate, der neueste zuerst (Issue #94); legt nichts an. Ein Jahr außerhalb von <see cref="PlanRules"/> ergibt einen Fehler.
    /// </summary>
    Task<ApiResult<IReadOnlyList<PlanOverviewMonthResponse>>> GetOverviewAsync(int year, Guid? owner = null, CancellationToken cancellationToken = default);

    /// <summary>Legt einen einmaligen Posten an.</summary>
    Task<ApiResult<PlanPositionResponse>> CreatePositionAsync(
        int year, int month, CreatePositionRequest request, Guid? owner = null, CancellationToken cancellationToken = default);

    /// <summary>Ändert einen Posten; ein Posten aus einem Vertrag gilt danach als von Hand geändert.</summary>
    Task<ApiResult<PlanPositionResponse>> UpdatePositionAsync(
        int year, int month, Guid id, UpdatePositionRequest request, Guid? owner = null, CancellationToken cancellationToken = default);

    /// <summary>Löscht einen Posten ohne Buchungen; der Vertrag läuft weiter.</summary>
    Task<ApiResult> DeletePositionAsync(int year, int month, Guid id, Guid? owner = null, CancellationToken cancellationToken = default);

    /// <summary>Hakt einen Posten ab, mit Buchung oder Ausgleich je nach Anfrage.</summary>
    Task<ApiResult<PlanPositionResponse>> TickAsync(
        int year, int month, Guid id, TickPositionRequest request, Guid? owner = null, CancellationToken cancellationToken = default);

    /// <summary>Nimmt den Haken zurück und löscht eine Buchung, die beim Abhaken entstanden ist.</summary>
    Task<ApiResult<PlanPositionResponse>> UntickAsync(int year, int month, Guid id, Guid? owner = null, CancellationToken cancellationToken = default);

    /// <summary>Übernimmt Unterschiede zu den Verträgen: ohne Liste alle ungeschützten, sonst die genannten.</summary>
    Task<ApiResult<PlanResponse>> ApplyChangesAsync(
        int year, int month, ApplyPlanChangesRequest request, Guid? owner = null, CancellationToken cancellationToken = default);

    /// <summary>Bucht den Übertrag in diesen Monat und gleicht die genannten Konten aus.</summary>
    Task<ApiResult<PlanResponse>> CreateCarryOverAsync(
        int year, int month, CreateCarryOverRequest request, Guid? owner = null, CancellationToken cancellationToken = default);

    /// <summary>Entfernt den Übertrag dieses Monats (beide Buchungen des Paars).</summary>
    Task<ApiResult> DeleteCarryOverAsync(int year, int month, Guid? owner = null, CancellationToken cancellationToken = default);
}
