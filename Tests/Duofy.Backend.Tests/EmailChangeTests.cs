using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>Die E-Mail-Adresse ändern (Spec 53, R5): A9 auf der gehosteten Version, A10 auf einem selbst betriebenen Server.</summary>
[Collection(ApiCollection.Name)]
public sealed class EmailChangeTests(ApiFixture fixture)
{
    private const string ChangeEmail = "/api/v1/users/me/email";

    private static Task<HttpResponseMessage> ConfirmChangeAsync(HttpClient client, OutgoingMail mail)
    {
        var link = MailLink.Of(mail);
        return client.PostAsJsonAsync("/api/v1/auth/confirm-email-change",
            new ConfirmEmailChangeRequest(Guid.Parse(link["id"]), link["email"], link["token"]));
    }

    private static async Task<string> EmailOfAsync(HttpClient client, Person person) =>
        (await (await client.SendAsync(person, HttpMethod.Get, "/api/v1/users/me")).Content.ReadFromJsonAsync<MeResponse>())!.Email;

    // A9
    [Fact]
    public async Task On_a_hosted_server_the_new_address_counts_only_after_confirming_and_the_old_one_is_told()
    {
        await using var server = MailServer.Hosted(fixture);
        var anna = await server.ConfirmedPersonAsync();
        var newEmail = NewEmail("anna-neu");

        var change = await server.Client.SendAsync(anna, HttpMethod.Post, ChangeEmail, new ChangeEmailRequest(newEmail, Password));

        Assert.Equal(HttpStatusCode.Accepted, change.StatusCode);
        Assert.Equal(anna.Email, await EmailOfAsync(server.Client, anna));
        var mail = Assert.Single(server.Mails.To(newEmail));
        Assert.Equal("Bitte bestätige deine neue E-Mail-Adresse", mail.Subject);
        Assert.StartsWith("https://duofy.example.de/email-bestaetigen?id=", MailLink.UrlOf(mail).AbsoluteUri);
        Assert.Equal(HttpStatusCode.OK, (await server.Client.LoginAsync(anna.Email)).StatusCode);

        var confirm = await ConfirmChangeAsync(server.Client, mail);

        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await server.Client.LoginAsync(newEmail)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await server.Client.LoginAsync(anna.Email)).StatusCode);
        var notice = server.Mails.To(anna.Email).Last();
        Assert.Equal("Deine E-Mail-Adresse bei Duofy wurde geändert", notice.Subject);
        Assert.Contains(newEmail, notice.Text);
        // Ein zweiter Klick auf denselben Link ändert nichts mehr.
        Assert.Equal(ErrorCodes.TokenInvalid, await (await ConfirmChangeAsync(server.Client, mail)).ReadCodeAsync());
    }

    [Fact]
    public async Task On_a_hosted_server_a_changed_link_or_a_meanwhile_taken_address_is_refused()
    {
        await using var server = MailServer.Hosted(fixture);
        var anna = await server.ConfirmedPersonAsync();
        var newEmail = NewEmail("anna-neu");
        await server.Client.SendAsync(anna, HttpMethod.Post, ChangeEmail, new ChangeEmailRequest(newEmail, Password));
        var link = MailLink.Of(server.Mails.To(newEmail).Single());

        var otherAddress = await server.Client.PostAsJsonAsync("/api/v1/auth/confirm-email-change",
            new ConfirmEmailChangeRequest(Guid.Parse(link["id"]), NewEmail("fremd"), link["token"]));
        (await server.RegisterAsync(newEmail, "Clara")).EnsureSuccessStatusCode();
        var taken = await ConfirmChangeAsync(server.Client, server.Mails.To(newEmail).First());

        Assert.Equal(HttpStatusCode.BadRequest, otherAddress.StatusCode);
        Assert.Equal(ErrorCodes.TokenInvalid, await otherAddress.ReadCodeAsync());
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal(ErrorCodes.EmailTaken, await taken.ReadCodeAsync());
        Assert.Equal(anna.Email, await EmailOfAsync(server.Client, anna));
    }

    // A10
    [Fact]
    public async Task On_a_self_hosted_server_the_address_changes_at_once_with_the_password()
    {
        await using var server = MailServer.SelfHosted(fixture);
        var anna = await server.Client.PersonAsync("Anna");
        var ben = await server.Client.PersonAsync("Ben");
        var newEmail = NewEmail("anna-neu");

        var wrong = await server.Client.SendAsync(anna, HttpMethod.Post, ChangeEmail, new ChangeEmailRequest(newEmail, "falsches-passwort"));
        var taken = await server.Client.SendAsync(anna, HttpMethod.Post, ChangeEmail, new ChangeEmailRequest(ben.Email.ToUpperInvariant(), Password));
        var invalid = await server.Client.SendAsync(anna, HttpMethod.Post, ChangeEmail, new ChangeEmailRequest("keine adresse", Password));

        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal(ErrorCodes.PasswordWrong, await wrong.ReadCodeAsync());
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal(ErrorCodes.EmailTaken, await taken.ReadCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(ErrorCodes.EmailInvalid, await invalid.ReadCodeAsync());

        var change = await server.Client.SendAsync(anna, HttpMethod.Post, ChangeEmail, new ChangeEmailRequest(newEmail, Password));

        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        Assert.Equal(newEmail, (await change.Content.ReadFromJsonAsync<MeResponse>())!.Email);
        Assert.Equal(HttpStatusCode.OK, (await server.Client.LoginAsync(newEmail)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await server.Client.LoginAsync(anna.Email)).StatusCode);
        Assert.Empty(server.Mails.All);
    }

    [Fact]
    public async Task On_a_self_hosted_server_the_change_signs_out_everywhere()
    {
        await using var server = MailServer.SelfHosted(fixture);
        var email = NewEmail();
        (await server.RegisterAsync(email)).EnsureSuccessStatusCode();
        var tokens = await server.TokensAsync(email);

        await server.Client.SendAsync(Authorized(HttpMethod.Post, ChangeEmail, tokens.AccessToken, new ChangeEmailRequest(NewEmail("neu"), Password)));

        Assert.Equal(HttpStatusCode.Unauthorized, (await server.RefreshAsync(tokens.RefreshToken)).StatusCode);
    }
}
