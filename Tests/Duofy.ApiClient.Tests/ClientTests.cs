using System.Net;
using Duofy.Schemas;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.ApiClient.Tests;

/// <summary>Die Clients übersetzen Antworten der API in Ergebnisse und pflegen die Token-Ablage.</summary>
public class ClientTests
{
    private static async Task<bool> IsSignedIn(IServiceProvider services) =>
        (await services.GetRequiredService<AuthenticationStateProvider>().GetAuthenticationStateAsync())
            .User.Identity!.IsAuthenticated;

    [Fact]
    public async Task Login_stores_the_tokens_and_signs_in()
    {
        var api = new FakeApi
        {
            Respond = r => r.Path == "/api/v1/auth/login"
                ? FakeApi.Json(HttpStatusCode.OK, new TokenResponse("Bearer", "access-1", 900, "refresh-1"))
                : new HttpResponseMessage(HttpStatusCode.NotFound),
        };
        var store = new InMemoryTokenStore();
        using var provider = TestHost.Create(api, store);
        using var scope = provider.CreateScope();
        var changes = 0;
        scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>().AuthenticationStateChanged += _ => changes++;

        var result = await scope.ServiceProvider.GetRequiredService<IAuthApi>()
            .LoginAsync(new LoginRequest("anna@example.org", "geheim-geheim"));

        Assert.True(result.Succeeded);
        Assert.Equal(new AuthTokens("access-1", "refresh-1"), store.Tokens);
        Assert.True(await IsSignedIn(scope.ServiceProvider));
        Assert.Equal(1, changes);
    }

    // Die Web-Variante kann ohne Cookie keine Sitzung führen; das wird ein Fehlercode,
    // keine Ausnahme, die die Verbindung zur Seite abreißen lässt.
    [Fact]
    public async Task Login_whose_tokens_cannot_be_stored_returns_the_code_of_the_store()
    {
        var api = new FakeApi
        {
            Respond = _ => FakeApi.Json(HttpStatusCode.OK, new TokenResponse("Bearer", "access-1", 900, "refresh-1")),
        };
        var store = new InMemoryTokenStore
        {
            SaveFailure = new TokenStoreUnavailableException(ClientErrorCodes.CookiesRequired, "Kein Cookie"),
        };
        using var provider = TestHost.Create(api, store);
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IAuthApi>()
            .LoginAsync(new LoginRequest("anna@example.org", "geheim-geheim"));

        Assert.Equal(ClientErrorCodes.CookiesRequired, result.ErrorCode);
        Assert.False(await IsSignedIn(scope.ServiceProvider));
    }

    [Fact]
    public async Task Failed_login_returns_the_code_and_never_tries_a_refresh()
    {
        var api = new FakeApi
        {
            Respond = _ => FakeApi.Json(HttpStatusCode.Unauthorized, new ErrorResponse(ErrorCodes.LoginFailed)),
        };
        var stale = new AuthTokens("stale-access", "stale-refresh");
        var store = new InMemoryTokenStore { Tokens = stale };
        using var provider = TestHost.Create(api, store);
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IAuthApi>()
            .LoginAsync(new LoginRequest("anna@example.org", "falsch-falsch"));

        Assert.Equal(ErrorCodes.LoginFailed, result.ErrorCode);
        Assert.Equal(0, api.CountOf("/api/v1/auth/refresh"));
        Assert.Null(Assert.Single(api.Requests).Authorization);
        Assert.Equal(stale, store.Tokens);
    }

    [Fact]
    public async Task Register_returns_the_code_of_a_400_answer()
    {
        var api = new FakeApi
        {
            Respond = _ => FakeApi.Json(HttpStatusCode.BadRequest, new ErrorResponse(ErrorCodes.EmailTaken)),
        };
        using var provider = TestHost.Create(api, new InMemoryTokenStore());
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IAuthApi>()
            .RegisterAsync(new RegisterRequest("Anna", "Beispiel", "anna@example.org", "geheim-geheim", true));

        Assert.Equal(ErrorCodes.EmailTaken, result.ErrorCode);
        var request = Assert.Single(api.Requests);
        Assert.Equal("/api/v1/auth/register", request.Path);
        Assert.Contains("\"acceptedTerms\":true", request.Body);
    }

    [Fact]
    public async Task Register_succeeds_on_201()
    {
        var api = new FakeApi { Respond = _ => new HttpResponseMessage(HttpStatusCode.Created) };
        using var provider = TestHost.Create(api, new InMemoryTokenStore());
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IAuthApi>()
            .RegisterAsync(new RegisterRequest("Anna", "Beispiel", "anna@example.org", "geheim-geheim", true));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Too_many_requests_is_reported_as_its_own_code()
    {
        var api = new FakeApi { Respond = _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests) };
        using var provider = TestHost.Create(api, new InMemoryTokenStore());
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IAuthApi>()
            .LoginAsync(new LoginRequest("anna@example.org", "geheim-geheim"));

        Assert.Equal(ClientErrorCodes.TooManyRequests, result.ErrorCode);
    }

    [Fact]
    public async Task An_unreachable_api_is_reported_as_server_unreachable()
    {
        var api = new FakeApi { Respond = _ => throw new HttpRequestException("no network") };
        using var provider = TestHost.Create(api, new InMemoryTokenStore());
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IAuthApi>()
            .LoginAsync(new LoginRequest("anna@example.org", "geheim-geheim"));

        Assert.Equal(ClientErrorCodes.ServerUnreachable, result.ErrorCode);
    }

    [Fact]
    public async Task Logout_clears_the_tokens_without_asking_the_api()
    {
        var api = new FakeApi();
        var store = new InMemoryTokenStore { Tokens = new AuthTokens("a", "r") };
        using var provider = TestHost.Create(api, store);
        using var scope = provider.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IAuthApi>().LogoutAsync();

        Assert.Null(store.Tokens);
        Assert.Empty(api.Requests);
        Assert.False(await IsSignedIn(scope.ServiceProvider));
    }

