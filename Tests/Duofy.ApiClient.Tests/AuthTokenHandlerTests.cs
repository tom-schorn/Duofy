using System.Net;
using Duofy.Schemas;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.ApiClient.Tests;

/// <summary>
/// Der gemeinsame Handler hängt das Access-Token an und tauscht bei einer 401 das
/// Refresh-Token genau einmal gegen ein neues Paar (A8, A9).
/// </summary>
public class AuthTokenHandlerTests
{
    private static readonly MeResponse Anna = new(Guid.NewGuid(), "anna@example.org", "Anna", "Beispiel");

    [Fact]
    public async Task Sends_the_stored_access_token_as_bearer()
    {
        var api = new FakeApi { Respond = _ => FakeApi.Json(HttpStatusCode.OK, Anna) };
        var store = new InMemoryTokenStore { Tokens = new AuthTokens("access-1", "refresh-1") };
        using var provider = TestHost.Create(api, store);
        using var scope = provider.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IUserApi>().GetMeAsync();

        Assert.Equal("Bearer access-1", Assert.Single(api.Requests).Authorization);
    }

    // A8
    [Fact]
    public async Task Refreshes_exactly_once_and_repeats_the_call_when_the_api_answers_401()
    {
        var api = new FakeApi();
        api.Respond = request => (request.Path, request.Authorization) switch
        {
            ("/api/v1/users/me", "Bearer old-access") => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            ("/api/v1/auth/refresh", _) when request.Body!.Contains("old-refresh") =>
                FakeApi.Json(HttpStatusCode.OK, new TokenResponse("Bearer", "new-access", 900, "new-refresh")),
            ("/api/v1/users/me", "Bearer new-access") => FakeApi.Json(HttpStatusCode.OK, Anna),
            _ => new HttpResponseMessage(HttpStatusCode.BadRequest),
        };
        var store = new InMemoryTokenStore { Tokens = new AuthTokens("old-access", "old-refresh") };
        using var provider = TestHost.Create(api, store);
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IUserApi>().GetMeAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(Anna, result.Value);
        Assert.Equal(1, api.CountOf("/api/v1/auth/refresh"));
        Assert.Equal(2, api.CountOf("/api/v1/users/me"));
        Assert.Equal(new AuthTokens("new-access", "new-refresh"), store.Tokens);
    }

    [Fact]
    public async Task Repeats_a_call_with_a_body_unchanged_after_the_refresh()
    {
        var api = new FakeApi();
        api.Respond = request => (request.Path, request.Authorization) switch
        {
            ("/api/v1/users/me", "Bearer old-access") => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            ("/api/v1/auth/refresh", _) =>
                FakeApi.Json(HttpStatusCode.OK, new TokenResponse("Bearer", "new-access", 900, "new-refresh")),
            ("/api/v1/users/me", "Bearer new-access") => FakeApi.Json(HttpStatusCode.OK, Anna with { FirstName = "Annika" }),
            _ => new HttpResponseMessage(HttpStatusCode.BadRequest),
        };
        var store = new InMemoryTokenStore { Tokens = new AuthTokens("old-access", "old-refresh") };
        using var provider = TestHost.Create(api, store);
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IUserApi>()
            .UpdateMeAsync(new UpdateMeRequest("Annika", "Beispiel"));

        Assert.True(result.Succeeded);
        var calls = api.Requests.Where(r => r.Path == "/api/v1/users/me").ToList();
        Assert.Equal(2, calls.Count);
        Assert.Equal(HttpMethod.Patch, calls[1].Method);
        Assert.Equal(calls[0].Body, calls[1].Body);
        Assert.Contains("Annika", calls[1].Body);
    }

    // A9
    [Fact]
    public async Task Clears_the_tokens_and_signs_out_when_the_refresh_answers_401()
    {
        var api = new FakeApi { Respond = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized) };
        var store = new InMemoryTokenStore { Tokens = new AuthTokens("old-access", "old-refresh") };
        using var provider = TestHost.Create(api, store);
        using var scope = provider.CreateScope();
        var authState = scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>();
        Assert.True((await authState.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated);

        var result = await scope.ServiceProvider.GetRequiredService<IUserApi>().GetMeAsync();

        Assert.Equal(ClientErrorCodes.NotSignedIn, result.ErrorCode);
        Assert.Equal(1, api.CountOf("/api/v1/auth/refresh"));
        Assert.Null(store.Tokens);
        Assert.False((await authState.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated);
    }

    [Fact]
    public async Task Does_not_refresh_when_no_tokens_are_stored()
    {
        var api = new FakeApi { Respond = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized) };
        var store = new InMemoryTokenStore();
        using var provider = TestHost.Create(api, store);
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IUserApi>().GetMeAsync();

        Assert.Equal(ClientErrorCodes.NotSignedIn, result.ErrorCode);
        Assert.Equal(0, api.CountOf("/api/v1/auth/refresh"));
        Assert.Null(Assert.Single(api.Requests).Authorization);
    }

    [Fact]
    public async Task Keeps_the_tokens_when_the_api_is_unreachable_during_the_refresh()
    {
        var api = new FakeApi();
        api.Respond = request => request.Path == "/api/v1/auth/refresh"
            ? throw new HttpRequestException("no network")
            : new HttpResponseMessage(HttpStatusCode.Unauthorized);
        var tokens = new AuthTokens("old-access", "old-refresh");
        var store = new InMemoryTokenStore { Tokens = tokens };
        using var provider = TestHost.Create(api, store);
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IUserApi>().GetMeAsync();

        Assert.Equal(ClientErrorCodes.ServerUnreachable, result.ErrorCode);
        Assert.Equal(tokens, store.Tokens);
    }
}
