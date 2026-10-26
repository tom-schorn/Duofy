using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>Anmeldung, Tokens, Abmeldung überall und Bremsen (R3, R4, R7, R8).</summary>
[Collection(ApiCollection.Name)]
public class SignInTests
{
    private readonly ApiFixture fixture;
    private readonly HttpClient client;

    public SignInTests(ApiFixture fixture)
    {
        this.fixture = fixture;
        client = fixture.Factory.CreateClient();
    }

    // A5
    [Fact]
    public async Task Right_password_returns_a_token_pair_for_15_minutes()
    {
        var email = NewEmail();
        await client.RegisterAsync(Registration(email));

        var response = await client.LoginAsync(email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.Equal("Bearer", tokens.TokenType);
        Assert.False(string.IsNullOrEmpty(tokens.AccessToken));
        Assert.False(string.IsNullOrEmpty(tokens.RefreshToken));
        Assert.Equal(900, tokens.ExpiresIn);
    }

    // A5
    [Fact]
    public async Task Wrong_password_and_unknown_email_answer_the_same_401()
    {
        var email = NewEmail();
        await client.RegisterAsync(Registration(email));

        var wrongPassword = await client.LoginAsync(email, "falsch-falsch");
        var unknownEmail = await client.LoginAsync(NewEmail("niemand"));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(ErrorCodes.LoginFailed, await wrongPassword.ReadCodeAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(ErrorCodes.LoginFailed, await unknownEmail.ReadCodeAsync());
    }

    [Fact]
    public async Task Sign_in_ignores_the_case_of_the_email()
    {
        var email = NewEmail();
        await client.RegisterAsync(Registration(email));

        var response = await client.LoginAsync(email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // A6
    [Fact]
    public async Task Me_needs_a_valid_access_token()
    {
        var (_, tokens) = await client.SignUpAsync();

        var withToken = await client.SendAsync(Authorized(HttpMethod.Get, "/api/v1/users/me", tokens.AccessToken));
        var withoutToken = await client.GetAsync("/api/v1/users/me");

        Assert.Equal(HttpStatusCode.OK, withToken.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, withoutToken.StatusCode);
    }

    // A7
    [Fact]
    public async Task Refresh_token_is_traded_for_a_new_pair()
    {
        var (_, tokens) = await client.SignUpAsync();

        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(tokens.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var renewed = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.NotEqual(tokens.AccessToken, renewed.AccessToken);
        Assert.NotEqual(tokens.RefreshToken, renewed.RefreshToken);
        var me = await client.SendAsync(Authorized(HttpMethod.Get, "/api/v1/users/me", renewed.AccessToken));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    // A7
    [Fact]
    public async Task Changed_refresh_token_answers_401()
    {
        var (_, tokens) = await client.SignUpAsync();
        var token = tokens.RefreshToken;
        var changed = token[..10] + (token[10] == 'A' ? 'B' : 'A') + token[11..];

        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(changed));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Access_token_is_no_refresh_token()
    {
        var (_, tokens) = await client.SignUpAsync();

        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(tokens.AccessToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // A16
    [Fact]
    public async Task Logout_everywhere_invalidates_the_refresh_token_of_the_other_device()
    {
        var (email, phone) = await client.SignUpAsync();
        var browser = (await (await client.LoginAsync(email)).Content.ReadFromJsonAsync<TokenResponse>())!;

        var logout = await client.SendAsync(Authorized(HttpMethod.Post, "/api/v1/auth/logout-everywhere", phone.AccessToken));

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(browser.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_everywhere_needs_a_token()
    {
        var response = await client.PostAsync("/api/v1/auth/logout-everywhere", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // A17
    [Fact]
    public async Task Five_failed_sign_ins_lock_the_account()
    {
        var email = NewEmail();
        await client.RegisterAsync(Registration(email));
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.LoginAsync(email, "falsch-falsch")).StatusCode);
        }

        var sixth = await client.LoginAsync(email);

        Assert.Equal(HttpStatusCode.Unauthorized, sixth.StatusCode);
        Assert.Equal(ErrorCodes.LoginFailed, await sixth.ReadCodeAsync());
    }

    // A18
    [Fact]
    public async Task Eleventh_auth_request_within_a_minute_answers_429()
    {
        await using var limited = new DuofyApiFactory(fixture.ConnectionString, authPermitLimit: 10);
        var limitedClient = limited.CreateClient();
        var email = NewEmail("niemand");
        for (var request = 0; request < 10; request++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await limitedClient.LoginAsync(email)).StatusCode);
        }

        var eleventh = await limitedClient.LoginAsync(email);

        Assert.Equal(HttpStatusCode.TooManyRequests, eleventh.StatusCode);
    }

    [Fact]
    public async Task Rate_limit_does_not_apply_outside_auth()
    {
        await using var limited = new DuofyApiFactory(fixture.ConnectionString, authPermitLimit: 10);
        var limitedClient = limited.CreateClient();

        for (var request = 0; request < 11; request++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await limitedClient.GetAsync("/api/v1/users/me")).StatusCode);
        }
    }
}
