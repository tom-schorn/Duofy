using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>
/// "Passwort vergessen" auf einem selbst betriebenen Server (Spec 53, R4): Der Administrator legt einen Link an. A7.
/// Jeder Test hat seinen eigenen Server, auf dem Tom mit dem Einrichtungscode Administrator ist.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ResetLinkTests(ApiFixture fixture) : IAsyncLifetime
{
    private const string Code = "s3cret-einrichtung";
    private const string Links = "/api/v1/admin/password-reset-links";
    private const string NewPassword = "ganz-neues-passwort";

    private FreshInstance server = null!;
    private Person tom = null!;
    private Person anna = null!;

    public async Task InitializeAsync()
    {
        server = FreshInstance.Start(fixture, Code, RegistrationModes.Open);
        tom = await server.PersonAsync("Tom", setupCode: Code);
        anna = await server.PersonAsync("Anna");
    }

    public async Task DisposeAsync() => await server.DisposeAsync();

    private HttpClient Client => server.Client;

    private async Task<PasswordResetLinkResponse> LinkForAsync(string email)
    {
        var response = await Client.SendAsync(tom, HttpMethod.Post, Links, new CreatePasswordResetLinkRequest(email));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PasswordResetLinkResponse>())!;
    }

    private static string TokenOf(PasswordResetLinkResponse link) => QueryHelpers.ParseQuery(new Uri(link.Url).Query)["link"].ToString();

    private Task<HttpResponseMessage> ResetAsync(string token, string password = NewPassword) =>
        Client.PostAsJsonAsync("/api/v1/auth/password/reset-by-link", new ResetPasswordByLinkRequest(token, password));

    // A7
    [Fact]
    public async Task The_administrator_gets_a_link_valid_24_hours_that_sets_a_new_password_once()
    {
        var login = await Client.LoginAsync(anna.Email);
        var oldRefresh = (await login.Content.ReadFromJsonAsync<TokenResponse>())!.RefreshToken;
        var before = DateTimeOffset.UtcNow;

        var link = await LinkForAsync(anna.Email.ToUpperInvariant());

        Assert.StartsWith("https://duofy.example.de/passwort-neu?link=", link.Url);
        Assert.InRange(link.ExpiresAt, before.AddHours(24).AddSeconds(-5), DateTimeOffset.UtcNow.AddHours(24).AddSeconds(5));

        var first = await ResetAsync(TokenOf(link));
        var second = await ResetAsync(TokenOf(link), "noch-ein-passwort");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.LoginAsync(anna.Email, NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(oldRefresh))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(ErrorCodes.TokenInvalid, await second.ReadCodeAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.LoginAsync(anna.Email, "noch-ein-passwort")).StatusCode);
    }

    // A7
    [Fact]
    public async Task An_unknown_address_is_named_only_to_the_administrator()
    {
        var unknown = await Client.SendAsync(tom, HttpMethod.Post, Links, new CreatePasswordResetLinkRequest(NewEmail("niemand")));
        var byAnna = await Client.SendAsync(anna, HttpMethod.Post, Links, new CreatePasswordResetLinkRequest(tom.Email));

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(ErrorCodes.UserNotFound, await unknown.ReadCodeAsync());
        Assert.Equal(HttpStatusCode.Forbidden, byAnna.StatusCode);
        Assert.Equal(ErrorCodes.AdminRequired, await byAnna.ReadCodeAsync());
    }

    // A13: Ein neuer Link macht die älteren offenen Links derselben Person ungültig, die einer anderen nicht.
    [Fact]
    public async Task Creating_a_new_link_makes_the_older_open_links_of_that_person_invalid()
    {
        var ben = await server.PersonAsync("Ben");
        var a = await LinkForAsync(anna.Email);
        var forBen = await LinkForAsync(ben.Email);
        var b = await LinkForAsync(anna.Email);

        var withA = await ResetAsync(TokenOf(a));
        var withB = await ResetAsync(TokenOf(b));
        var withBens = await ResetAsync(TokenOf(forBen));

        Assert.Equal(HttpStatusCode.BadRequest, withA.StatusCode);
        Assert.Equal(ErrorCodes.TokenInvalid, await withA.ReadCodeAsync());
        Assert.Equal(HttpStatusCode.NoContent, withB.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, withBens.StatusCode);
    }

    // A13: Wer mit B ein Passwort setzt, macht A ungültig, auch wenn beide offen waren (etwa aus der Zeit vor dieser Regel).
    [Fact]
    public async Task Setting_a_password_with_one_link_makes_the_other_open_links_of_that_person_invalid()
    {
        var a = await LinkForAsync(anna.Email);
        var b = await LinkForAsync(anna.Email);
        var hashOfA = Duofy.Domain.PasswordResetLink.Hash(TokenOf(a));
        await server.Factory.WithDbAsync(db => db.PasswordResetLinks.Where(l => l.TokenHash == hashOfA)
            .ExecuteUpdateAsync(set => set.SetProperty(l => l.UsedAt, (DateTimeOffset?)null)));

        var withB = await ResetAsync(TokenOf(b));
        var withA = await ResetAsync(TokenOf(a), "noch-ein-passwort");

        Assert.Equal(HttpStatusCode.NoContent, withB.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, withA.StatusCode);
        Assert.Equal(ErrorCodes.TokenInvalid, await withA.ReadCodeAsync());
        Assert.Equal(HttpStatusCode.OK, (await Client.LoginAsync(anna.Email, NewPassword)).StatusCode);
    }

    // A13: Zwei offene Links zur selben Zeit benutzt: Genau einer setzt das Passwort, der andere ist ungültig.
    [Fact]
    public async Task Two_open_links_used_at_the_same_time_set_the_password_once()
    {
        var a = await LinkForAsync(anna.Email);
        var b = await LinkForAsync(anna.Email);
        await server.Factory.WithDbAsync(db => db.PasswordResetLinks
            .ExecuteUpdateAsync(set => set.SetProperty(l => l.UsedAt, (DateTimeOffset?)null)));

        var answers = await Task.WhenAll(ResetAsync(TokenOf(a)), ResetAsync(TokenOf(b)));

        Assert.Equal(
            [HttpStatusCode.NoContent, HttpStatusCode.BadRequest],
            answers.Select(r => r.StatusCode).Order().ToArray());
    }

    [Fact]
    public async Task An_expired_or_unknown_link_is_invalid_and_the_password_stays()
    {
        var link = await LinkForAsync(anna.Email);
        await server.Factory.WithDbAsync(db => db.PasswordResetLinks
            .ExecuteUpdateAsync(set => set.SetProperty(l => l.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1))));

        var expired = await ResetAsync(TokenOf(link));
        var unknown = await ResetAsync("gibt-es-nicht");

        Assert.Equal(ErrorCodes.TokenInvalid, await expired.ReadCodeAsync());
        Assert.Equal(ErrorCodes.TokenInvalid, await unknown.ReadCodeAsync());
        Assert.Equal(HttpStatusCode.OK, (await Client.LoginAsync(anna.Email)).StatusCode);
    }

    [Fact]
    public async Task The_database_keeps_only_the_hash_and_who_created_and_used_the_link()
    {
        var link = await LinkForAsync(anna.Email);
        await ResetAsync(TokenOf(link));

        var stored = await server.Factory.WithDbAsync(db => db.PasswordResetLinks.SingleAsync());

        Assert.Equal((anna.Id, tom.Id), (stored.UserId, stored.CreatedById));
        Assert.Equal(Duofy.Domain.PasswordResetLink.Hash(TokenOf(link)), stored.TokenHash);
        Assert.NotNull(stored.UsedAt);
    }

    [Fact]
    public async Task A_reset_link_needs_a_valid_address_and_a_long_enough_password()
    {
        var invalid = await Client.SendAsync(tom, HttpMethod.Post, Links, new CreatePasswordResetLinkRequest("keine adresse"));
        var link = await LinkForAsync(anna.Email);
        var tooShort = await ResetAsync(TokenOf(link), "kurz");

        Assert.Equal(ErrorCodes.EmailInvalid, await invalid.ReadCodeAsync());
        Assert.Equal(ErrorCodes.PasswordTooShort, await tooShort.ReadCodeAsync());
        // Ein abgelehntes Passwort verbraucht den Link nicht.
        Assert.Equal(HttpStatusCode.NoContent, (await ResetAsync(TokenOf(link))).StatusCode);
    }

    [Fact]
    public async Task A_hosted_server_has_no_reset_links_of_the_administrator()
    {
        await using var hosted = MailServer.Hosted(fixture);

        var reset = await hosted.Client.PostAsJsonAsync("/api/v1/auth/password/reset-by-link", new ResetPasswordByLinkRequest("x", NewPassword));
        var create = await hosted.Client.SendAsync(Authorized(HttpMethod.Post, Links, "kein-token", new CreatePasswordResetLinkRequest(anna.Email)));

        Assert.Equal(HttpStatusCode.NotFound, reset.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
    }
}
