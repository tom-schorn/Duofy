using System.Net;
using Duofy.Schemas;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.ApiClient.Tests;

/// <summary>Der Client für den Haushaltsplan (Spec 69): richtige Adressen, Inhalte und Fehlercodes.</summary>
public class HouseholdPlanClientTests
{
    private static readonly Guid HouseholdId = Guid.NewGuid();

    private static readonly HouseholdPlanResponse Plan = new(
        HouseholdId, "Haushalt von Anna", 2026, 11,
        [new HouseholdPlanPositionResponse(Guid.NewGuid(), "Miete", 890.00m, 1, CommitmentTypes.Contract, Guid.NewGuid(), PotKeys.Needs, false, 0m, null, Guid.NewGuid(), "Anna")],
        3000.00m,
        [new HouseholdPotResponse(PotKeys.Needs, 890.00m, 0m)],
        890.00m,
        [new PlanQuotaResponse(PotKeys.Needs, 50, 0m)],
        new HouseholdUnplannedResponse(0m, 0m, 40.00m, 0m),
        [],
        ["Clara"]);

    private static (FakeApi Api, ServiceProvider Provider, IServiceScope Scope) Start(Func<SeenRequest, HttpResponseMessage> respond)
    {
        var api = new FakeApi { Respond = respond };
        var provider = TestHost.Create(api, new InMemoryTokenStore { Tokens = new AuthTokens("access", "refresh") });
        return (api, provider, provider.CreateScope());
    }

    [Fact]
    public async Task The_plan_of_a_month_is_read_from_the_household_path()
    {
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.OK, Plan));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IHouseholdApi>().GetPlanAsync(HouseholdId, 2026, 11);

        Assert.Equal("Miete", Assert.Single(result.Value!.Positions).Label);
        Assert.Equal(["Clara"], result.Value.AccountsMissing);
        var request = Assert.Single(api.Requests);
        Assert.Equal((HttpMethod.Get, $"/api/v1/households/{HouseholdId}/plans/2026/11", "Bearer access"), (request.Method, request.Path, request.Authorization));
    }

    [Fact]
    public async Task The_months_are_read_from_the_plans_path()
    {
        var months = new[] { new HouseholdPlanMonth(2026, 11), new HouseholdPlanMonth(2026, 10) };
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.OK, months));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IHouseholdApi>().ListPlansAsync(HouseholdId);

        Assert.Equal(months, result.Value);
        Assert.Equal($"/api/v1/households/{HouseholdId}/plans", Assert.Single(api.Requests).Path);
    }

    [Fact]
    public async Task A_stranger_gets_not_found()
    {
        var (_, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.NotFound, new ErrorResponse(ErrorCodes.HouseholdNotFound)));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IHouseholdApi>().GetPlanAsync(HouseholdId, 2026, 11);

        Assert.Equal(ClientErrorCodes.NotFound, result.ErrorCode);
    }
}
