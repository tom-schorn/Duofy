using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using Duofy.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using static Duofy.Api.Tests.HouseholdDriver;

namespace Duofy.Api.Tests;

/// <summary>Einladungen als Link: anlegen, auflisten, zurückziehen, Vorschau und Beitreten (Spec 67, R4 bis R6).</summary>
[Collection(ApiCollection.Name)]
public class InvitationTests
{
    private readonly ApiFixture fixture;
    private readonly HttpClient client;

    public InvitationTests(ApiFixture fixture)
    {
        this.fixture = fixture;
        client = fixture.Factory.CreateClient();
    }

    private async Task<List<InvitationResponse>> OpenInvitationsAsync(Person founder, Guid householdId)
    {
        var response = await client.SendAsync(founder, HttpMethod.Get, Invitations(householdId));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<InvitationResponse>>())!;
    }

    // A3
    [Fact]
    public async Task The_founder_creates_lists_and_revokes_an_invitation()
    {
        var anna = await client.PersonAsync("Anna");
        var household = await client.FoundAsync(anna);
        var before = DateTimeOffset.UtcNow;

        var create = await client.SendAsync(anna, HttpMethod.Post, Invitations(household.Id));

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var invitation = (await create.Content.ReadFromJsonAsync<InvitationResponse>())!;
        Assert.True(invitation.Token.Length >= 43, invitation.Token);
        Assert.InRange(invitation.ExpiresAt, before.AddDays(14).AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(14).AddMinutes(1));
        Assert.Equal(invitation, Assert.Single(await OpenInvitationsAsync(anna, household.Id)));

        var revoke = await client.SendAsync(anna, HttpMethod.Delete, $"{Invitations(household.Id)}/{invitation.Id}");

        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        Assert.Empty(await OpenInvitationsAsync(anna, household.Id));
        var preview = await client.GetAsync(Invitation(invitation.Token));
        Assert.Equal(HttpStatusCode.Conflict, preview.StatusCode);
        Assert.Equal(ErrorCodes.InvitationNotOpen, await preview.ReadCodeAsync());
    }

    // A19: Die App ist über das Heimnetz verbunden, der Link zeigt trotzdem auf die öffentliche Adresse.
    [Fact]
    public async Task The_link_uses_the_public_address_and_not_the_one_the_request_came_through()
    {
        var lan = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://192.168.2.67:5080/") });
        var anna = await lan.PersonAsync("Anna");
        var household = await lan.FoundAsync(anna);

        var create = await lan.SendAsync(anna, HttpMethod.Post, Invitations(household.Id));

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var invitation = (await create.Content.ReadFromJsonAsync<InvitationResponse>())!;
        Assert.Equal($"https://duofy.example.de/einladung/{invitation.Token}", invitation.Url);
        Assert.Equal(invitation.Url, Assert.Single(await OpenInvitationsAsync(anna, household.Id)).Url);
    }

    // A19: Ein Schrägstrich am Ende der Adresse gibt keinen doppelten im Link.
    [Fact]
    public async Task A_public_address_with_a_slash_at_the_end_gives_the_same_link()
    {
        using var api = WithPublicUrl("https://duofy.example.de/");
        var http = api.CreateClient();
        var anna = await http.FounderAsync("Anna");

        var invitation = await http.InviteAsync(anna);

        Assert.Equal($"https://duofy.example.de/einladung/{invitation.Token}", invitation.Url);
    }

    // A19: Ohne öffentliche Adresse, oder mit einer, die keine ist, entsteht keine Einladung.
    [Theory]
    [InlineData("")]
    [InlineData("duofy.example.de")]
    [InlineData("ftp://duofy.example.de")]
    public async Task Without_a_public_address_no_invitation_is_created(string publicUrl)
    {
        using var api = WithPublicUrl(publicUrl);
        var http = api.CreateClient();
        var anna = await http.PersonAsync("Anna");
        var household = await http.FoundAsync(anna);

        var create = await http.SendAsync(anna, HttpMethod.Post, Invitations(household.Id));

        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.PublicUrlMissing), (create.StatusCode, await create.ReadCodeAsync()));
        Assert.Equal(0, await fixture.Factory.WithDbAsync(db => db.HouseholdInvitations.CountAsync(i => i.HouseholdId == household.Id)));
    }

    private WebApplicationFactory<Program> WithPublicUrl(string publicUrl) =>
        fixture.Factory.WithWebHostBuilder(builder => builder.UseSetting("Duofy:PublicUrl", publicUrl));

    // A4
    [Fact]
    public async Task A_member_may_neither_create_nor_list_nor_revoke_invitations()
    {
        var anna = await client.FounderAsync("Anna");
        var ben = await client.PersonAsync("Ben");
        await client.JoinAsync(anna, ben);
        var household = await client.MineAsync(anna);
        var open = await client.InviteAsync(anna);
        var countBefore = await fixture.Factory.WithDbAsync(db => db.HouseholdInvitations.CountAsync(i => i.HouseholdId == household.Id));

        var create = await client.SendAsync(ben, HttpMethod.Post, Invitations(household.Id));
        var list = await client.SendAsync(ben, HttpMethod.Get, Invitations(household.Id));
        var revoke = await client.SendAsync(ben, HttpMethod.Delete, $"{Invitations(household.Id)}/{open.Id}");

        foreach (var answer in new[] { create, list, revoke })
        {
            Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NotHouseholdFounder), (answer.StatusCode, await answer.ReadCodeAsync()));
        }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Invitation(open.Token))).StatusCode);
        Assert.Equal(countBefore, await fixture.Factory.WithDbAsync(db => db.HouseholdInvitations.CountAsync(i => i.HouseholdId == household.Id)));
    }

    // A5
    [Fact]
    public async Task The_eleventh_open_invitation_is_refused()
    {
        var anna = await client.PersonAsync("Anna");
        var household = await client.FoundAsync(anna);
        for (var i = 0; i < 10; i++)
        {
            (await client.SendAsync(anna, HttpMethod.Post, Invitations(household.Id))).EnsureSuccessStatusCode();
        }

        var eleventh = await client.SendAsync(anna, HttpMethod.Post, Invitations(household.Id));

        Assert.Equal(HttpStatusCode.Conflict, eleventh.StatusCode);
        Assert.Equal(ErrorCodes.TooManyInvitations, await eleventh.ReadCodeAsync());
        Assert.Equal(10, (await OpenInvitationsAsync(anna, household.Id)).Count);
    }

    // A5: Abgelaufene Einladungen zählen nicht mehr mit und stehen nicht mehr in der Liste.
    [Fact]
    public async Task Expired_invitations_neither_count_nor_show()
    {
        var anna = await client.PersonAsync("Anna");
        var household = await client.FoundAsync(anna);
        for (var i = 0; i < 10; i++)
        {
            (await client.SendAsync(anna, HttpMethod.Post, Invitations(household.Id))).EnsureSuccessStatusCode();
        }
        await ExpireAllAsync(household.Id);

        var next = await client.SendAsync(anna, HttpMethod.Post, Invitations(household.Id));

        Assert.Equal(HttpStatusCode.Created, next.StatusCode);
        Assert.Single(await OpenInvitationsAsync(anna, household.Id));
    }

    // A6
    [Fact]
    public async Task The_preview_without_sign_in_shows_only_household_inviter_and_expiry()
    {
        var anna = await client.FounderAsync("Anna", "Muster");
        var invitation = await client.InviteAsync(anna);

        var response = await client.GetAsync(Invitation(invitation.Token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var preview = (await response.Content.ReadFromJsonAsync<InvitationPreview>())!;
        Assert.Equal(new InvitationPreview("Haushalt von Anna", "Anna Muster", invitation.ExpiresAt), preview);
        Assert.DoesNotContain(anna.Email, body);
        Assert.DoesNotContain("members", body, StringComparison.OrdinalIgnoreCase);
    }

    // A7
    [Fact]
    public async Task Accepting_moves_ben_into_annas_household_and_uses_the_invitation_up()
    {
        var anna = await client.FounderAsync("Anna");
        var ben = await client.PersonAsync("Ben");
        var clara = await client.PersonAsync("Clara");
        var bensAccount = await AddAccountAsync(ben);
        var contract = $"Handy {ben.Id:N}";
        (await client.SendAsync(ben, HttpMethod.Post, "/api/v1/commitments",
            new CreateCommitmentRequest(CommitmentTypes.Contract, contract, 20.00m, 1, new DateOnly(2026, 1, 1), null,
                CategoryCatalog.IdFor("household.groceries"), PotKeys.Needs, PaymentMethod: PaymentMethods.Other))).EnsureSuccessStatusCode();
        var invitation = await client.InviteAsync(anna);

        var accept = await client.SendAsync(ben, HttpMethod.Post, Invitation(invitation.Token) + "/accept");

        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        var joined = (await accept.Content.ReadFromJsonAsync<HouseholdResponse>())!;
        var mine = await client.MineAsync(ben);
        SameHousehold(joined, mine);
        Assert.Equal("Haushalt von Anna", mine.Name);
        Assert.Equal(
            [(anna.Id, HouseholdRoles.Founder, false), (ben.Id, HouseholdRoles.Member, true)],
            mine.Members.Select(m => (m.UserId, m.Role, m.IsMe)));
        Assert.Equal(ben.Id, await fixture.Factory.WithDbAsync(db => db.Accounts.Where(a => a.Id == bensAccount).Select(a => a.OwnerId).SingleAsync()));
        Assert.Equal(ben.Id, await fixture.Factory.WithDbAsync(db => db.Commitments.Where(c => c.Name == contract).Select(c => c.OwnerId).SingleAsync()));
        var used = await fixture.Factory.WithDbAsync(db => db.HouseholdInvitations.AsNoTracking().SingleAsync(i => i.Id == invitation.Id));
        Assert.Equal((InvitationStatus.Accepted, ben.Id), (used.Status, used.UsedById));
        Assert.NotNull(used.UsedAt);

        var second = await client.SendAsync(clara, HttpMethod.Post, Invitation(invitation.Token) + "/accept");

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(ErrorCodes.InvitationNotOpen, await second.ReadCodeAsync());
        Assert.True(await client.HasNoHouseholdAsync(clara));
    }

    // A8: Ben hat selbst einen Haushalt gegründet.
    [Fact]
    public async Task A_founder_of_an_own_household_cannot_accept()
    {
        var anna = await client.FounderAsync("Anna");
        var ben = await client.FounderAsync("Ben");
        var invitation = await client.InviteAsync(anna);
        var bensBefore = await client.MineAsync(ben);

        var accept = await client.SendAsync(ben, HttpMethod.Post, Invitation(invitation.Token) + "/accept");

        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.AlreadyInHousehold), (accept.StatusCode, await accept.ReadCodeAsync()));
        SameHousehold(bensBefore, await client.MineAsync(ben));
        Assert.Single((await client.MineAsync(anna)).Members);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Invitation(invitation.Token))).StatusCode);
    }

    // A8: Ben ist Mitglied bei Dora.
    [Fact]
    public async Task A_member_elsewhere_cannot_accept()
    {
        var anna = await client.FounderAsync("Anna");
        var dora = await client.FounderAsync("Dora");
        var ben = await client.PersonAsync("Ben");
        await client.JoinAsync(dora, ben);
        var invitation = await client.InviteAsync(anna);
        var dorasBefore = await client.MineAsync(dora);

        var accept = await client.SendAsync(ben, HttpMethod.Post, Invitation(invitation.Token) + "/accept");

        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.AlreadyInHousehold), (accept.StatusCode, await accept.ReadCodeAsync()));
        SameHousehold(dorasBefore, await client.MineAsync(dora));
        Assert.Single((await client.MineAsync(anna)).Members);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Invitation(invitation.Token))).StatusCode);
    }

    [Fact]
    public async Task The_founder_accepting_an_own_invitation_leaves_it_open()
    {
        var anna = await client.FounderAsync("Anna");
        var invitation = await client.InviteAsync(anna);

        var accept = await client.SendAsync(anna, HttpMethod.Post, Invitation(invitation.Token) + "/accept");

        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.AlreadyInHousehold), (accept.StatusCode, await accept.ReadCodeAsync()));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Invitation(invitation.Token))).StatusCode);
    }

    // A9
    [Fact]
    public async Task An_expired_invitation_can_be_neither_previewed_nor_accepted()
    {
        var anna = await client.FounderAsync("Anna");
        var ben = await client.PersonAsync("Ben");
        var invitation = await client.InviteAsync(anna);
        await ExpireAllAsync((await client.MineAsync(anna)).Id);

        var preview = await client.GetAsync(Invitation(invitation.Token));
        var accept = await client.SendAsync(ben, HttpMethod.Post, Invitation(invitation.Token) + "/accept");

        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.InvitationExpired), (preview.StatusCode, await preview.ReadCodeAsync()));
        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.InvitationExpired), (accept.StatusCode, await accept.ReadCodeAsync()));
        Assert.True(await client.HasNoHouseholdAsync(ben));
    }

    // A9
    [Fact]
    public async Task An_unknown_token_is_not_found()
    {
        var ben = await client.PersonAsync("Ben");

        var preview = await client.GetAsync(Invitation("gibt-es-nicht"));
        var accept = await client.SendAsync(ben, HttpMethod.Post, Invitation("gibt-es-nicht") + "/accept");

        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.InvitationNotFound), (preview.StatusCode, await preview.ReadCodeAsync()));
        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.InvitationNotFound), (accept.StatusCode, await accept.ReadCodeAsync()));
    }

    [Fact]
    public async Task Accepting_needs_a_sign_in()
    {
        var anna = await client.FounderAsync("Anna");
        var invitation = await client.InviteAsync(anna);

        var accept = await client.PostAsync(Invitation(invitation.Token) + "/accept", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, accept.StatusCode);
    }

    [Fact]
    public async Task Revoking_an_unknown_invitation_is_not_found()
    {
        var anna = await client.PersonAsync("Anna");
        var household = await client.FoundAsync(anna);

        var revoke = await client.SendAsync(anna, HttpMethod.Delete, $"{Invitations(household.Id)}/{Guid.NewGuid()}");

        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.InvitationNotFound), (revoke.StatusCode, await revoke.ReadCodeAsync()));
    }

    // A22: Nimmt Ben an, während Anna zurückzieht, gewinnt einer von beiden ganz; nie steht "zurückgezogen" an
    // einer Einladung, die Ben schon benutzt hat.
    [Fact]
    public async Task Accepting_and_revoking_at_the_same_time_never_revoke_a_used_invitation()
    {
        var anna = await client.FounderAsync("Anna");
        var household = await client.MineAsync(anna);
        // Mehrere Runden, damit ein Wettlauf ohne Sperre sicher auffällt.
        for (var round = 1; round <= 10; round++)
        {
            var ben = await client.PersonAsync($"Ben{round}");
            var invitation = await client.InviteAsync(anna);

            await Task.WhenAll(
                client.SendAsync(ben, HttpMethod.Post, Invitation(invitation.Token) + "/accept"),
                client.SendAsync(anna, HttpMethod.Delete, $"{Invitations(household.Id)}/{invitation.Id}"));

            var stored = await fixture.Factory.WithDbAsync(db => db.HouseholdInvitations
                .Where(i => i.Id == invitation.Id)
                .Select(i => new { i.Status, i.UsedById })
                .SingleAsync());
            var (status, usedBy) = (stored.Status, stored.UsedById);
            var member = !await client.HasNoHouseholdAsync(ben);
            Assert.True(
                (status, usedBy, member) == (InvitationStatus.Accepted, ben.Id, true)
                || (status, usedBy, member) == (InvitationStatus.Revoked, null, false),
                $"Runde {round}: {status}, benutzt von {usedBy}, Mitglied {member}");
        }
    }

    private Task<int> ExpireAllAsync(Guid householdId) =>
        fixture.Factory.WithDbAsync(db => db.HouseholdInvitations
            .Where(i => i.HouseholdId == householdId)
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.ExpiresAt, DateTimeOffset.UtcNow.AddSeconds(-1))));

    private async Task<Guid> AddAccountAsync(Person person)
    {
        var response = await client.SendAsync(person, HttpMethod.Post, "/api/v1/accounts",
            new CreateAccountRequest("Giro", AccountTypes.Checking, 100m, new DateOnly(2026, 1, 1), null, null, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountResponse>())!.Id;
    }
}
