using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>
/// Die Sparziele (/api/v1/savings-goals, Issue 30). Der Stand je Ziel kommt fertig gerechnet in der Antwort; der Client
/// rechnet nichts selbst.
/// </summary>
public interface ISavingsGoalApi
{
    /// <summary>Die Ziele der Person, nach Namen sortiert; mit <paramref name="owner"/> die eines anderen Mitglieds, das den Bereich Sparen freigegeben hat.</summary>
    Task<ApiResult<IReadOnlyList<SavingsGoalResponse>>> ListAsync(Guid? owner = null, CancellationToken cancellationToken = default);

    /// <summary>Legt ein Ziel an.</summary>
    Task<ApiResult<SavingsGoalResponse>> CreateAsync(CreateSavingsGoalRequest request, CancellationToken cancellationToken = default);

    /// <summary>Ändert ein Ziel; nur die geschickten Felder ändern sich.</summary>
    Task<ApiResult<SavingsGoalResponse>> UpdateAsync(Guid id, UpdateSavingsGoalRequest request, CancellationToken cancellationToken = default);

    /// <summary>Löscht ein Ziel ohne Zuordnungen; mit Zuordnungen antwortet die API 409 savings_goal_has_bookings.</summary>
    Task<ApiResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
