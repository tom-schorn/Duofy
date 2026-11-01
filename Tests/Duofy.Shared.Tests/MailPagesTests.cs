using Bunit;
using Duofy.ApiClient;
using Duofy.Schemas;
using Duofy.Shared.Pages;

namespace Duofy.Shared.Tests;

/// <summary>
/// Die Seiten zu Bestätigung, "Passwort vergessen" und dem Wechsel der Adresse (Spec 53). Ohne Vorgabe ist der Server
/// selbst betrieben (FakeInstanceApi); <see cref="Hosted"/> macht ihn zur gehosteten Version.
/// </summary>
public class MailPagesTests : PageTestContext
{
    private void Hosted() => InstanceApiFake.Instance = InstanceApiFake.Instance with { Hosted = true };

    private static bool Has(IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> cut, string testId) =>
        cut.FindAll($"[data-testid='{testId}']").Count > 0;

    // --- Anmeldung -------------------------------------------------------

    // A8
    [Fact]
    public void On_a_self_hosted_server_passwort_vergessen_names_the_operator_and_sends_nothing()
    {
        var cut = Render<Login>();

        cut.Find("[data-testid='login-forgot']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Bitte wende dich an die Person, die diesen Server betreibt.",
            cut.Find("[data-testid='login-forgot-note']").TextContent));
        Assert.False(Has(cut, "login-forgot-submit"));
        Assert.Empty(Auth.Calls);
    }

    [Fact]
    public void On_a_hosted_server_passwort_vergessen_sends_the_address_and_says_a_link_may_be_on_its_way()
    {
        Hosted();
        var cut = Render<Login>();
        Field(cut, "login-email").Input("anna@example.org");

        cut.Find("[data-testid='login-forgot']").Click();
        cut.WaitForAssertion(() => Assert.True(Has(cut, "login-forgot-submit")));
        Assert.Equal("anna@example.org", Field(cut, "login-forgot-email").GetAttribute("value"));
        Field(cut, "login-forgot-submit").Click();

        cut.WaitForAssertion(() => Assert.Equal(new ForgotPasswordRequest("anna@example.org"), Assert.Single(Auth.Calls)));
        Assert.Contains("Wenn zu dieser Adresse ein Konto gehört, ist ein Link unterwegs. Er gilt eine Stunde.", cut.Markup);
        Assert.False(Has(cut, "login-forgot-note"));
    }

    [Fact]
    public void An_unconfirmed_account_gets_the_hint_and_can_send_the_link_once_more()
    {
        Auth.LoginResult = ApiResult.Failure(ErrorCodes.EmailNotConfirmed);
        var cut = Render<Login>();
        Field(cut, "login-email").Input("anna@example.org");
        Field(cut, "login-password").Input("ein-langes-passwort");

        Field(cut, "login-submit").Click();

        cut.WaitForAssertion(() => Assert.Contains("Bitte bestätige zuerst deine E-Mail-Adresse.", cut.Markup));
        Field(cut, "login-resend").Click();

        cut.WaitForAssertion(() => Assert.Equal(new ResendConfirmationRequest("anna@example.org"), Assert.Single(Auth.Calls)));
        // Danach ist der Knopf ausgegraut, und darunter steht, warum.
        var button = cut.Find("[data-testid='login-resend']");
        Assert.Equal("true", button.GetAttribute("aria-disabled"));
        Assert.Contains("in fünf Minuten", cut.Find("[data-testid='login-resend-reason']").TextContent);
        button.Click();
        Assert.Single(Auth.Calls);
    }

    // --- Registrierung ---------------------------------------------------

    [Fact]
    public void After_registering_on_a_hosted_server_the_page_asks_to_open_the_mail()
    {
        Auth.LoginResult = ApiResult.Failure(ErrorCodes.EmailNotConfirmed);
        var cut = Render<Register>();
        Field(cut, "register-first-name").Change("Anna");
        Field(cut, "register-last-name").Change("Muster");
        Field(cut, "register-email").Change("anna@example.org");
        Field(cut, "register-password").Change("ein-langes-passwort");
        Field(cut, "register-terms").Change(true);

        Field(cut, "register-submit").Click();

        cut.WaitForAssertion(() => Assert.Contains("anna@example.org",
            cut.Find("[data-testid='register-confirm-notice']").TextContent));
        Assert.Contains("24 Stunden", cut.Markup);
        Assert.DoesNotContain("/anmelden", CurrentPath);
    }

    // --- Seiten aus den Links ---------------------------------------------

    [Fact]
    public void The_confirmation_page_confirms_with_id_and_token_from_the_address()
    {
        var id = Guid.NewGuid();
        Navigation.NavigateTo($"/bestaetigen?id={id}&token=a%2Bb%3D");
        var cut = Render<ConfirmEmail>();

        Field(cut, "confirm-submit").Click();

        cut.WaitForAssertion(() => Assert.Equal(new ConfirmEmailRequest(id, "a+b="), Assert.Single(Auth.Calls)));
        Assert.Contains("Deine E-Mail-Adresse ist bestätigt.", cut.Markup);
        Assert.Equal("/anmelden", cut.Find("[data-testid='confirm-sign-in']").GetAttribute("href"));
    }

    [Fact]
    public void A_used_link_says_so()
    {
        Auth.MailResult = ApiResult.Failure(ErrorCodes.TokenInvalid);
        Navigation.NavigateTo($"/bestaetigen?id={Guid.NewGuid()}&token=alt");
        var cut = Render<ConfirmEmail>();

        Field(cut, "confirm-submit").Click();

        cut.WaitForAssertion(() => Assert.Contains("Der Link ist abgelaufen oder wurde schon benutzt.", cut.Markup));
    }

    [Fact]
    public void The_new_password_page_resets_with_the_mail_token()
    {
        Navigation.NavigateTo("/passwort-neu?email=anna%40example.org&token=t%2B1");
        var cut = Render<ResetPassword>();
        Field(cut, "reset-password").Change("ganz-neues-passwort");

        Field(cut, "reset-submit").Click();

        cut.WaitForAssertion(() => Assert.Equal(new ResetPasswordRequest("anna@example.org", "t+1", "ganz-neues-passwort"), Assert.Single(Auth.Calls)));
        Assert.Contains("Dein Passwort ist geändert.", cut.Markup);
        Assert.True(Has(cut, "reset-sign-in"));
    }

    [Fact]
    public void The_new_password_page_resets_with_the_link_of_the_administrator()
    {
        Navigation.NavigateTo("/passwort-neu?link=abc_-1");
        var cut = Render<ResetPassword>();
        Field(cut, "reset-password").Change("ganz-neues-passwort");

        Field(cut, "reset-submit").Click();

        cut.WaitForAssertion(() => Assert.Equal(new ResetPasswordByLinkRequest("abc_-1", "ganz-neues-passwort"), Assert.Single(Auth.Calls)));
    }

    [Fact]
    public void The_new_password_page_without_a_token_says_the_link_is_incomplete_and_offers_no_button()
    {
        Navigation.NavigateTo("/passwort-neu");
        var cut = Render<ResetPassword>();

        Assert.Contains("Dieser Link ist unvollständig.", cut.Markup);
        Assert.False(Has(cut, "reset-submit"));
    }

    [Fact]
    public void A_too_short_new_password_is_named_before_sending()
    {
        Navigation.NavigateTo("/passwort-neu?link=abc");
        var cut = Render<ResetPassword>();
        Field(cut, "reset-password").Change("kurz");

        Field(cut, "reset-submit").Click();

        cut.WaitForAssertion(() => Assert.Contains("Das Passwort braucht mindestens 10 Zeichen.", cut.Markup));
        Assert.Empty(Auth.Calls);
    }

    [Fact]
    public void The_email_change_page_shows_the_new_address_and_applies_it()
    {
        var id = Guid.NewGuid();
        Navigation.NavigateTo($"/email-bestaetigen?id={id}&email=anna%40neu.de&token=t");
        var cut = Render<ConfirmEmailChange>();

        Assert.Contains("anna@neu.de", cut.Markup);
        Field(cut, "email-confirm-submit").Click();

        cut.WaitForAssertion(() => Assert.Equal(new ConfirmEmailChangeRequest(id, "anna@neu.de", "t"), Assert.Single(Auth.Calls)));
        Assert.Contains("Deine neue Adresse gilt ab jetzt.", cut.Markup);
    }

    // --- Konto -------------------------------------------------------------

    // A10 in der Oberfläche
    [Fact]
    public void On_a_self_hosted_server_the_account_page_changes_the_address_at_once()
    {
        var cut = Render<Account>();
        cut.WaitForAssertion(() => Assert.True(Has(cut, "account-email-save")));
        Field(cut, "account-new-email").Change("anna@neu.de");
        Field(cut, "account-email-password").Change("ein-langes-passwort");

        Field(cut, "account-email-save").Click();

        cut.WaitForAssertion(() => Assert.Contains("Deine E-Mail-Adresse ist geändert.", cut.Markup));
        Assert.Equal(new ChangeEmailRequest("anna@neu.de", "ein-langes-passwort"), Assert.Single(Users.EmailChanges));
        Assert.Contains("anna@neu.de", cut.Markup);
    }

    // A9 in der Oberfläche
    [Fact]
    public void On_a_hosted_server_the_account_page_says_the_new_address_waits_for_the_link()
    {
        Hosted();
        Users.EmailChangesAtOnce = false;
        var cut = Render<Account>();
        cut.WaitForAssertion(() => Assert.True(Has(cut, "account-email-save")));
        Field(cut, "account-new-email").Change("anna@neu.de");
        Field(cut, "account-email-password").Change("ein-langes-passwort");

        Field(cut, "account-email-save").Click();

        cut.WaitForAssertion(() => Assert.Contains("Wir haben einen Link an anna@neu.de geschickt.", cut.Markup));
        Assert.Contains("anna@example.org", cut.Markup);
    }

    [Fact]
    public void A_refused_address_change_shows_the_reason()
    {
        Users.ChangeEmailError = ErrorCodes.PasswordWrong;
        var cut = Render<Account>();
        cut.WaitForAssertion(() => Assert.True(Has(cut, "account-email-save")));
        Field(cut, "account-new-email").Change("anna@neu.de");

        Field(cut, "account-email-save").Click();

        cut.WaitForAssertion(() => Assert.Contains("Das Passwort stimmt nicht.", cut.Markup));
    }

    // --- Admin ---------------------------------------------------------------

    private IRenderedComponent<Admin> OpenAdmin()
    {
        Users.Me = Users.Me with { IsInstanceAdmin = true };
        var cut = Render<Admin>();
        cut.WaitForAssertion(() => Assert.True(Has(cut, "admin-create")));
        return cut;
    }

    // A7 in der Oberfläche
    [Fact]
    public void On_a_self_hosted_server_the_administrator_creates_a_reset_link_with_qr_code_and_teilen()
    {
        var cut = OpenAdmin();
        cut.WaitForAssertion(() => Assert.True(Has(cut, "admin-reset-create")));
        Field(cut, "admin-reset-email").Change("anna@example.org");

        Field(cut, "admin-reset-create").Click();

        var link = $"{FakeAdminApi.PublicUrl}/passwort-neu?link=reset1";
        cut.WaitForAssertion(() => Assert.Equal(link, cut.Find("[data-testid='admin-reset-link']").TextContent.Trim()));
        Assert.Equal("anna@example.org", Assert.Single(AdminApiFake.ResetLinksFor));
        Assert.NotEmpty(cut.FindAll("[data-testid='admin-reset-qr'] svg"));
        Assert.Contains("Gültig bis 22.10.2026", cut.Markup);

        cut.Find("[data-testid='admin-reset-share']").Click();

        cut.WaitForAssertion(() => Assert.Equal(("Neues Passwort für Duofy", link), Assert.Single(ShareFake.Shared)));
    }

    [Fact]
    public void An_unknown_address_is_named_to_the_administrator()
    {
        var cut = OpenAdmin();
        cut.WaitForAssertion(() => Assert.True(Has(cut, "admin-reset-create")));
        Field(cut, "admin-reset-email").Change("niemand@example.org");

        Field(cut, "admin-reset-create").Click();

        cut.WaitForAssertion(() => Assert.Contains("Zu dieser Adresse gibt es kein Konto.", cut.Markup));
        Assert.False(Has(cut, "admin-reset-link"));
    }

    [Fact]
    public void On_a_hosted_server_the_admin_page_offers_no_reset_links_and_says_invitations_go_by_mail()
    {
        Hosted();
        var cut = OpenAdmin();
        Field(cut, "admin-email").Input("ben@example.org");

        Field(cut, "admin-create").Click();

        cut.WaitForAssertion(() => Assert.Contains("Der Link geht auch per Mail an die Adresse", cut.Markup));
        Assert.False(Has(cut, "admin-reset"));
    }

    // --- Haushalt -------------------------------------------------------------

    // A11 in der Oberfläche
    [Fact]
    public void On_a_hosted_server_the_invitation_can_name_an_address_and_the_dialog_says_it_may_be_on_its_way()
    {
        Hosted();
        var dialogs = RenderRadzenHost();
        var cut = Render<Household>();
        cut.WaitForAssertion(() => Assert.True(Has(cut, "household-invite-email")));
        Field(cut, "household-invite-email").Change("ben@example.org");

        cut.Find("[data-testid='household-invite']").Click();

        dialogs.WaitForAssertion(() => Assert.Contains("Wenn zu dieser Adresse ein bestätigtes Konto gehört, ist die Einladung unterwegs.",
            dialogs.Find("[data-testid='invite-mail-note']").TextContent));
        Assert.Equal("ben@example.org", Assert.Single(HouseholdApiFake.InvitationEmails));
    }

    [Fact]
    public void On_a_self_hosted_server_the_invitation_has_no_address_field()
    {
        var dialogs = RenderRadzenHost();
        var cut = Render<Household>();
        cut.WaitForAssertion(() => Assert.True(Has(cut, "household-invite")));

        cut.Find("[data-testid='household-invite']").Click();

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='invite-link']")));
        Assert.False(Has(cut, "household-invite-email"));
        Assert.Empty(dialogs.FindAll("[data-testid='invite-mail-note']"));
        Assert.Null(Assert.Single(HouseholdApiFake.InvitationEmails));
    }
}

