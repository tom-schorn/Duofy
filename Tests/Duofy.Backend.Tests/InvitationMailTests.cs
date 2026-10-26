using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Duofy.Schemas;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>Einladungen per Mail auf der gehosteten Version (Spec 53, R6): A11 und die Einladungen zur Registrierung.</summary>
[Collection(ApiCollection.Name)]
public sealed class InvitationMailTests(ApiFixture fixture)
{
    /// <summary>Die Namen der Felder einer Antwort; die Werte (Kennung, Token, Zeit) sind bei jeder Einladung andere.</summary>
    private static async Task<string> ShapeAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return string.Join(",", json.RootElement.EnumerateObject().Select(p => $"{p.Name}:{p.Value.ValueKind}"));
    }

    // A11
    [Fact]
    public async Task A_household_invitation_goes_by_mail_only_to_a_confirmed_account_and_answers_the_same_either_way()
    {
        await using var server = MailServer.Hosted(fixture);
        var anna = await server.ConfirmedPersonAsync();
        await server.Client.FoundAsync(anna);
        var household = await server.Client.MineAsync(anna);
        var ben = await server.ConfirmedAsync("Ben");
        var clara = NewEmail("clara");
        var dora = NewEmail("dora");
        (await server.RegisterAsync(dora, "Dora")).EnsureSuccessStatusCode();

        var toBen = await server.Client.SendAsync(anna, HttpMethod.Post, HouseholdDriver.Invitations(household.Id), new CreateHouseholdInvitationRequest(ben.ToUpperInvariant()));
        var toClara = await server.Client.SendAsync(anna, HttpMethod.Post, HouseholdDriver.Invitations(household.Id), new CreateHouseholdInvitationRequest(clara));
        var toDora = await server.Client.SendAsync(anna, HttpMethod.Post, HouseholdDriver.Invitations(household.Id), new CreateHouseholdInvitationRequest(dora));

        Assert.Equal(HttpStatusCode.Created, toBen.StatusCode);
        Assert.Equal(HttpStatusCode.Created, toClara.StatusCode);
        Assert.Equal(HttpStatusCode.Created, toDora.StatusCode);
        Assert.Equal(await ShapeAsync(toBen), await ShapeAsync(toClara));
        Assert.Equal(await ShapeAsync(toBen), await ShapeAsync(toDora));
        var invitation = (await toBen.Content.ReadFromJsonAsync<InvitationResponse>())!;
        var mail = server.Mails.To(ben).Last();
        Assert.Equal($"Anna Muster lädt dich in den Haushalt {household.Name} ein", mail.Subject);
        Assert.Equal(invitation.Url, MailLink.UrlOf(mail).AbsoluteUri);
        Assert.Empty(server.Mails.To(clara));
        // Dora hat ein Konto, aber ihre Adresse ist nicht bestätigt: nur die Mail zum Bestätigen.
        Assert.Single(server.Mails.To(dora));
    }

    [Fact]
    public async Task A_household_invitation_without_an_address_sends_nothing_and_a_wrong_address_is_refused()
    {
        await using var server = MailServer.Hosted(fixture);
        var anna = await server.ConfirmedPersonAsync();
        await server.Client.FoundAsync(anna);
        var household = await server.Client.MineAsync(anna);
        var before = server.Mails.All.Count;

        var plain = await server.Client.SendAsync(anna, HttpMethod.Post, HouseholdDriver.Invitations(household.Id));
        var invalid = await server.Client.SendAsync(anna, HttpMethod.Post, HouseholdDriver.Invitations(household.Id), new CreateHouseholdInvitationRequest("keine adresse"));

        Assert.Equal(HttpStatusCode.Created, plain.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(ErrorCodes.EmailInvalid, await invalid.ReadCodeAsync());
        Assert.Equal(before, server.Mails.All.Count);
    }

    [Fact]
    public async Task A_registration_invitation_with_an_address_goes_by_mail_to_any_address_at_most_20_a_day()
    {
        const string code = "s3cret-einrichtung";
        var settings = MailServer.HostedSettings();
        settings["Duofy:SetupCode"] = code;
        settings["Duofy:RegistrationMode"] = RegistrationModes.Invite;
        await using var server = MailServer.Start(MailServer.FreshDatabase(fixture), settings);
        var tom = await server.ConfirmedPersonAsync("Tom", code);

        var answers = new List<HttpResponseMessage>();
        var addresses = Enumerable.Range(1, 21).Select(i => NewEmail($"gast{i}")).ToList();
        foreach (var address in addresses)
        {
            answers.Add(await server.Client.SendAsync(tom, HttpMethod.Post, AdminDriver.AdminInvitations, new CreateRegistrationInvitationRequest(address)));
        }
        var withoutAddress = await server.Client.SendAsync(tom, HttpMethod.Post, AdminDriver.AdminInvitations, new CreateRegistrationInvitationRequest());

        Assert.All(answers, a => Assert.Equal(HttpStatusCode.Created, a.StatusCode));
        Assert.Equal(await ShapeAsync(answers[0]), await ShapeAsync(answers[20]));
        Assert.All(addresses.Take(20), a => Assert.Single(server.Mails.To(a)));
        Assert.Empty(server.Mails.To(addresses[20]));
        Assert.Equal(HttpStatusCode.Created, withoutAddress.StatusCode);
        var first = (await answers[0].Content.ReadFromJsonAsync<RegistrationInvitationResponse>())!;
        var mail = server.Mails.To(addresses[0]).Single();
        Assert.Equal("Einladung zu Duofy", mail.Subject);
        Assert.Equal(first.Url, MailLink.UrlOf(mail).AbsoluteUri);
        Assert.Contains(server.Logs.Messages("Duofy.Api.Admin"), m => m.Contains("Tagesgrenze"));
    }
}