    [Fact]
    public async Task Logout_everywhere_calls_the_api_and_clears_the_tokens()
    {
        var api = new FakeApi { Respond = _ => new HttpResponseMessage(HttpStatusCode.NoContent) };
        var store = new InMemoryTokenStore { Tokens = new AuthTokens("a", "r") };
        using var provider = TestHost.Create(api, store);
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IAuthApi>().LogoutEverywhereAsync();

        Assert.True(result.Succeeded);
        var request = Assert.Single(api.Requests);
        Assert.Equal("/api/v1/auth/logout-everywhere", request.Path);
        Assert.Equal("Bearer a", request.Authorization);
        Assert.Null(store.Tokens);
    }

    [Fact]
    public async Task Delete_me_sends_the_password_and_clears_the_tokens()
    {
        var api = new FakeApi { Respond = _ => new HttpResponseMessage(HttpStatusCode.NoContent) };
        var store = new InMemoryTokenStore { Tokens = new AuthTokens("a", "r") };
        using var provider = TestHost.Create(api, store);
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IUserApi>()
            .DeleteMeAsync(new DeleteMeRequest("geheim-geheim"));

        Assert.True(result.Succeeded);
        var request = Assert.Single(api.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v1/users/me", request.Path);
        Assert.Contains("geheim-geheim", request.Body);
        Assert.Null(store.Tokens);
        Assert.False(await IsSignedIn(scope.ServiceProvider));
    }

    [Fact]
    public async Task Delete_me_with_a_wrong_password_keeps_the_tokens()
    {
        var api = new FakeApi
        {
            Respond = _ => FakeApi.Json(HttpStatusCode.BadRequest, new ErrorResponse(ErrorCodes.PasswordWrong)),
        };
        var tokens = new AuthTokens("a", "r");
        var store = new InMemoryTokenStore { Tokens = tokens };
        using var provider = TestHost.Create(api, store);
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IUserApi>()
            .DeleteMeAsync(new DeleteMeRequest("falsch-falsch"));

        Assert.Equal(ErrorCodes.PasswordWrong, result.ErrorCode);
        Assert.Equal(tokens, store.Tokens);
    }

    [Fact]
    public async Task Change_password_posts_to_the_password_endpoint()
    {
        var api = new FakeApi { Respond = _ => new HttpResponseMessage(HttpStatusCode.NoContent) };
        using var provider = TestHost.Create(api, new InMemoryTokenStore { Tokens = new AuthTokens("a", "r") });
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IUserApi>()
            .ChangePasswordAsync(new ChangePasswordRequest("alt-alt-alt", "neu-neu-neu"));

        Assert.True(result.Succeeded);
        var request = api.Requests[0];
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v1/users/me/password", request.Path);
    }

    [Fact]
    public async Task Change_password_signs_in_again_so_this_device_stays_signed_in()
    {
        // Ein neues Passwort macht alle alten Refresh-Tokens ungültig, auch das eigene.
        var api = new FakeApi
        {
            Respond = r => r.Path switch
            {
                "/api/v1/users/me/password" => new HttpResponseMessage(HttpStatusCode.NoContent),
                "/api/v1/users/me" => FakeApi.Json(HttpStatusCode.OK,
                    new MeResponse(Guid.NewGuid(), "anna@example.org", "Anna", "Beispiel")),
                "/api/v1/auth/login" when r.Body!.Contains("neu-neu-neu") && r.Body.Contains("anna@example.org") =>
                    FakeApi.Json(HttpStatusCode.OK, new TokenResponse("Bearer", "access-2", 900, "refresh-2")),
                _ => new HttpResponseMessage(HttpStatusCode.BadRequest),
            },
        };
        var store = new InMemoryTokenStore { Tokens = new AuthTokens("access-1", "refresh-1") };
        using var provider = TestHost.Create(api, store);
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IUserApi>()
            .ChangePasswordAsync(new ChangePasswordRequest("alt-alt-alt", "neu-neu-neu"));

        Assert.True(result.Succeeded);
        Assert.Equal(new AuthTokens("access-2", "refresh-2"), store.Tokens);
    }

    [Fact]
    public async Task Change_password_succeeds_even_if_signing_in_again_fails()
    {
        var api = new FakeApi
        {
            Respond = r => r.Path == "/api/v1/users/me/password"
                ? new HttpResponseMessage(HttpStatusCode.NoContent)
                : throw new HttpRequestException("no network"),
        };
        var tokens = new AuthTokens("access-1", "refresh-1");
        var store = new InMemoryTokenStore { Tokens = tokens };
        using var provider = TestHost.Create(api, store);
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IUserApi>()
            .ChangePasswordAsync(new ChangePasswordRequest("alt-alt-alt", "neu-neu-neu"));

        Assert.True(result.Succeeded);
        Assert.Equal(tokens, store.Tokens);
    }

    [Fact]
    public async Task Base_address_with_a_path_keeps_its_path()
    {
        var api = new FakeApi { Respond = _ => new HttpResponseMessage(HttpStatusCode.Created) };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDuofyApiClient(new Uri("https://example.test/duofy"));
        services.AddHttpClient(DuofyApiClientServiceCollectionExtensions.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => api);
        services.AddSingleton<ITokenStore>(new InMemoryTokenStore());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IAuthApi>()
            .RegisterAsync(new RegisterRequest("Anna", "Beispiel", "anna@example.org", "geheim-geheim", true));

        Assert.Equal("/duofy/api/v1/auth/register", Assert.Single(api.Requests).Path);
    }
}
