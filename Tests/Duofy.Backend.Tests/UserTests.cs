using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using Duofy.Domain;
using Microsoft.EntityFrameworkCore;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>Das eigene Konto: Namen, Passwort, Löschen (R5, R6).</summary>
[Collection(ApiCollection.Name)]
public class UserTests
{
    private const string Me = "/api/v1/users/me";

    private readonly ApiFixture fixture;
    private readonly HttpClient client;

    public UserTests(ApiFixture fixture)
    {
        this.fixture = fixture;
        client = fixture.Factory.CreateClient();
    }

    private static Commitment Rent(Guid owner) => new()
    {
        OwnerId = owner,
        Name = "Miete",
        Amount = 500m,
        Type = CommitmentType.Contract,
        IntervalMonths = 1,
        FirstDueDate = new DateOnly(2026, 1, 1),
        // Jeder Vertrag hat eine Kategorie; die Datenbank prüft, dass es sie gibt.
        CategoryId = CategoryCatalog.IdFor("housing.rent"),
        Pot = Pot.Needs,
    };

    // A11
    [Fact]
    public async Task Deleting_the_account_removes_the_user_and_their_commitments()
    {
        var (email, tokens) = await client.SignUpAsync();
        var id = (await (await client.SendAsync(Authorized(HttpMethod.Get, Me, tokens.AccessToken)))
            .Content.ReadFromJsonAsync<MeResponse>())!.Id;
        await fixture.Factory.WithDbAsync(db =>
        {
            db.Commitments.AddRange(Rent(id), Rent(id));
            return db.SaveChangesAsync();
        });

        var delete = await client.SendAsync(Authorized(HttpMethod.Delete, Me, tokens.AccessToken, new DeleteMeRequest(Password)));

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.LoginAsync(email)).StatusCode);
        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        var remaining = await fixture.Factory.WithDbAsync(db => db.Commitments.CountAsync(c => c.OwnerId == id));
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task Deleting_one_account_keeps_the_commitments_of_others()
    {
        var (_, anna) = await client.SignUpAsync();
        var (_, ben) = await client.SignUpAsync();
        var benId = (await (await client.SendAsync(Authorized(HttpMethod.Get, Me, ben.AccessToken)))
            .Content.ReadFromJsonAsync<MeResponse>())!.Id;
        await fixture.Factory.WithDbAsync(db =>
        {
            db.Commitments.Add(Rent(benId));
            return db.SaveChangesAsync();
        });

        await client.SendAsync(Authorized(HttpMethod.Delete, Me, anna.AccessToken, new DeleteMeRequest(Password)));

        Assert.Equal(1, await fixture.Factory.WithDbAsync(db => db.Commitments.CountAsync(c => c.OwnerId == benId)));
    }

    // A12
    [Fact]
    public async Task Deleting_with_a_wrong_password_keeps_the_account()
    {
        var (email, tokens) = await client.SignUpAsync();

        var delete = await client.SendAsync(Authorized(HttpMethod.Delete, Me, tokens.AccessToken, new DeleteMeRequest("falsch-falsch")));

        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);
        Assert.Equal(ErrorCodes.PasswordWrong, await delete.ReadCodeAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.LoginAsync(email)).StatusCode);
    }

    // A14
    [Fact]
    public async Task Patch_sets_new_names()
    {
        var (_, tokens) = await client.SignUpAsync();

        var patch = await client.SendAsync(Authorized(HttpMethod.Patch, Me, tokens.AccessToken, new UpdateMeRequest(" Annika ", "Muster")));

        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var body = (await patch.Content.ReadFromJsonAsync<MeResponse>())!;
        Assert.Equal(("Annika", "Muster"), (body.FirstName, body.LastName));
        var me = (await (await client.SendAsync(Authorized(HttpMethod.Get, Me, tokens.AccessToken)))
            .Content.ReadFromJsonAsync<MeResponse>())!;
        Assert.Equal(("Annika", "Muster"), (me.FirstName, me.LastName));
    }

    // A14
    [Theory]
    [InlineData("", "Muster", ErrorCodes.FirstNameRequired)]
    [InlineData("Annika", " ", ErrorCodes.LastNameRequired)]
    public async Task Patch_with_an_empty_name_keeps_the_old_names(string firstName, string lastName, string code)
    {
        var (_, tokens) = await client.SignUpAsync();

        var patch = await client.SendAsync(Authorized(HttpMethod.Patch, Me, tokens.AccessToken, new UpdateMeRequest(firstName, lastName)));

        Assert.Equal(HttpStatusCode.BadRequest, patch.StatusCode);
        Assert.Equal(code, await patch.ReadCodeAsync());
        var me = (await (await client.SendAsync(Authorized(HttpMethod.Get, Me, tokens.AccessToken)))
            .Content.ReadFromJsonAsync<MeResponse>())!;
        Assert.Equal(("Anna", "Beispiel"), (me.FirstName, me.LastName));
    }

    // A15
    [Fact]
    public async Task Changed_password_works_and_the_old_one_does_not()
    {
        var (email, tokens) = await client.SignUpAsync();
        const string newPassword = "neues-passwort";

        var change = await client.SendAsync(Authorized(HttpMethod.Post, $"{Me}/password", tokens.AccessToken,
            new ChangePasswordRequest(Password, newPassword)));

        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.LoginAsync(email, newPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.LoginAsync(email, Password)).StatusCode);
    }

    [Theory]
    [InlineData("falsch-falsch", "neues-passwort", ErrorCodes.PasswordWrong)]
    [InlineData(Password, "kurz", ErrorCodes.PasswordTooShort)]
    public async Task Password_change_is_refused_with_its_code(string current, string next, string code)
    {
        var (email, tokens) = await client.SignUpAsync();

        var change = await client.SendAsync(Authorized(HttpMethod.Post, $"{Me}/password", tokens.AccessToken,
            new ChangePasswordRequest(current, next)));

        Assert.Equal(HttpStatusCode.BadRequest, change.StatusCode);
        Assert.Equal(code, await change.ReadCodeAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.LoginAsync(email)).StatusCode);
    }

    // Wer ein Access-Token gestohlen hat, soll das Passwort nicht über diese
    // Endpunkte erraten können: Fehlversuche zählen zur Sperre wie bei der Anmeldung.
    [Theory]
    [InlineData("delete")]
    [InlineData("password")]
    public async Task Wrong_passwords_on_account_endpoints_lock_the_account(string endpoint)
    {
        var (email, tokens) = await client.SignUpAsync();
        HttpRequestMessage WrongAttempt() => endpoint == "delete"
            ? Authorized(HttpMethod.Delete, Me, tokens.AccessToken, new DeleteMeRequest("falsch-falsch"))
            : Authorized(HttpMethod.Post, $"{Me}/password", tokens.AccessToken, new ChangePasswordRequest("falsch-falsch", "neues-passwort"));
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(WrongAttempt())).StatusCode);
        }

        var login = await client.LoginAsync(email);
        var rightPassword = await client.SendAsync(Authorized(HttpMethod.Delete, Me, tokens.AccessToken, new DeleteMeRequest(Password)));

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, rightPassword.StatusCode);
        Assert.Equal(ErrorCodes.PasswordWrong, await rightPassword.ReadCodeAsync());
    }

    [Fact]
    public async Task User_endpoints_need_a_token()
    {
        var patch = await client.PatchAsJsonAsync(Me, new UpdateMeRequest("A", "B"));
        var password = await client.PostAsJsonAsync($"{Me}/password", new ChangePasswordRequest("a", "b"));
        var delete = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, Me)
        {
            Content = JsonContent.Create(new DeleteMeRequest("a")),
        });

        Assert.All([patch, password, delete], r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
    }
}
