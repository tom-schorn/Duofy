using System.Net.Http.Json;
using Duofy.Schemas;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>Eine registrierte und angemeldete Person für die Tests des Haushalts.</summary>
public sealed record Person(Guid Id, string Email, string FirstName, string LastName, string Token);

/// <summary>Kurze Wege für die Tests des Haushalts (Spec 67).</summary>
public static class HouseholdDriver
{
    public const string Households = "/api/v1/households";

    public const string Mine = "/api/v1/households/mine";

    public static string Household(Guid id) => $"/api/v1/households/{id}";

    public static string Invitations(Guid householdId) => $"/api/v1/households/{householdId}/invitations";

    public static string Members(Guid householdId, string userId) => $"/api/v1/households/{householdId}/members/{userId}";

    public static string Invitation(string token) => $"/api/v1/invitations/{token}";

    public static string Grants(Guid householdId, Guid granteeId) => $"/api/v1/households/{householdId}/grants/{granteeId}";

    /// <summary>Registriert eine Person mit diesen Namen und meldet sie an.</summary>
    public static async Task<Person> PersonAsync(
        this HttpClient client, string firstName, string lastName = "Muster", DuofyApiFactory? withoutFirstAccount = null)
    {
        var email = NewEmail(firstName.ToLowerInvariant());
        (await client.RegisterAsync(new RegisterRequest(firstName, lastName, email, Password, AcceptedTerms: true)))
            .EnsureSuccessStatusCode();
        if (withoutFirstAccount is not null)
        {
            await withoutFirstAccount.RemoveFirstAccountAsync(email);
        }
        var login = await client.LoginAsync(email);
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken;
        var me = await client.SendAsync(Authorized(HttpMethod.Get, "/api/v1/users/me", token));
        var id = (await me.Content.ReadFromJsonAsync<MeResponse>())!.Id;
        return new Person(id, email, firstName, lastName, token);
    }

    public static Task<HttpResponseMessage> SendAsync(this HttpClient client, Person person, HttpMethod method, string path, object? body = null) =>
        client.SendAsync(Authorized(method, path, person.Token, body));

    /// <summary>Gründet als <paramref name="person"/> einen Haushalt, ohne Namen mit dem vorgegebenen.</summary>
    public static async Task<HouseholdResponse> FoundAsync(this HttpClient client, Person person, string? name = null)
    {
        var response = await client.SendAsync(person, HttpMethod.Post, Households, new CreateHouseholdRequest(name));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<HouseholdResponse>())!;
    }

    /// <summary>Registriert eine Person und lässt sie gleich einen Haushalt gründen.</summary>
    public static async Task<Person> FounderAsync(
        this HttpClient client, string firstName, string lastName = "Muster", DuofyApiFactory? withoutFirstAccount = null)
    {
        var person = await client.PersonAsync(firstName, lastName, withoutFirstAccount);
        await client.FoundAsync(person);
        return person;
    }

    /// <summary>True, wenn die Person in keinem Haushalt ist: GET mine antwortet 404 no_household.</summary>
    public static async Task<bool> HasNoHouseholdAsync(this HttpClient client, Person person)
    {
        var response = await client.SendAsync(person, HttpMethod.Get, Mine);
        return response.StatusCode == System.Net.HttpStatusCode.NotFound && await response.ReadCodeAsync() == ErrorCodes.NoHousehold;
    }

    public static async Task<HouseholdResponse> MineAsync(this HttpClient client, Person person)
    {
        var response = await client.SendAsync(person, HttpMethod.Get, Mine);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<HouseholdResponse>())!;
    }

    /// <summary>Vergleicht zwei Antworten samt Mitgliedern (die Liste selbst vergleicht ein Record nur als Verweis).</summary>
    public static void SameHousehold(HouseholdResponse expected, HouseholdResponse actual)
    {
        Assert.Equal(expected with { Members = [] }, actual with { Members = [] });
        Assert.Equal(expected.Members, actual.Members);
    }

    /// <summary>Legt als Gründer eine Einladung an.</summary>
    public static async Task<InvitationResponse> InviteAsync(this HttpClient client, Person founder)
    {
        var household = await client.MineAsync(founder);
        var response = await client.SendAsync(founder, HttpMethod.Post, Invitations(household.Id));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InvitationResponse>())!;
    }

    /// <summary>Setzt als <paramref name="granter"/>, was <paramref name="grantee"/> darf (Spec 68), im Haushalt des Gebers.</summary>
    public static async Task<MemberResponse> GrantAsync(this HttpClient client, Person granter, Person grantee, UpdateGrantsRequest levels)
    {
        var household = await client.MineAsync(granter);
        var response = await client.SendAsync(granter, HttpMethod.Put, Grants(household.Id, grantee.Id), levels);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MemberResponse>())!;
    }

    /// <summary>Anna als Gründerin und Ben als Mitglied in ihrem Haushalt.</summary>
    public static async Task<(Person Anna, Person Ben)> AnnaAndBenAsync(
        this HttpClient client, DuofyApiFactory? withoutFirstAccount = null)
    {
        var anna = await client.FounderAsync("Anna", withoutFirstAccount: withoutFirstAccount);
        var ben = await client.PersonAsync("Ben", withoutFirstAccount: withoutFirstAccount);
        await client.JoinAsync(anna, ben);
        return (anna, ben);
    }

    /// <summary>Holt <paramref name="joiner"/> mit einer Einladung in den Haushalt von <paramref name="founder"/>.</summary>
    public static async Task JoinAsync(this HttpClient client, Person founder, Person joiner)
    {
        var invitation = await client.InviteAsync(founder);
        (await client.SendAsync(joiner, HttpMethod.Post, Invitation(invitation.Token) + "/accept")).EnsureSuccessStatusCode();
    }
}
