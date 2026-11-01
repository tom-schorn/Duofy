using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>
/// Schulden und Kredite (/api/v1/debts, Issue 31). Das Gezahlte und das Offene kommen fertig gerechnet in der Antwort; der
/// Client rechnet nichts selbst.
/// </summary>
public interface IDebtApi
{
    /// <summary>Die Einträge der Person; mit <paramref name="owner"/> die eines anderen Mitglieds, das den Bereich Sparen freigegeben hat.</summary>
    Task<ApiResult<IReadOnlyList<DebtResponse>>> ListAsync(Guid? owner = null, CancellationToken cancellationToken = default);

    /// <summary>Legt einen Eintrag an.</summary>
    Task<ApiResult<DebtResponse>> CreateAsync(CreateDebtRequest request, CancellationToken cancellationToken = default);

    /// <summary>Ändert einen Eintrag; nur die geschickten Felder ändern sich.</summary>
    Task<ApiResult<DebtResponse>> UpdateAsync(Guid id, UpdateDebtRequest request, CancellationToken cancellationToken = default);

    /// <summary>Löscht einen Eintrag ohne Zuordnungen; mit Zuordnungen antwortet die API 409 debt_has_bookings.</summary>
    Task<ApiResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
