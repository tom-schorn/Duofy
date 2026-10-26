using System.Net;
using Duofy.Schemas;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.ApiClient.Tests;

/// <summary>Der Client für Verträge: richtige Adressen, Inhalte und Fehlercodes.</summary>
public class CommitmentClientTests
{
    private static readonly CommitmentResponse Rent = new(
        Guid.NewGuid(), CommitmentTypes.Contract, "Miete", 890m, 1, new DateOnly(2026, 11, 1), null,
        Guid.NewGuid(), PotKeys.Needs, null, null, false, PaymentMethods.StandingOrder, null, null,
        new DateOnly(2026, 11, 1), true);

    private static (FakeApi Api, ServiceProvider Provider, IServiceScope Scope) Start(Func<SeenRequest, HttpResponseMessage> respond)
    {
        var api = new FakeApi { Respond = respond };
        var provider = TestHost.Create(api, new InMemoryTokenStore { Tokens = new AuthTokens("access", "refresh") });
        return (api, provider, provider.CreateScope());
    }

    [Fact]
    public async Task List_reads_the_running_contracts_or_all_of_them()
    {
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.OK, new[] { Rent }));
        using var _p = provider;
        using var _s = scope;
        var commitments = scope.ServiceProvider.GetRequiredService<ICommitmentApi>();

        var running = await commitments.ListAsync();
        await commitments.ListAsync(includeEnded: true);

        Assert.Equal(Rent.Id, Assert.Single(running.Value!).Id);
        Assert.Equal("/api/v1/commitments", api.Requests[0].Path);
        Assert.Equal("", api.Requests[0].Query);
        Assert.Equal("?includeEnded=true", api.Requests[1].Query);
        Assert.Equal("Bearer access", api.Requests[0].Authorization);
    }

    [Fact]
    public async Task Create_posts_the_contract_with_its_keys()
    {
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.Created, Rent));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<ICommitmentApi>().CreateAsync(new CreateCommitmentRequest(
            CommitmentTypes.SavingsGoal, "Urlaub", 100m, 1, new DateOnly(2026, 11, 1), null, Guid.NewGuid(), PotKeys.Savings,
            PaymentMethod: PaymentMethods.StandingOrder));

        Assert.Equal(Rent.Id, result.Value!.Id);
        var request = Assert.Single(api.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v1/commitments", request.Path);
        Assert.Contains("\"type\":\"savings_goal\"", request.Body);
        Assert.Contains("\"paymentMethod\":\"standing_order\"", request.Body);
    }

    [Fact]
    public async Task Update_patches_the_contract_by_its_id()
    {
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.OK, Rent));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<ICommitmentApi>()
            .UpdateAsync(Rent.Id, new UpdateCommitmentRequest(ClearLastDueDate: true));

        Assert.True(result.Succeeded);
        var request = Assert.Single(api.Requests);
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Equal($"/api/v1/commitments/{Rent.Id}", request.Path);
        Assert.Contains("\"clearLastDueDate\":true", request.Body);
    }

    [Fact]
    public async Task End_posts_to_the_end_address_of_the_contract()
    {
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.OK, Rent with { LastDueDate = new DateOnly(2026, 10, 1) }));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<ICommitmentApi>().EndAsync(Rent.Id);

        Assert.Equal(new DateOnly(2026, 10, 1), result.Value!.LastDueDate);
        var request = Assert.Single(api.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"/api/v1/commitments/{Rent.Id}/end", request.Path);
    }

    [Fact]
    public async Task A_400_answer_gives_its_code_and_a_404_gives_not_found()
    {
        var (_, provider, scope) = Start(request => request.Method == HttpMethod.Post
            ? FakeApi.Json(HttpStatusCode.BadRequest, new ErrorResponse(ErrorCodes.CommitmentPotInvalid))
            : FakeApi.Json(HttpStatusCode.NotFound, new ErrorResponse(ErrorCodes.CommitmentNotFound)));
        using var _p = provider;
        using var _s = scope;
        var commitments = scope.ServiceProvider.GetRequiredService<ICommitmentApi>();

        var created = await commitments.CreateAsync(new CreateCommitmentRequest(
            CommitmentTypes.Contract, "Miete", 890m, 1, new DateOnly(2026, 11, 1), null, Guid.NewGuid(), PotKeys.Savings));
        var updated = await commitments.UpdateAsync(Guid.NewGuid(), new UpdateCommitmentRequest(Name: "x"));

        Assert.Equal(ErrorCodes.CommitmentPotInvalid, created.ErrorCode);
        Assert.Equal(ClientErrorCodes.NotFound, updated.ErrorCode);
    }
}
