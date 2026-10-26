using Duofy.ApiClient;
using Duofy.Schemas;

namespace Duofy.Shared.Tests;

/// <summary>Der Haushaltsplan (Spec 69) in der Haushalts-API im Speicher: je Monat ein Plan, den der Test vorgibt.</summary>
public sealed partial class FakeHouseholdApi
{
    public Dictionary<(int Year, int Month), HouseholdPlanResponse> HouseholdPlans { get; } = [];

    public List<(Guid HouseholdId, int Year, int Month)> PlanCalls { get; } = [];

    public List<HouseholdPlanMonth> PlanMonths { get; } = [];

    public static HouseholdPlanResponse EmptyPlan(Guid householdId, string name, int year, int month) => new(
        householdId, name, year, month, [], 0m,
        [new HouseholdPotResponse(PotKeys.Needs, 0m, 0m), new HouseholdPotResponse(PotKeys.Wants, 0m, 0m), new HouseholdPotResponse(PotKeys.Savings, 0m, 0m)],
        0m,
        [new PlanQuotaResponse(PotKeys.Needs, 50, 0m), new PlanQuotaResponse(PotKeys.Wants, 30, 0m), new PlanQuotaResponse(PotKeys.Savings, 20, 0m)],
        new HouseholdUnplannedResponse(0m, 0m, 0m, 0m), [], []);

    public static HouseholdPlanPositionResponse OwnedPosition(
        string label, decimal planned, Guid ownerId, string ownerName, string pot = PotKeys.Needs, decimal booked = 0m, bool done = false) =>
        new(Guid.NewGuid(), label, planned, 1, pot == PotKeys.Income ? CommitmentTypes.Income : CommitmentTypes.Contract,
            FakeCategoryApi.Groceries.Id, pot, false, booked, done ? DateTimeOffset.UnixEpoch : null, ownerId, ownerName);

    public Task<ApiResult<HouseholdPlanResponse>> GetPlanAsync(Guid householdId, int year, int month, CancellationToken cancellationToken = default)
    {
        PlanCalls.Add((householdId, year, month));
        if (Error is not null)
        {
            return Task.FromResult(ApiResult<HouseholdPlanResponse>.Failure(Error));
        }
        var plan = HouseholdPlans.TryGetValue((year, month), out var seeded)
            ? seeded
            : EmptyPlan(householdId, Household?.Name ?? "", year, month);
        return Task.FromResult(ApiResult<HouseholdPlanResponse>.Success(plan));
    }

    public Task<ApiResult<IReadOnlyList<HouseholdPlanMonth>>> ListPlansAsync(Guid householdId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult<IReadOnlyList<HouseholdPlanMonth>>.Success(PlanMonths.ToList()));
}