public class ResetPasswordRadzenTests : PageTestContext
{
    private bool Has(IRenderedComponent<ResetPassword> cut, string testId) => cut.FindAll($"[data-testid='{testId}']").Count > 0;

    // Radzen-Umstellung R11: Passwort zurücksetzen in Radzen.
    [Fact]
    public void The_new_password_page_is_built_with_radzen_and_offers_a_link_to_sign_in_after_resetting()
    {
        Navigation.NavigateTo("/passwort-neu?link=abc_-1");
        var cut = Render<ResetPassword>();

        Assert.Equal("password", Field(cut, "reset-password").GetAttribute("type"));
        Assert.Equal("new-password", Field(cut, "reset-password").GetAttribute("autocomplete"));
        Assert.Equal("reset-password-input", Field(cut, "reset-password").Id);

        Field(cut, "reset-password").Change("ganz-neues-passwort");
        Field(cut, "reset-submit").Click();

        cut.WaitForAssertion(() => Assert.True(Has(cut, "reset-sign-in")));
        Assert.Equal("/anmelden", cut.Find("[data-testid='reset-sign-in']").GetAttribute("href"));
    }

    [Fact]
    public void An_incomplete_new_password_link_shows_its_warning_in_a_radzen_alert()
    {
        Navigation.NavigateTo("/passwort-neu");
        var cut = Render<ResetPassword>();

        Assert.Contains("rz-alert", cut.Find("[data-testid='reset-incomplete']").ClassList);
    }
}

