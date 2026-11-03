using Bunit;
using Duofy.ApiClient;
using Duofy.Schemas;
using Duofy.Shared.Layout;
using Duofy.Shared.Pages;
using Microsoft.AspNetCore.Components;

namespace Duofy.Shared.Tests;

/// <summary>
/// Der Server in der App (Spec 70): Einrichtungscode und Modus bei der Registrierung, Admin werden im Benutzerkonto,
/// der Eintrag Admin unter "Mehr" und die Seite Admin mit den Einladungen zur Registrierung.
/// </summary>
public class InstancePagesTests : PageTestContext
{
    private readonly IRenderedComponent<IComponent> dialogs;

    public InstancePagesTests()
    {
        dialogs = RenderDialogHost();
    }

    private void Mode(string mode, bool setupPending = false) =>
        InstanceApiFake.Instance = InstanceApiFake.Instance with { RegistrationMode = mode, SetupPending = setupPending };

    private IRenderedComponent<Register> OpenRegister(string? invitation = null)
    {
        if (invitation is not null)
        {
            Navigation.NavigateTo(Navigation.GetUriWithQueryParameter("einladung", invitation));
        }
        var cut = Render<Register>();
        Field(cut, "register-first-name").Change("Ben");
        Field(cut, "register-last-name").Change("Muster");
        Field(cut, "register-email").Change("ben@example.org");
        Field(cut, "register-password").Change("ein-langes-passwort");
        Field(cut, "register-terms").Click();
        return cut;
    }

