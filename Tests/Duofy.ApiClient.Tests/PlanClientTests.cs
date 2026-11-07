using System.Net;
using Duofy.Schemas;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.ApiClient.Tests;

/// <summary>Der Client für Monatspläne und Quoten: richtige Adressen, Inhalte und Fehlercodes.</summary>
public class PlanClientTests
{
    private static readonly PlanPositionResponse Rent = new(
        Guid.NewGuid(), Guid.NewGuid(), "Miete", 890m, 1, CommitmentTypes.Contract, Guid.NewGuid(), PotKeys.Needs,
        null, null, false, PaymentMethods.StandingOrder, 0m, null, false, null);

    private static readonly PlanResponse November = new(
        Guid.NewGuid(), 2026, 11, [Rent], 0m, 890m, 0m, 0m,
        [new PlanQuotaResponse(PotKeys.Needs, 50, 0m)], [], new PlanCarryOverResponse(230m, false, null, []));

    private static (FakeApi Api, ServiceProvider Provider, IServiceScope Scope) Start(Func<SeenRequest, HttpResponseMessage> respond)
    {
        var api = new FakeApi { Respond = respond };
        var provider = TestHost.Create(api, new InMemoryTokenStore { Tokens = new AuthTokens("access", "refresh") });
        return (api, provider, provider.CreateScope());
    }