/// <summary>Radzen-Umstellung R12: Die Bestätigungsseiten sind in Radzen gebaut.</summary>
public class ConfirmPagesRadzenTests : PageTestContext
{
    [Fact]
    public void The_confirmation_page_is_built_with_radzen_and_ends_with_a_link_to_sign_in()
    {
        Navigation.NavigateTo($"/bestaetigen?id={Guid.NewGuid()}&token=t");
        var cut = Render<ConfirmEmail>();

        Assert.Contains("rz-button", cut.Find("[data-testid='confirm-submit']").ClassList);
        Assert.Equal("Adresse bestätigen", cut.Find("[data-testid='confirm-submit']").TextContent.Trim());

        Field(cut, "confirm-submit").Click();

        cut.WaitForAssertion(() => Assert.Equal("/anmelden", cut.Find("a[data-testid='confirm-sign-in']").GetAttribute("href")));
        Assert.Contains("rz-alert", cut.Find(".rz-alert").ClassList);
    }

    [Fact]
    public void An_incomplete_confirmation_link_shows_its_warning_in_a_radzen_alert()
    {
        Navigation.NavigateTo("/bestaetigen");
        var cut = Render<ConfirmEmail>();

        Assert.Contains("rz-alert", cut.Find("[data-testid='confirm-incomplete']").ClassList);
        Assert.Contains("Dieser Link ist unvollständig.", cut.Find("[data-testid='confirm-incomplete']").TextContent);
        Assert.Empty(cut.FindAll("[data-testid='confirm-submit']"));
    }

    [Fact]
    public void The_email_change_page_is_built_with_radzen_and_ends_with_a_link_to_sign_in()
    {
        Navigation.NavigateTo($"/email-bestaetigen?id={Guid.NewGuid()}&email=anna%40neu.de&token=t");
        var cut = Render<ConfirmEmailChange>();

        Assert.Contains("anna@neu.de", cut.Find("[data-testid='email-confirm-address']").TextContent);

        Field(cut, "email-confirm-submit").Click();

        cut.WaitForAssertion(() => Assert.Equal("/anmelden", cut.Find("a[data-testid='email-confirm-sign-in']").GetAttribute("href")));
    }

    [Fact]
    public void An_incomplete_email_change_link_shows_its_warning_in_a_radzen_alert()
    {
        Navigation.NavigateTo("/email-bestaetigen");
        var cut = Render<ConfirmEmailChange>();

        Assert.Contains("rz-alert", cut.Find("[data-testid='email-confirm-incomplete']").ClassList);
    }
}