    // A14
    [Fact]
    public void While_setup_is_pending_the_registration_page_has_the_field_einrichtungscode()
    {
        Mode(RegistrationModes.Invite, setupPending: true);

        var cut = OpenRegister();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='register-setup-code']")));
        Assert.Contains("Einrichtungscode", cut.Markup);
    }

    [Fact]
    public void Without_pending_setup_there_is_no_field_einrichtungscode()
    {
        Mode(RegistrationModes.Open);

        var cut = OpenRegister();

        Assert.Empty(cut.FindAll("[data-testid='register-setup-code']"));
        Assert.DoesNotContain("Einrichtungscode", cut.Markup);
    }

    // A14
    [Fact]
    public void In_invite_mode_without_invitation_the_page_says_so_and_greys_out_the_button()
    {
        Mode(RegistrationModes.Invite);

        var cut = OpenRegister();

        cut.WaitForAssertion(() => Assert.Contains("Registrieren geht auf diesem Server nur mit Einladung.",
            cut.Find("[data-testid='register-mode-notice']").TextContent));
        var submit = cut.Find("[data-testid='register-submit']");
        Assert.Equal("true", submit.GetAttribute("aria-disabled"));
        Assert.Equal("register-mode-notice", submit.GetAttribute("aria-describedby"));
        submit.Click();
        Assert.Empty(Auth.Registered);
    }

    [Fact]
    public void In_closed_mode_the_page_says_that_nobody_can_register()
    {
        Mode(RegistrationModes.Closed);

        var cut = OpenRegister();

        cut.WaitForAssertion(() => Assert.Contains("Auf diesem Server ist keine Registrierung möglich.",
            cut.Find("[data-testid='register-mode-notice']").TextContent));
        Assert.Equal("true", cut.Find("[data-testid='register-submit']").GetAttribute("aria-disabled"));
    }

    [Fact]
    public void With_an_invitation_in_the_address_the_token_is_sent_and_no_notice_is_shown()
    {
        Mode(RegistrationModes.Invite);

        var cut = OpenRegister(invitation: "tok");
        cut.WaitForAssertion(() => Assert.NotEqual("true", cut.Find("[data-testid='register-submit']").GetAttribute("aria-disabled")));
        Assert.Empty(cut.FindAll("[data-testid='register-mode-notice']"));
        Field(cut, "register-submit").Click();

        cut.WaitForAssertion(() => Assert.Equal("tok", Assert.Single(Auth.Registered).InvitationToken));
        Assert.Null(Auth.Registered[0].SetupCode);
    }

    [Fact]
    public void A_typed_setup_code_lifts_the_mode_and_is_sent()
    {
        Mode(RegistrationModes.Closed, setupPending: true);
        var cut = OpenRegister();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='register-setup-code']")));

        Field(cut, "register-setup-code").Input(" s3cret ");
        cut.WaitForAssertion(() => Assert.NotEqual("true", cut.Find("[data-testid='register-submit']").GetAttribute("aria-disabled")));
        Field(cut, "register-submit").Click();

        cut.WaitForAssertion(() => Assert.Equal("s3cret", Assert.Single(Auth.Registered).SetupCode));
    }

    [Fact]
    public void Refusals_of_the_server_are_shown_in_german()
    {
        Mode(RegistrationModes.Invite);
        Auth.RegisterResult = ApiResult.Failure(ErrorCodes.InvitationInvalid);
        var cut = OpenRegister(invitation: "alt");
        cut.WaitForAssertion(() => Assert.NotEqual("true", cut.Find("[data-testid='register-submit']").GetAttribute("aria-disabled")));

        Field(cut, "register-submit").Click();

        cut.WaitForAssertion(() => Assert.Contains("Diese Einladung gilt nicht (mehr).", cut.Markup));
    }

    [Fact]
    public void Without_an_answer_of_the_server_the_page_lets_the_server_decide()
    {
        InstanceApiFake.Failure = ClientErrorCodes.ServerUnreachable;

        var cut = OpenRegister();
        Field(cut, "register-submit").Click();

        cut.WaitForAssertion(() => Assert.Single(Auth.Registered));
        Assert.Empty(cut.FindAll("[data-testid='register-mode-notice']"));
    }

    // A16 (Seite)
    [Fact]
    public void While_setup_is_pending_the_account_page_offers_admin_werden_and_claims_with_the_code()
    {
        Mode(RegistrationModes.Invite, setupPending: true);
        var cut = Render<Account>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='account-claim-admin']")));

        Field(cut, "account-setup-code").Input("s3cret");
        Field(cut, "account-claim-admin").Click();

        cut.WaitForAssertion(() => Assert.Contains("Du bist jetzt Admin dieses Servers.", cut.Markup));
        Assert.Equal("s3cret", Assert.Single(Users.Claims).SetupCode);
        Assert.Empty(cut.FindAll("[data-testid='account-claim-admin']"));
    }

    [Fact]
    public void A_wrong_code_on_the_account_page_says_so()
    {
        Mode(RegistrationModes.Invite, setupPending: true);
        var cut = Render<Account>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='account-claim-admin']")));

        Field(cut, "account-setup-code").Input("falsch");
        Field(cut, "account-claim-admin").Click();

        cut.WaitForAssertion(() => Assert.Contains("Der Einrichtungscode stimmt nicht", cut.Markup));
        Assert.NotEmpty(cut.FindAll("[data-testid='account-claim-admin']"));
    }

    [Fact]
    public void Without_pending_setup_the_account_page_offers_no_admin_werden()
    {
        Mode(RegistrationModes.Invite);

        var cut = Render<Account>();

        cut.WaitForAssertion(() => Assert.Contains("anna@example.org", cut.Markup));
        Assert.Empty(cut.FindAll("[data-testid='account-claim-admin']"));
    }

    // R5 mit Nachtrag: Ein Knopf, der nicht gehen darf, ist ausgegraut und sagt warum.
    [Fact]
    public void For_the_administrator_deleting_the_account_is_greyed_out_with_the_reason()
    {
        Users.Me = Users.Me with { IsInstanceAdmin = true };

        var cut = Render<Account>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='account-delete-reason']")));
        var link = cut.Find("[data-testid='account-delete-link']");
        Assert.Equal("true", link.GetAttribute("aria-disabled"));
        Assert.Null(link.GetAttribute("href"));
        Assert.Contains("einzige Person, die diesen Server verwaltet", cut.Find("[data-testid='account-delete-reason']").TextContent);
    }

    [Fact]
    public void For_everybody_else_deleting_the_account_stays_a_link()
    {
        var cut = Render<Account>();

        cut.WaitForAssertion(() => Assert.Equal("/konto/loeschen", cut.Find("[data-testid='account-delete-link']").GetAttribute("href")));
        Assert.Empty(cut.FindAll("[data-testid='account-delete-reason']"));
    }

    // A15
    [Fact]
    public void Mehr_has_the_entry_admin_only_for_administrators()
    {
        var ben = Render<Mehr>();
        ben.WaitForAssertion(() => Assert.NotEmpty(ben.FindAll("[data-testid='more-licenses']")));
        Assert.Empty(ben.FindAll("[data-testid='more-admin']"));

        Users.Me = Users.Me with { IsInstanceAdmin = true };
        var anna = Render<Mehr>();

        anna.WaitForAssertion(() => Assert.NotEmpty(anna.FindAll("[data-testid='more-admin']")));
        var entry = anna.Find("[data-testid='more-admin']");
        Assert.Equal("/admin", entry.GetAttribute("href"));
        // Der Text eines Eintrags ohne sein Symbol.
        Assert.EndsWith("Admin", entry.TextContent.Trim());
    }

    [Fact]
    public void The_admin_page_lights_mehr()
    {
        Assert.Equal(AppDestination.More, AppDestinations.For("/admin"));
    }

    private IRenderedComponent<Admin> OpenAdmin()
    {
        Users.Me = Users.Me with { IsInstanceAdmin = true };
        var cut = Render<Admin>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='admin-create']")));
        return cut;
    }

    // A15
    [Fact]
    public void Creating_an_invitation_shows_its_link_qr_code_and_teilen()
    {
        var cut = OpenAdmin();

        Field(cut, "admin-create").Click();

        cut.WaitForAssertion(() => Assert.Single(AdminApiFake.Invitations));
        var invitation = AdminApiFake.Invitations[0];
        var link = $"{FakeAdminApi.PublicUrl}/registrieren?einladung={invitation.Token}";
        cut.WaitForAssertion(() => Assert.Equal(link, cut.Find($"[data-testid='admin-invitation-link-{invitation.Id}']").TextContent.Trim()));
        Assert.NotEmpty(cut.FindAll($"[data-testid='admin-invitation-qr-{invitation.Id}'] svg"));
        Assert.Null(Assert.Single(AdminApiFake.Created));
        Assert.Contains("Für jede Adresse", cut.Markup);
        Assert.Contains("Gültig bis 28.10.2026", cut.Markup);

        var share = cut.Find($"[data-testid='admin-invitation-share-{invitation.Id}']");
        Assert.EndsWith("Teilen", share.TextContent.Trim());
        share.Click();
        cut.WaitForAssertion(() => Assert.Equal(("Einladung zu Duofy", link), Assert.Single(ShareFake.Shared)));
    }

    // Am Handy schob eine lange Adresse ohne Leerstellen den Knopf Zurückziehen aus der Karte (Gesamtdurchlauf #76).
    [Fact]
    public void A_long_address_of_an_invitation_may_break_so_the_revoke_button_stays_in_the_card()
    {
        var cut = OpenAdmin();

        Field(cut, "admin-email").Input("eine.sehr.lange.adresse.ohne.leerstellen@example.de");
        Field(cut, "admin-create").Click();

        cut.WaitForAssertion(() =>
        {
            var address = Assert.Single(cut.FindAll("p"), p => p.TextContent.Trim() == "eine.sehr.lange.adresse.ohne.leerstellen@example.de");
            Assert.Contains("long-link", address.ClassList);
            // Der Knopf rutscht in die nächste Zeile, statt seinen Text abzuschneiden: .row bricht um (flex-wrap in duofy.css).
            var revoke = cut.Find("[data-testid^='admin-invitation-revoke-']");
            Assert.Contains("row", revoke.ParentElement!.ClassList);
        });
    }

    [Fact]
    public void An_invitation_for_an_address_names_the_address()
    {
        var cut = OpenAdmin();

        Field(cut, "admin-email").Input(" ben@example.de ");
        Field(cut, "admin-create").Click();

        cut.WaitForAssertion(() => Assert.Equal("ben@example.de", Assert.Single(AdminApiFake.Created)));
        cut.WaitForAssertion(() => Assert.Contains("ben@example.de", cut.Find("[data-testid^='admin-invitation-']").TextContent));
    }

    [Fact]
    public void Revoking_asks_once_and_removes_the_invitation()
    {
        AdminApiFake.Invitations.Add(new RegistrationInvitationResponse(Guid.NewGuid(), "offen", null,
            new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero), $"{FakeAdminApi.PublicUrl}/registrieren?einladung=offen"));
        var invitation = AdminApiFake.Invitations[0];
        var cut = OpenAdmin();
        cut.WaitForAssertion(() => Assert.Contains("Gültig bis 30.10.2026", cut.Markup));

        cut.Find($"[data-testid='admin-invitation-revoke-{invitation.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Einladung zurückziehen?", dialogs.Markup));
        Assert.Empty(AdminApiFake.Revoked);
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Zurückziehen").Click();

        cut.WaitForAssertion(() => Assert.Equal([invitation.Id], AdminApiFake.Revoked));
        cut.WaitForAssertion(() => Assert.DoesNotContain("Gültig bis 30.10.2026", cut.Markup));
    }
    [Fact]
    public void The_admin_page_is_built_from_the_blocks_and_keeps_the_qr_code_white()
    {
        AdminApiFake.Invitations.Add(new RegistrationInvitationResponse(Guid.NewGuid(), "offen", null,
            new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero), $"{FakeAdminApi.PublicUrl}/registrieren?einladung=offen"));
        var invitation = AdminApiFake.Invitations[0];

        var cut = OpenAdmin();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"[data-testid='admin-invitation-qr-{invitation.Id}']")));

        Assert.Contains("card", cut.Find($"[data-testid='admin-invitation-{invitation.Id}']").ClassList);
        Assert.Contains("duofy-button", cut.Find("[data-testid='admin-create']").ClassList);
        Assert.Equal("admin-email-input", cut.Find("label[for='admin-email-input']").GetAttribute("for"));
        Assert.Equal("email", Field(cut, "admin-email").GetAttribute("type"));
        Assert.Contains("qr-code", cut.Find($"[data-testid='admin-invitation-qr-{invitation.Id}']").ClassList);
    }

    [Fact]
    public void The_admin_page_says_which_mode_applies()
    {
        Mode(RegistrationModes.Closed);

        var cut = OpenAdmin();

        cut.WaitForAssertion(() => Assert.Contains("Die Registrierung ist geschlossen.", cut.Find("[data-testid='admin-mode']").TextContent));
        Assert.Contains("Keine offenen Einladungen.", cut.Markup);
    }

    [Fact]
    public void A_refusal_of_the_server_is_shown_in_german()
    {
        var cut = OpenAdmin();
        AdminApiFake.Failure = ErrorCodes.PublicUrlMissing;

        Field(cut, "admin-create").Click();

        cut.WaitForAssertion(() => Assert.Contains("Für Einladungen fehlt die öffentliche Adresse des Servers.", cut.Markup));
    }

    [Fact]
    public void Somebody_who_is_not_administrator_sees_no_invitations_on_the_admin_page()
    {
        AdminApiFake.Invitations.Add(new RegistrationInvitationResponse(Guid.NewGuid(), "geheim", null, DateTimeOffset.UtcNow.AddDays(3), "x"));

        var cut = Render<Admin>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='admin-not-allowed']")));
        Assert.Empty(cut.FindAll("[data-testid='admin-create']"));
        Assert.DoesNotContain("geheim", cut.Markup);
    }

    [Fact]
    public void The_admin_page_needs_a_sign_in()
    {
        Assert.NotNull(typeof(Admin).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true).SingleOrDefault());
        Assert.Equal("/admin", ((RouteAttribute)typeof(Admin).GetCustomAttributes(typeof(RouteAttribute), inherit: false).Single()).Template);
    }
}
