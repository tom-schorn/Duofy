using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>Der Client für /api/v1/plans, siehe <see cref="IPlanApi"/>.</summary>
internal sealed class PlanApi(ApiConnection api) : IPlanApi
{
    private static string Month(int year, int month) => $"api/v1/plans/{year}/{month}";

    private static string Position(int year, int month, Guid id) => $"{Month(year, month)}/positions/{id}";

    public Task<ApiResult<PlanResponse>> GetAsync(int year, int month, Guid? owner = null, CancellationToken cancellationToken = default) =>
        api.SendAsync<PlanResponse>(HttpMethod.Get, OwnerQuery.With(Month(year, month), owner), body: null, anonymous: false, cancellationToken);

    public async Task<ApiResult<IReadOnlyList<PlanMonthResponse>>> ListMonthsAsync(Guid? owner = null, CancellationToken cancellationToken = default)
    {
        var result = await api.SendAsync<List<PlanMonthResponse>>(
            HttpMethod.Get, OwnerQuery.With("api/v1/plans", owner), body: null, anonymous: false, cancellationToken);
        return result.Succeeded
            ? ApiResult<IReadOnlyList<PlanMonthResponse>>.Success(result.Value!)
            : ApiResult<IReadOnlyList<PlanMonthResponse>>.Failure(result.ErrorCode!);
    }

    public async Task<ApiResult<IReadOnlyList<PlanOverviewMonthResponse>>> GetOverviewAsync(
        int year, Guid? owner = null, CancellationToken cancellationToken = default)
    {
        var result = await api.SendAsync<List<PlanOverviewMonthResponse>>(
            HttpMethod.Get, OwnerQuery.With($"api/v1/plans/overview?year={year}", owner), body: null, anonymous: false, cancellationToken);
        return result.Succeeded
            ? ApiResult<IReadOnlyList<PlanOverviewMonthResponse>>.Success(result.Value!)
            : ApiResult<IReadOnlyList<PlanOverviewMonthResponse>>.Failure(result.ErrorCode!);
    }

    public Task<ApiResult<PlanPositionResponse>> CreatePositionAsync(
        int year, int month, CreatePositionRequest request, Guid? owner = null, CancellationToken cancellationToken = default) =>
        api.SendAsync<PlanPositionResponse>(
            HttpMethod.Post, OwnerQuery.With($"{Month(year, month)}/positions", owner), request, anonymous: false, cancellationToken);

    public Task<ApiResult<PlanPositionResponse>> UpdatePositionAsync(
        int year, int month, Guid id, UpdatePositionRequest request, Guid? owner = null, CancellationToken cancellationToken = default) =>
        api.SendAsync<PlanPositionResponse>(
            HttpMethod.Patch, OwnerQuery.With(Position(year, month, id), owner), request, anonymous: false, cancellationToken);

    public Task<ApiResult> DeletePositionAsync(int year, int month, Guid id, Guid? owner = null, CancellationToken cancellationToken = default) =>
        api.SendAsync(HttpMethod.Delete, OwnerQuery.With(Position(year, month, id), owner), body: null, anonymous: false, cancellationToken);

    public Task<ApiResult<PlanPositionResponse>> TickAsync(
        int year, int month, Guid id, TickPositionRequest request, Guid? owner = null, CancellationToken cancellationToken = default) =>
        api.SendAsync<PlanPositionResponse>(
            HttpMethod.Post, OwnerQuery.With($"{Position(year, month, id)}/tick", owner), request, anonymous: false, cancellationToken);

    public Task<ApiResult<PlanPositionResponse>> UntickAsync(
        int year, int month, Guid id, Guid? owner = null, CancellationToken cancellationToken = default) =>
        api.SendAsync<PlanPositionResponse>(
            HttpMethod.Delete, OwnerQuery.With($"{Position(year, month, id)}/tick", owner), body: null, anonymous: false, cancellationToken);

    public Task<ApiResult<PlanResponse>> ApplyChangesAsync(
        int year, int month, ApplyPlanChangesRequest request, Guid? owner = null, CancellationToken cancellationToken = default) =>
        api.SendAsync<PlanResponse>(
            HttpMethod.Post, OwnerQuery.With($"{Month(year, month)}/changes/apply", owner), request, anonymous: false, cancellationToken);

    public Task<ApiResult<PlanResponse>> CreateCarryOverAsync(
        int year, int month, CreateCarryOverRequest request, Guid? owner = null, CancellationToken cancellationToken = default) =>
        api.SendAsync<PlanResponse>(
            HttpMethod.Post, OwnerQuery.With($"{Month(year, month)}/carry-over", owner), request, anonymous: false, cancellationToken);

    public Task<ApiResult> DeleteCarryOverAsync(int year, int month, Guid? owner = null, CancellationToken cancellationToken = default) =>
        api.SendAsync(HttpMethod.Delete, OwnerQuery.With($"{Month(year, month)}/carry-over", owner), body: null, anonymous: false, cancellationToken);
}
