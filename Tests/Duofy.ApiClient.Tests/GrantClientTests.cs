using System.Net;
using Duofy.Schemas;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.ApiClient.Tests;

/// <summary>Die Freigaben im Client (Spec 68): der Besitzer in jeder Abfrage, Freigaben setzen, 403 mit Code.</summary>
public class GrantClientTests
{
    private static readonly Guid Anna = Guid.NewGuid();

    private static readonly PlanResponse November = new(
        Guid.NewGuid(), 2026, 11, [], 0m, 0m, 0m, 0m, [], [], new PlanCarryOverResponse(0m, false, null, []));

    private static (FakeApi Api, ServiceProvider Provider, IServiceScope Scope) Start(Func<SeenRequest, HttpResponseMessage> respond)
    {
        var api = new FakeApi { Respond = respond };
        var provider = TestHost.Create(api, new InMemoryTokenStore { Tokens = new AuthTokens("access", "refresh") });
        return (api, provider, provider.CreateScope());
    }

    [Fact]
    public async Task Every_list_names_the_owner_only_when_one_is_given()
    {
        var (api, provider, scope) = Start(request => request.Path.StartsWith("/api/v1/bookings")
            ? FakeApi.Json(HttpStatusCode.OK, new BookingMonthResponse([], 0m, 0m))
            : FakeApi.Json(HttpStatusCode.OK, Array.Empty<object>()));
        using var _p = provider;
        using var _s = scope;
        var services = scope.ServiceProvider;

        await services.GetRequiredService<IAccountApi>().ListAsync(activeOnly: true, owner: Anna);
        await services.GetRequiredService<IAccountApi>().ListAsync(owner: Anna);
        await services.GetRequiredService<IBookingApi>().ListMonthAsync(2026, 11, accountId: null, owner: Anna);
        await services.GetRequiredService<ICommitmentApi>().ListAsync(includeEnded: true, owner: Anna);
        await services.GetRequiredService<ICategoryApi>().ListAsync(owner: Anna);
        await services.GetRequiredService<IAccountApi>().ListAsync();

        Assert.Equal(
        [
            $"?active=true&owner={Anna}",
            $"?owner={Anna}",
            $"?year=2026&month=11&owner={Anna}",
            $"?includeEnded=true&owner={Anna}",
            $"?owner={Anna}",
            "",
        ], api.Requests.Select(r => r.Query));
    }

    [Fact]
    public async Task Every_plan_address_carries_the_owner()
    {
        var position = new PlanPositionResponse(Guid.NewGuid(), null, "Geschenk", 30m, 15, CommitmentTypes.Contract, Guid.NewGuid(),
            PotKeys.Needs, null, null, false, PaymentMethods.StandingOrder, 0m, null, false, null);
        var (api, provider, scope) = Start(request => request.Method == HttpMethod.Delete && !request.Path.EndsWith("/tick")
            ? new HttpResponseMessage(HttpStatusCode.NoContent)
            : request.Path.Contains("/positions")
                ? FakeApi.Json(HttpStatusCode.OK, position)
                : FakeApi.Json(HttpStatusCode.OK, November));
        using var _p = provider;
        using var _s = scope;
        var plans = scope.ServiceProvider.GetRequiredService<IPlanApi>();

        await plans.GetAsync(2026, 11, Anna);
        await plans.CreatePositionAsync(2026, 11, new CreatePositionRequest("Geschenk", 30m, 15, Guid.NewGuid(), PotKeys.Needs), Anna);
        await plans.UpdatePositionAsync(2026, 11, position.Id, new UpdatePositionRequest(Label: "X"), Anna);
        await plans.DeletePositionAsync(2026, 11, position.Id, Anna);
        await plans.TickAsync(2026, 11, position.Id, new TickPositionRequest(), Anna);
        await plans.UntickAsync(2026, 11, position.Id, Anna);
        await plans.ApplyChangesAsync(2026, 11, new ApplyPlanChangesRequest(), Anna);
        await plans.CreateCarryOverAsync(2026, 11, new CreateCarryOverRequest(10m), Anna);
        await plans.DeleteCarryOverAsync(2026, 11, Anna);
        await plans.GetAsync(2026, 11);

        Assert.All(api.Requests.Take(9), request => Assert.Equal($"?owner={Anna}", request.Query));
        Assert.Equal("", api.Requests[9].Query);
    }

    [Fact]
    public async Task Setting_grants_puts_the_levels_for_the_member()
    {
        var household = Guid.NewGuid();
        var ben = new MemberResponse(Guid.NewGuid(), "Ben", "Beispiel", "ben@example.org", HouseholdRoles.Member, false,
            GrantLevels.None, GrantLevels.None with { Plan = AccessLevelKeys.View });
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.OK, ben));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IHouseholdApi>()
            .SetGrantsAsync(household, ben.UserId, new UpdateGrantsRequest(Plan: AccessLevelKeys.View));

        Assert.Equal(AccessLevelKeys.View, result.Value!.MyGrants.Plan);
        var request = Assert.Single(api.Requests);
        Assert.Equal((HttpMethod.Put, $"/api/v1/households/{household}/grants/{ben.UserId}"), (request.Method, request.Path));
        Assert.Contains("\"plan\":\"view\"", request.Body);
    }

    [Theory]
    [InlineData(ErrorCodes.NoCreateGranted)]
    [InlineData(ErrorCodes.NoEditGranted)]
    [InlineData(ErrorCodes.NoDeleteGranted)]
    public async Task A_refused_step_gives_its_code(string code)
    {
        var (_, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.Forbidden, new ErrorResponse(code)));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IBookingApi>().DeleteAsync(Guid.NewGuid());

        Assert.Equal(code, result.ErrorCode);
    }
}
