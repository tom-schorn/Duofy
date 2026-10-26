using System.Net;
using Duofy.Schemas;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.ApiClient.Tests;

/// <summary>Der Client für Konten: richtige Adressen, Inhalte und Fehlercodes.</summary>
public class AccountClientTests
{
    private static readonly AccountResponse Giro = new(
        Guid.NewGuid(), "Giro", AccountTypes.Checking, 1250m, new DateOnly(2026, 10, 1), true, null, true, true, 1250m);

    private static (FakeApi Api, ServiceProvider Provider, IServiceScope Scope) Start(Func<SeenRequest, HttpResponseMessage> respond)
    {
        var api = new FakeApi { Respond = respond };
        var provider = TestHost.Create(api, new InMemoryTokenStore { Tokens = new AuthTokens("access", "refresh") });
        return (api, provider, provider.CreateScope());
    }

    [Fact]
    public async Task List_reads_all_accounts_or_only_the_active_ones()
    {
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.OK, new[] { Giro }));
        using var _p = provider;
        using var _s = scope;
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountApi>();

        var all = await accounts.ListAsync();
        await accounts.ListAsync(activeOnly: true);

        Assert.Equal(Giro.Id, Assert.Single(all.Value!).Id);
        Assert.Equal("/api/v1/accounts", api.Requests[0].Path);
        Assert.Equal("", api.Requests[0].Query);
        Assert.Equal("?active=true", api.Requests[1].Query);
        Assert.Equal("Bearer access", api.Requests[0].Authorization);
    }

    [Fact]
    public async Task Create_posts_the_account_and_returns_the_saved_one()
    {
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.Created, Giro));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IAccountApi>()
            .CreateAsync(new CreateAccountRequest("Giro", AccountTypes.Checking, 1250m, new DateOnly(2026, 10, 1)));

        Assert.Equal(Giro.Id, result.Value!.Id);
        var request = Assert.Single(api.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v1/accounts", request.Path);
        Assert.Contains("\"type\":\"checking\"", request.Body);
    }

    [Fact]
    public async Task Update_patches_the_account_by_its_id()
    {
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.OK, Giro));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IAccountApi>()
            .UpdateAsync(Giro.Id, new UpdateAccountRequest(Active: false));

        Assert.True(result.Succeeded);
        var request = Assert.Single(api.Requests);
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Equal($"/api/v1/accounts/{Giro.Id}", request.Path);
        Assert.Contains("\"active\":false", request.Body);
    }

    [Fact]
    public async Task A_400_answer_gives_its_code_and_a_404_gives_not_found()
    {
        var (_, provider, scope) = Start(request => request.Method == HttpMethod.Post
            ? FakeApi.Json(HttpStatusCode.BadRequest, new ErrorResponse(ErrorCodes.IbanInvalid))
            : FakeApi.Json(HttpStatusCode.NotFound, new ErrorResponse(ErrorCodes.AccountNotFound)));
        using var _p = provider;
        using var _s = scope;
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountApi>();

        var created = await accounts.CreateAsync(new CreateAccountRequest("Giro", AccountTypes.Checking, 0m, new DateOnly(2026, 10, 1)));
        var updated = await accounts.UpdateAsync(Guid.NewGuid(), new UpdateAccountRequest(Name: "x"));

        Assert.Equal(ErrorCodes.IbanInvalid, created.ErrorCode);
        Assert.Equal(ClientErrorCodes.NotFound, updated.ErrorCode);
    }
}
