using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Duofy.Schemas;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Duofy.Api.Tests.AdminDriver;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>
/// Einladungen zur Registrierung und der letzte Administrator (Spec 70, R4 bis R6): A6 bis A10. Jeder Test hat seinen
/// eigenen Server, auf dem Anna mit dem Einrichtungscode Administrator wird.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AdminTests(ApiFixture fixture) : IAsyncLifetime
{
    private const string Code = "s3cret-einrichtung";

    private FreshInstance server = null!;
    private Person anna = null!;

    public async Task InitializeAsync()
    {
        server = FreshInstance.Start(fixture, Code, RegistrationModes.Invite);
        anna = await server.PersonAsync("Anna", setupCode: Code);
    }

    public async Task DisposeAsync() => await server.DisposeAsync();

    private HttpClient Client => server.Client;

    private async Task<RegistrationInvitationResponse> InviteAsync(string? email = null)
    {
        var response = await Client.SendAsync(anna, HttpMethod.Post, AdminInvitations, new CreateRegistrationInvitationRequest(email));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RegistrationInvitationResponse>())!;
    }

    private async Task<List<RegistrationInvitationResponse>> ListAsync() =>
        (await (await Client.SendAsync(anna, HttpMethod.Get, AdminInvitations)).Content.ReadFromJsonAsync<List<RegistrationInvitationResponse>>())!;

    // A6
    [Fact]
    public async Task An_invitation_for_an_address_admits_only_that_address_and_only_once()
    {
        var invitation = await InviteAsync("Ben@Example.de");

        var clara = await server.RegisterAsync("Clara", NewEmail("clara"), invitationToken: invitation.Token);
        var ben = await server.RegisterAsync("Ben", "ben@example.de", invitationToken: invitation.Token);
        var again = await server.RegisterAsync("Ben", NewEmail("ben"), invitationToken: invitation.Token);

        Assert.Equal(HttpStatusCode.Forbidden, clara.StatusCode);
        Assert.Equal(ErrorCodes.InvitationInvalid, await clara.ReadCodeAsync());
        Assert.Equal(HttpStatusCode.Created, ben.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, again.StatusCode);
        Assert.Equal(ErrorCodes.InvitationInvalid, await again.ReadCodeAsync());

        // Wer die Einladung benutzt hat, steht an ihr; sie ist nicht mehr offen.
        var stored = await server.Factory.WithDbAsync(db => db.RegistrationInvitations.SingleAsync(i => i.Id == invitation.Id));
        var benId = await server.Factory.WithDbAsync(db => db.Users.Where(u => u.Email == "ben@example.de").Select(u => u.Id).SingleAsync());
        Assert.Equal(benId, stored.UsedById);
        Assert.NotNull(stored.UsedAt);
        Assert.Empty(await ListAsync());
    }

    [Fact]
    public async Task An_unknown_or_expired_invitation_is_invalid()
    {
        var invitation = await InviteAsync();
        await server.Factory.WithDbAsync(db => db.RegistrationInvitations
            .Where(i => i.Id == invitation.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1))));

        var expired = await server.RegisterAsync("Ben", invitationToken: invitation.Token);
        var unknown = await server.RegisterAsync("Ben", invitationToken: "unbekannt");

        Assert.Equal(ErrorCodes.InvitationInvalid, await expired.ReadCodeAsync());
        Assert.Equal(ErrorCodes.InvitationInvalid, await unknown.ReadCodeAsync());
    }

    [Fact]
    public async Task A_failed_registration_leaves_the_invitation_open()
    {
        var invitation = await InviteAsync();

        var taken = await server.RegisterAsync("Anna", anna.Email, invitationToken: invitation.Token);

        Assert.Equal(ErrorCodes.EmailTaken, await taken.ReadCodeAsync());
        Assert.Equal([invitation.Id], (await ListAsync()).Select(i => i.Id));
    }

    // A7
    [Fact]
    public async Task An_invitation_without_address_is_listed_for_seven_days_and_revoking_deletes_it()
    {
        var before = DateTimeOffset.UtcNow;
        var created = await InviteAsync();

        var listed = Assert.Single(await ListAsync());
        Assert.Equal((created.Id, created.Token, (string?)null), (listed.Id, listed.Token, listed.Email));
        Assert.InRange(listed.ExpiresAt, before.AddDays(7).AddSeconds(-5), DateTimeOffset.UtcNow.AddDays(7).AddSeconds(5));
        Assert.Equal($"{DuofyApiFactory.PublicUrl}/registrieren?einladung={created.Token}", listed.Url);

        var revoked = await Client.SendAsync(anna, HttpMethod.Delete, $"{AdminInvitations}/{created.Id}");
        var register = await server.RegisterAsync("Ben", invitationToken: created.Token);

        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        Assert.Empty(await ListAsync());
        Assert.False(await server.Factory.WithDbAsync(db => db.RegistrationInvitations.AnyAsync()));
        Assert.Equal(ErrorCodes.InvitationInvalid, await register.ReadCodeAsync());
    }

    [Fact]
    public async Task The_list_shows_open_invitations_newest_first()
    {
        var first = await InviteAsync();
        var second = await InviteAsync("ben@example.de");

        Assert.Equal([second.Id, first.Id], (await ListAsync()).Select(i => i.Id));
    }

    [Fact]
    public async Task Revoking_an_unknown_invitation_answers_invitation_not_found()
    {
        var response = await Client.SendAsync(anna, HttpMethod.Delete, $"{AdminInvitations}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.InvitationNotFound, await response.ReadCodeAsync());
    }

    [Theory]
    [InlineData("keine-adresse")]
    [InlineData("Ben <ben@example.de>")]
    public async Task An_invalid_address_answers_email_invalid(string email)
    {
        var response = await Client.SendAsync(anna, HttpMethod.Post, AdminInvitations, new CreateRegistrationInvitationRequest(email));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.EmailInvalid, await response.ReadCodeAsync());
    }

    [Fact]
    public async Task Without_a_public_address_no_invitation_is_created()
    {
        var withoutUrl = server.Factory.WithWebHostBuilder(builder => builder.UseSetting("Duofy:PublicUrl", ""));
        using var client = withoutUrl.CreateClient();

        var response = await client.SendAsync(anna, HttpMethod.Post, AdminInvitations, new CreateRegistrationInvitationRequest());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.PublicUrlMissing, await response.ReadCodeAsync());
        Assert.Empty(await ListAsync());
        await withoutUrl.DisposeAsync();
    }

    // A8
    [Fact]
    public async Task Somebody_who_is_not_administrator_gets_admin_required()
    {
        var invitation = await InviteAsync();
        var ben = await server.PersonAsync("Ben", invitationToken: invitation.Token);

        var list = await Client.SendAsync(ben, HttpMethod.Get, AdminInvitations);
        var create = await Client.SendAsync(ben, HttpMethod.Post, AdminInvitations, new CreateRegistrationInvitationRequest());
        var revoke = await Client.SendAsync(ben, HttpMethod.Delete, $"{AdminInvitations}/{Guid.NewGuid()}");

        Assert.All([list, create, revoke], r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
        Assert.Equal(ErrorCodes.AdminRequired, await list.ReadCodeAsync());
        Assert.Equal(ErrorCodes.AdminRequired, await create.ReadCodeAsync());
        Assert.Equal(ErrorCodes.AdminRequired, await revoke.ReadCodeAsync());
    }

    [Fact]
    public async Task Without_sign_in_the_admin_endpoints_answer_401()
    {
        var response = await Client.GetAsync(AdminInvitations);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // A9
    [Fact]
    public async Task The_only_administrator_cannot_delete_the_account()
    {
        var response = await Client.SendAsync(anna, HttpMethod.Delete, "/api/v1/users/me", new DeleteMeRequest(Password));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.LastAdmin, await response.ReadCodeAsync());
        Assert.True(await server.Factory.WithDbAsync(db => db.Users.AnyAsync(u => u.Id == anna.Id)));
    }

    [Fact]
    public async Task With_a_wrong_password_the_administrator_gets_password_wrong_first()
    {
        var response = await Client.SendAsync(anna, HttpMethod.Delete, "/api/v1/users/me", new DeleteMeRequest("falsch-falsch"));

        Assert.Equal(ErrorCodes.PasswordWrong, await response.ReadCodeAsync());
    }

    [Fact]
    public async Task A_second_administrator_lets_the_first_one_go()
    {
        // R9 bietet keinen Weg zu einem zweiten Administrator; die Regel zählt trotzdem nur, ob noch jemand bleibt.
        var invitation = await InviteAsync();
        var ben = await server.PersonAsync("Ben", invitationToken: invitation.Token);
        await server.Factory.WithDbAsync(db => db.Users.Where(u => u.Id == ben.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.IsInstanceAdmin, true)));

        var response = await Client.SendAsync(anna, HttpMethod.Delete, "/api/v1/users/me", new DeleteMeRequest(Password));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Somebody_who_is_not_administrator_deletes_as_before()
    {
        var invitation = await InviteAsync();
        var ben = await server.PersonAsync("Ben", invitationToken: invitation.Token);

        var response = await Client.SendAsync(ben, HttpMethod.Delete, "/api/v1/users/me", new DeleteMeRequest(Password));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // A10
    [Fact]
    public async Task No_admin_endpoint_answers_data_of_plans_bookings_accounts_or_contracts()
    {
        var invitation = await InviteAsync("ben@example.de");
        var requests = new Dictionary<string, (HttpMethod Method, string Path, object? Body)>
        {
            ["GET /api/v1/admin/invitations"] = (HttpMethod.Get, AdminInvitations, null),
            ["POST /api/v1/admin/invitations"] = (HttpMethod.Post, AdminInvitations, new CreateRegistrationInvitationRequest()),
            ["DELETE /api/v1/admin/invitations/{id:guid}"] = (HttpMethod.Delete, $"{AdminInvitations}/{invitation.Id}", null),
            // Spec 53, R4: nur Link und Ablaufzeit, nichts aus dem Konto der Person.
            ["POST /api/v1/admin/password-reset-links"] = (HttpMethod.Post, "/api/v1/admin/password-reset-links", new CreatePasswordResetLinkRequest(anna.Email)),
        };

        // Jeder Endpunkt unter /admin ist im Rundgang; ein neuer lässt den Test scheitern, bis er hier steht.
        Assert.Equal(requests.Keys.Order(), AdminEndpoints());

        var allowed = new HashSet<string> { "id", "token", "email", "expiresAt", "url" };
        var forbidden = FieldsOf(typeof(AccountResponse), typeof(BookingResponse), typeof(CommitmentResponse), typeof(PlanResponse));
        forbidden.Remove("id");
        foreach (var (endpoint, (method, path, body)) in requests)
        {
            var response = await Client.SendAsync(anna, method, path, body);
            Assert.True(response.IsSuccessStatusCode, $"{endpoint}: {response.StatusCode}");
            var names = PropertyNames(await response.Content.ReadAsStringAsync());
            Assert.True(names.IsSubsetOf(allowed), $"{endpoint} antwortet mit {string.Join(", ", names.Except(allowed))}");
            Assert.Empty(names.Intersect(forbidden));
        }
    }

    private List<string> AdminEndpoints() =>
        server.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText is { } raw && raw.StartsWith("/api/v1/admin", StringComparison.Ordinal))
            .SelectMany(e => (e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? []).Select(m => $"{m} {e.RoutePattern.RawText!.TrimEnd('/')}"))
            .Order()
            .ToList();

    /// <summary>Die JSON-Namen der Felder dieser Antworten, so wie die API sie schreibt.</summary>
    private static HashSet<string> FieldsOf(params Type[] types) =>
        types.SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name))
            .ToHashSet();

    /// <summary>Alle Namen von Feldern in der Antwort, in jeder Tiefe; eine leere Antwort hat keine.</summary>
    private static HashSet<string> PropertyNames(string json)
    {
        var names = new HashSet<string>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return names;
        }
        void Walk(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        names.Add(property.Name);
                        Walk(property.Value);
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        Walk(item);
                    }
                    break;
            }
        }
        using var document = JsonDocument.Parse(json);
        Walk(document.RootElement);
        return names;
    }
}
