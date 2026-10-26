using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>"Passwort vergessen" auf der gehosteten Version (Spec 53, R3): A5 und A6.</summary>
[Collection(ApiCollection.Name)]
public sealed class PasswordResetTests(ApiFixture fixture) : IAsyncLifetime
{
    private const string NewPassword = "ganz-neues-passwort";

    private MailServer server = null!;

    public Task InitializeAsync()
    {
        server = MailServer.Hosted(fixture);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await server.DisposeAsync();

    private HttpClient Client => server.Client;

    private Task<HttpResponseMessage> ForgotAsync(string email) =>
        Client.PostAsJsonAsync("/api/v1/auth/password/forgot", new ForgotPasswordRequest(email));

    private static Task<HttpResponseMessage> ResetAsync(HttpClient client, OutgoingMail mail, string password = NewPassword)
    {
        var link = MailLink.Of(mail);
        return client.PostAsJsonAsync("/api/v1/auth/password/reset", new ResetPasswordRequest(link["email"], link["token"], password));
    }

    // A5
    [Fact]
    public async Task Forgot_answers_202_for_everybody_and_only_an_account_gets_a_link_that_signs_out_everywhere()
    {
        var anna = await server.ConfirmedAsync();
        var oldTokens = await server.TokensAsync(anna);
        var nobody = NewEmail("niemand");

        var forAnna = await ForgotAsync(anna);
        var forNobody = await ForgotAsync(nobody);

        Assert.Equal(HttpStatusCode.Accepted, forAnna.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, forNobody.StatusCode);
        Assert.Equal(await forAnna.Content.ReadAsStringAsync(), await forNobody.Content.ReadAsStringAsync());
        Assert.Empty(server.Mails.To(nobody));
        var mail = server.Mails.To(anna).Last();
        Assert.Equal("Neues Passwort für Duofy", mail.Subject);
        Assert.StartsWith("https://duofy.example.de/passwort-neu?email=", MailLink.UrlOf(mail).AbsoluteUri);

        var reset = await ResetAsync(Client, mail);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await server.RefreshAsync(oldTokens.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.LoginAsync(anna)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.LoginAsync(anna, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task A_reset_link_works_once_and_follows_the_password_rules()
    {
        var anna = await server.ConfirmedAsync();
        await ForgotAsync(anna);
        var mail = server.Mails.To(anna).Last();

        var tooShort = await ResetAsync(Client, mail, "kurz");
        var first = await ResetAsync(Client, mail);
        var again = await ResetAsync(Client, mail, "noch-ein-passwort");

        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Equal(ErrorCodes.PasswordTooShort, await tooShort.ReadCodeAsync());
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal(ErrorCodes.TokenInvalid, await again.ReadCodeAsync());
    }

    [Fact]
    public async Task Resetting_through_the_mail_confirms_the_address_too()
    {
        var anna = NewEmail();
        (await server.RegisterAsync(anna)).EnsureSuccessStatusCode();
        await ForgotAsync(anna);

        await ResetAsync(Client, server.Mails.To(anna).Last());

        Assert.Equal(HttpStatusCode.OK, (await Client.LoginAsync(anna, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Asking_twice_within_five_minutes_sends_one_link()
    {
        var anna = await server.ConfirmedAsync();

        await ForgotAsync(anna);
        await ForgotAsync(anna);

        Assert.Single(server.Mails.To(anna), m => m.Subject == "Neues Passwort für Duofy");
    }

    // A6
    [Fact]
    public async Task A_reset_link_past_its_lifetime_is_invalid_and_the_password_stays()
    {
        await using var expiring = MailServer.Hosted(fixture, services =>
            services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.Zero));
        var anna = await expiring.ConfirmedAsync();
        await expiring.Client.PostAsJsonAsync("/api/v1/auth/password/forgot", new ForgotPasswordRequest(anna));
        await Task.Delay(20);

        var reset = await ResetAsync(expiring.Client, expiring.Mails.To(anna).Last());

        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
        Assert.Equal(ErrorCodes.TokenInvalid, await reset.ReadCodeAsync());
        Assert.Equal(HttpStatusCode.OK, (await expiring.Client.LoginAsync(anna)).StatusCode);
    }

    [Fact]
    public async Task A_reset_for_an_unknown_address_is_invalid()
    {
        var reset = await Client.PostAsJsonAsync("/api/v1/auth/password/reset",
            new ResetPasswordRequest(NewEmail("niemand"), "token", NewPassword));

        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
        Assert.Equal(ErrorCodes.TokenInvalid, await reset.ReadCodeAsync());
    }
}
