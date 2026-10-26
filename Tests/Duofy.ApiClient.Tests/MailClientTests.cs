using System.Net;
using Duofy.Schemas;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.ApiClient.Tests;

/// <summary>
/// Der Client für Bestätigung, "Passwort vergessen", Links des Administrators und den Wechsel der Adresse (Spec 53).
/// </summary>
public class MailClientTests
{
    private static (FakeApi Api, InMemoryTokenStore Store, ServiceProvider Provider, IServiceScope Scope) Start(
        Func<SeenRequest, HttpResponseMessage> respond, AuthTokens? tokens = null)
    {
        var api = new FakeApi { Respond = respond };
        var store = new InMemoryTokenStore { Tokens = tokens };
        var provider = TestHost.Create(api, store);
        return (api, store, provider, provider.CreateScope());
    }

    [Fact]
    public async Task The_anonymous_mail_requests_go_to_their_addresses_without_a_token()
    {
        var (api, _, provider, scope) = Start(_ => new HttpResponseMessage(HttpStatusCode.Accepted), new AuthTokens("access", "refresh"));
        using var _p = provider;
        using var _s = scope;
        var auth = scope.ServiceProvider.GetRequiredService<IAuthApi>();
        var id = Guid.NewGuid();

        var results = new[]
        {
            await auth.ConfirmEmailAsync(new ConfirmEmailRequest(id, "t1")),
            await auth.ResendConfirmationAsync(new ResendConfirmationRequest("anna@example.de")),
            await auth.ForgotPasswordAsync(new ForgotPasswordRequest("anna@example.de")),
            await auth.ResetPasswordAsync(new ResetPasswordRequest("anna@example.de", "t2", "neues-passwort")),
            await auth.ResetPasswordByLinkAsync(new ResetPasswordByLinkRequest("t3", "neues-passwort")),
            await auth.ConfirmEmailChangeAsync(new ConfirmEmailChangeRequest(id, "neu@example.de", "t4")),
        };

        Assert.All(results, r => Assert.True(r.Succeeded));
        Assert.Equal(
            [
                "/api/v1/auth/confirm-email",
                "/api/v1/auth/confirm-email/resend",
                "/api/v1/auth/password/forgot",
                "/api/v1/auth/password/reset",
                "/api/v1/auth/password/reset-by-link",
                "/api/v1/auth/confirm-email-change",
            ],
            api.Requests.Select(r => r.Path));
        Assert.All(api.Requests, r => Assert.Equal(HttpMethod.Post, r.Method));
        Assert.All(api.Requests, r => Assert.Null(r.Authorization));
        Assert.Contains("\"token\":\"t2\"", api.Requests[3].Body);
        Assert.Contains("\"newEmail\":\"neu@example.de\"", api.Requests[5].Body);
    }

    [Fact]
    public async Task An_invalid_token_and_an_unconfirmed_sign_in_give_their_codes()
    {
        var (_, store, provider, scope) = Start(request => request.Path == "/api/v1/auth/login"
            ? FakeApi.Json(HttpStatusCode.Forbidden, new ErrorResponse(ErrorCodes.EmailNotConfirmed))
            : FakeApi.Json(HttpStatusCode.BadRequest, new ErrorResponse(ErrorCodes.TokenInvalid)));
        using var _p = provider;
        using var _s = scope;
        var auth = scope.ServiceProvider.GetRequiredService<IAuthApi>();

        var confirm = await auth.ConfirmEmailAsync(new ConfirmEmailRequest(Guid.NewGuid(), "alt"));
        var login = await auth.LoginAsync(new LoginRequest("anna@example.de", "zehn-zeich"));

        Assert.Equal(ErrorCodes.TokenInvalid, confirm.ErrorCode);
        Assert.Equal(ErrorCodes.EmailNotConfirmed, login.ErrorCode);
        Assert.Null(store.Tokens);
    }

