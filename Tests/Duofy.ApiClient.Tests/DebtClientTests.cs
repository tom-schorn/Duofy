using System.Net;
using Duofy.Schemas;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.ApiClient.Tests;

/// <summary>Der Client für Schulden und Kredite: richtige Adressen, Inhalte und Fehlercodes (Issue 31).</summary>
public class DebtClientTests
{
    private static readonly DebtResponse Bill = new(Guid.NewGuid(), DebtKinds.Claim, "Strom", "Stadtwerke", 300m, null, null, 100m, 200m, false);

    private static (FakeApi Api, ServiceProvider Provider, IServiceScope Scope) Start(Func<SeenRequest, HttpResponseMessage> respond)
    {
        var api = new FakeApi { Respond = respond };
        var provider = TestHost.Create(api, new InMemoryTokenStore { Tokens = new AuthTokens("access", "refresh") });
        return (api, provider, provider.CreateScope());
    }

    [Fact]
    public async Task List_reads_the_entries_of_the_person_or_of_another_owner()
    {
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.OK, new[] { Bill }));
        using var _p = provider;
        using var _s = scope;
        var debts = scope.ServiceProvider.GetRequiredService<IDebtApi>();
        var owner = Guid.NewGuid();

        var own = await debts.ListAsync();
        await debts.ListAsync(owner);

        Assert.Equal(200m, Assert.Single(own.Value!).Open);
        Assert.Equal("/api/v1/debts", api.Requests[0].Path);
        Assert.Equal($"?owner={owner}", api.Requests[1].Query);
    }

    [Fact]
    public async Task Create_posts_update_patches_and_delete_deletes_by_id()
    {
        var (api, provider, scope) = Start(request => request.Method == HttpMethod.Delete
            ? new HttpResponseMessage(HttpStatusCode.NoContent)
            : FakeApi.Json(request.Method == HttpMethod.Post ? HttpStatusCode.Created : HttpStatusCode.OK, Bill));
        using var _p = provider;
        using var _s = scope;
        var debts = scope.ServiceProvider.GetRequiredService<IDebtApi>();

        await debts.CreateAsync(new CreateDebtRequest(DebtKinds.Loan, "Kredit", "Bank", 1000m));
        await debts.UpdateAsync(Bill.Id, new UpdateDebtRequest(ClearNote: true));
        var deleted = await debts.DeleteAsync(Bill.Id);

        Assert.Equal((HttpMethod.Post, "/api/v1/debts"), (api.Requests[0].Method, api.Requests[0].Path));
        Assert.Contains("\"kind\":\"loan\"", api.Requests[0].Body);
        Assert.Equal((HttpMethod.Patch, $"/api/v1/debts/{Bill.Id}"), (api.Requests[1].Method, api.Requests[1].Path));
        Assert.Contains("\"clearNote\":true", api.Requests[1].Body);
        Assert.True(deleted.Succeeded);
        Assert.Equal(HttpMethod.Delete, api.Requests[2].Method);
    }

    [Fact]
    public async Task A_409_answer_gives_its_code()
    {
        var (_, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.Conflict, new ErrorResponse(ErrorCodes.DebtHasBookings)));
        using var _p = provider;
        using var _s = scope;

        var refused = await scope.ServiceProvider.GetRequiredService<IDebtApi>().DeleteAsync(Guid.NewGuid());

        Assert.Equal(ErrorCodes.DebtHasBookings, refused.ErrorCode);
    }
}