    [Fact]
    public async Task Get_reads_the_month_by_year_and_month()
    {
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.OK, November));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IPlanApi>().GetAsync(2026, 11);

        Assert.Equal(Rent.Id, Assert.Single(result.Value!.Positions).Id);
        var request = Assert.Single(api.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v1/plans/2026/11", request.Path);
        Assert.Equal("Bearer access", request.Authorization);
    }

    [Fact]
    public async Task ListMonths_reads_the_plans_path_and_names_the_owner_only_when_one_is_given()
    {
        var months = new[] { new PlanMonthResponse(2026, 11, 120m), new PlanMonthResponse(2026, 10, -5m) };
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.OK, months));
        using var _p = provider;
        using var _s = scope;
        var owner = Guid.NewGuid();

        var own = await scope.ServiceProvider.GetRequiredService<IPlanApi>().ListMonthsAsync();
        await scope.ServiceProvider.GetRequiredService<IPlanApi>().ListMonthsAsync(owner);

        Assert.Equal(months, own.Value);
        Assert.Equal([("/api/v1/plans", ""), ("/api/v1/plans", $"?owner={owner}")], api.Requests.Select(r => (r.Path, r.Query)));
        Assert.All(api.Requests, r => Assert.Equal((HttpMethod.Get, "Bearer access"), (r.Method, r.Authorization)));
    }

    [Fact]
    public async Task GetOverview_reads_the_overview_path_with_the_year_and_names_the_owner_only_when_one_is_given()
    {
        var months = new[] { new PlanOverviewMonthResponse(2026, 11, 120m, true, 10m, [new PlanPotResponse(PotKeys.Needs, 100m, 40m)]) };
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.OK, months));
        using var _p = provider;
        using var _s = scope;
        var owner = Guid.NewGuid();

        var own = await scope.ServiceProvider.GetRequiredService<IPlanApi>().GetOverviewAsync(2026);
        await scope.ServiceProvider.GetRequiredService<IPlanApi>().GetOverviewAsync(2026, owner);

        var month = Assert.Single(own.Value!);
        Assert.Equal((2026, 11, 120m, true, 10m), (month.Year, month.Month, month.Free, month.CarriedIn, month.Unpaid));
        Assert.Equal(new PlanPotResponse(PotKeys.Needs, 100m, 40m), Assert.Single(month.Pots));
        Assert.Equal([("/api/v1/plans/overview", "?year=2026"), ("/api/v1/plans/overview", $"?year=2026&owner={owner}")],
            api.Requests.Select(r => (r.Path, r.Query)));
        Assert.All(api.Requests, r => Assert.Equal((HttpMethod.Get, "Bearer access"), (r.Method, r.Authorization)));
    }

    [Fact]
    public async Task Positions_ticks_changes_and_carry_over_go_to_their_addresses()
    {
        var (api, provider, scope) = Start(request => request.Method == HttpMethod.Delete && !request.Path.EndsWith("/tick")
            ? new HttpResponseMessage(HttpStatusCode.NoContent)
            : request.Path.EndsWith("/positions") || request.Path.EndsWith("/tick") || request.Path.Contains("/positions/")
                ? FakeApi.Json(HttpStatusCode.OK, Rent)
                : FakeApi.Json(HttpStatusCode.OK, November));
        using var _p = provider;
        using var _s = scope;
        var plans = scope.ServiceProvider.GetRequiredService<IPlanApi>();

        await plans.CreatePositionAsync(2026, 11, new CreatePositionRequest("Geschenk", 50m, 20, Guid.NewGuid(), PotKeys.Wants));
        await plans.UpdatePositionAsync(2026, 11, Rent.Id, new UpdatePositionRequest(Amount: 900m));
        await plans.DeletePositionAsync(2026, 11, Rent.Id);
        await plans.TickAsync(2026, 11, Rent.Id, new TickPositionRequest(Amount: 0m));
        await plans.UntickAsync(2026, 11, Rent.Id);
        await plans.ApplyChangesAsync(2026, 11, new ApplyPlanChangesRequest([new PlanChangeItem(Rent.Id, null)]));
        await plans.CreateCarryOverAsync(2026, 11, new CreateCarryOverRequest(230m));
        await plans.DeleteCarryOverAsync(2026, 11);

        var seen = api.Requests.Select(r => (r.Method.Method, r.Path)).ToList();
        Assert.Equal(
        [
            ("POST", "/api/v1/plans/2026/11/positions"),
            ("PATCH", $"/api/v1/plans/2026/11/positions/{Rent.Id}"),
            ("DELETE", $"/api/v1/plans/2026/11/positions/{Rent.Id}"),
            ("POST", $"/api/v1/plans/2026/11/positions/{Rent.Id}/tick"),
            ("DELETE", $"/api/v1/plans/2026/11/positions/{Rent.Id}/tick"),
            ("POST", "/api/v1/plans/2026/11/changes/apply"),
            ("POST", "/api/v1/plans/2026/11/carry-over"),
            ("DELETE", "/api/v1/plans/2026/11/carry-over"),
        ], seen);
        Assert.Contains("\"amount\":0", api.Requests[3].Body);
        Assert.Contains(Rent.Id.ToString(), api.Requests[5].Body);
    }

    [Fact]
    public async Task A_409_answer_gives_its_code()
    {
        var (_, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.Conflict, new ErrorResponse(ErrorCodes.PositionHasBookings)));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IPlanApi>().DeletePositionAsync(2026, 11, Rent.Id);

        Assert.Equal(ErrorCodes.PositionHasBookings, result.ErrorCode);
    }

    [Fact]
    public async Task Quotas_are_read_and_written_on_the_own_account()
    {
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.OK, new QuotaSettings(60, 20, 20)));
        using var _p = provider;
        using var _s = scope;
        var users = scope.ServiceProvider.GetRequiredService<IUserApi>();

        var read = await users.GetQuotasAsync();
        await users.UpdateQuotasAsync(new QuotaSettings(60, 20, 20));

        Assert.Equal(new QuotaSettings(60, 20, 20), read.Value);
        Assert.Equal((HttpMethod.Get, "/api/v1/users/me/quotas"), (api.Requests[0].Method, api.Requests[0].Path));
        Assert.Equal((HttpMethod.Put, "/api/v1/users/me/quotas"), (api.Requests[1].Method, api.Requests[1].Path));
        Assert.Contains("\"needs\":60", api.Requests[1].Body);
    }
}