    [Fact]
    public async Task A_change_that_waits_for_confirmation_succeeds_without_an_account_and_keeps_the_tokens()
    {
        var (api, store, provider, scope) = Start(_ => new HttpResponseMessage(HttpStatusCode.Accepted), new AuthTokens("access", "refresh"));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IUserApi>()
            .ChangeEmailAsync(new ChangeEmailRequest("neu@example.de", "zehn-zeich"));

        Assert.True(result.Succeeded);
        Assert.Null(result.Value);
        var request = Assert.Single(api.Requests);
        Assert.Equal("/api/v1/users/me/email", request.Path);
        Assert.Equal("Bearer access", request.Authorization);
        Assert.Contains("\"password\":\"zehn-zeich\"", request.Body);
        Assert.Equal(new AuthTokens("access", "refresh"), store.Tokens);
    }

    [Fact]
    public async Task A_change_at_once_returns_the_account_and_signs_in_again_with_the_new_address()
    {
        var me = new MeResponse(Guid.NewGuid(), "neu@example.de", "Anna", "Muster");
        var (api, store, provider, scope) = Start(request => request.Path == "/api/v1/auth/login"
            ? FakeApi.Json(HttpStatusCode.OK, new TokenResponse("Bearer", "access-neu", 900, "refresh-neu"))
            : FakeApi.Json(HttpStatusCode.OK, me), new AuthTokens("access", "refresh"));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IUserApi>()
            .ChangeEmailAsync(new ChangeEmailRequest("neu@example.de", "zehn-zeich"));

        Assert.Equal(me, result.Value);
        Assert.Equal("/api/v1/auth/login", api.Requests[1].Path);
        Assert.Contains("\"email\":\"neu@example.de\"", api.Requests[1].Body);
        Assert.Equal(new AuthTokens("access-neu", "refresh-neu"), store.Tokens);
    }

    [Fact]
    public async Task A_refused_change_gives_its_code()
    {
        var (_, _, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.Conflict, new ErrorResponse(ErrorCodes.EmailTaken)), new AuthTokens("access", "refresh"));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IUserApi>()
            .ChangeEmailAsync(new ChangeEmailRequest("ben@example.de", "zehn-zeich"));

        Assert.Equal(ErrorCodes.EmailTaken, result.ErrorCode);
    }

    [Fact]
    public async Task The_administrator_gets_a_reset_link_and_an_unknown_address_is_named_as_such()
    {
        var link = new PasswordResetLinkResponse("https://duofy.example.de/passwort-neu?link=abc", DateTimeOffset.UtcNow.AddHours(24));
        var (api, _, provider, scope) = Start(request => request.Body!.Contains("anna")
            ? FakeApi.Json(HttpStatusCode.Created, link)
            : FakeApi.Json(HttpStatusCode.NotFound, new ErrorResponse(ErrorCodes.UserNotFound)), new AuthTokens("access", "refresh"));
        using var _p = provider;
        using var _s = scope;
        var admin = scope.ServiceProvider.GetRequiredService<IAdminApi>();

        var created = await admin.CreatePasswordResetLinkAsync("anna@example.de");
        var unknown = await admin.CreatePasswordResetLinkAsync("niemand@example.de");

        Assert.Equal(link, created.Value);
        Assert.Equal("/api/v1/admin/password-reset-links", api.Requests[0].Path);
        Assert.Equal(ErrorCodes.UserNotFound, unknown.ErrorCode);
    }

    [Fact]
    public async Task A_household_invitation_carries_the_address_when_there_is_one()
    {
        var invitation = new InvitationResponse(Guid.NewGuid(), "tok", DateTimeOffset.UtcNow.AddDays(14), "https://duofy.example.de/einladung/tok");
        var (api, _, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.Created, invitation), new AuthTokens("access", "refresh"));
        using var _p = provider;
        using var _s = scope;
        var households = scope.ServiceProvider.GetRequiredService<IHouseholdApi>();
        var id = Guid.NewGuid();

        await households.CreateInvitationAsync(id, "ben@example.de");
        await households.CreateInvitationAsync(id);

        Assert.Contains("\"email\":\"ben@example.de\"", api.Requests[0].Body);
        Assert.Null(api.Requests[1].Body);
    }
}
