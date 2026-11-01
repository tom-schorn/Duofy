using System.Net;
using Duofy.Schemas;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.ApiClient.Tests;

/// <summary>Der Client für Sparziele: richtige Adressen, Inhalte und Fehlercodes (Issue 30).</summary>
public class SavingsGoalClientTests
{
    private static readonly SavingsGoalResponse Holiday = new(Guid.NewGuid(), "Urlaub", 1200m, null, null, 350m, false);

    private static (FakeApi Api, ServiceProvider Provider, IServiceScope Scope) Start(Func<SeenRequest, HttpResponseMessage> respond)
    {
        var api = new FakeApi { Respond = respond };
        var provider = TestHost.Create(api, new InMemoryTokenStore { Tokens = new AuthTokens("access", "refresh") });
        return (api, provider, provider.CreateScope());
    }

    [Fact]
    public async Task List_reads_the_goals_of_the_person_or_of_another_owner()
    {
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.OK, new[] { Holiday }));
        using var _p = provider;
        using var _s = scope;
        var goals = scope.ServiceProvider.GetRequiredService<ISavingsGoalApi>();
        var owner = Guid.NewGuid();

        var own = await goals.ListAsync();
        await goals.ListAsync(owner);

        Assert.Equal(350m, Assert.Single(own.Value!).Balance);
        Assert.Equal("/api/v1/savings-goals", api.Requests[0].Path);
        Assert.Equal("", api.Requests[0].Query);
        Assert.Equal($"?owner={owner}", api.Requests[1].Query);
        Assert.Equal("Bearer access", api.Requests[0].Authorization);
    }

    [Fact]
    public async Task Create_posts_the_goal_and_update_patches_it_by_id()
    {
        var (api, provider, scope) = Start(request => FakeApi.Json(request.Method == HttpMethod.Post ? HttpStatusCode.Created : HttpStatusCode.OK, Holiday));
        using var _p = provider;
        using var _s = scope;
        var goals = scope.ServiceProvider.GetRequiredService<ISavingsGoalApi>();

        var created = await goals.CreateAsync(new CreateSavingsGoalRequest("Urlaub", 1200m));
        var changed = await goals.UpdateAsync(Holiday.Id, new UpdateSavingsGoalRequest(ClearTargetAmount: true));

        Assert.Equal(Holiday.Id, created.Value!.Id);
        Assert.True(changed.Succeeded);
        Assert.Equal(HttpMethod.Post, api.Requests[0].Method);
        Assert.Equal("/api/v1/savings-goals", api.Requests[0].Path);
        Assert.Contains("\"name\":\"Urlaub\"", api.Requests[0].Body);
        Assert.Equal(HttpMethod.Patch, api.Requests[1].Method);
        Assert.Equal($"/api/v1/savings-goals/{Holiday.Id}", api.Requests[1].Path);
        Assert.Contains("\"clearTargetAmount\":true", api.Requests[1].Body);
    }

    [Fact]
    public async Task Delete_removes_the_goal_and_a_409_gives_its_code()
    {
        var (api, provider, scope) = Start(request => request.Path.EndsWith(Holiday.Id.ToString())
            ? new HttpResponseMessage(HttpStatusCode.NoContent)
            : FakeApi.Json(HttpStatusCode.Conflict, new ErrorResponse(ErrorCodes.SavingsGoalHasBookings)));
        using var _p = provider;
        using var _s = scope;
        var goals = scope.ServiceProvider.GetRequiredService<ISavingsGoalApi>();

        var deleted = await goals.DeleteAsync(Holiday.Id);
        var refused = await goals.DeleteAsync(Guid.NewGuid());

        Assert.True(deleted.Succeeded);
        Assert.Equal(HttpMethod.Delete, api.Requests[0].Method);
        Assert.Equal(ErrorCodes.SavingsGoalHasBookings, refused.ErrorCode);
    }
}
