using Bunit;
using Duofy.ApiClient;
using Duofy.Schemas;
using Duofy.Shared.Pages;

namespace Duofy.Shared.Tests;

/// <summary>Die Kontoseite (R6, R7) und das Abmelden (A10).</summary>
public class AccountPageTests : PageTestContext
{
    [Fact]
    public void Sign_out_empties_the_token_store_and_goes_to_sign_in()
    {
        var cut = Render<Account>();

        Field(cut, "account-sign-out").Click();

        cut.WaitForAssertion(() => Assert.Equal("/anmelden", CurrentPath));
        Assert.Null(Tokens.Tokens);
        Assert.Equal(1, Auth.LogoutCalls);
    }

    [Fact]
    public void Sign_out_everywhere_empties_the_token_store_and_goes_to_sign_in()
    {
        var cut = Render<Account>();

        Field(cut, "account-sign-out-everywhere").Click();

        cut.WaitForAssertion(() => Assert.Equal("/anmelden", CurrentPath));
        Assert.Null(Tokens.Tokens);
        Assert.Equal(1, Auth.LogoutEverywhereCalls);
    }

    [Fact]
    public void Sign_out_everywhere_error_is_shown_in_the_sessions_section()
    {
        Auth.LogoutEverywhereResult = ApiResult.Failure(ClientErrorCodes.ServerUnreachable);
        var cut = Render<Account>();

        Field(cut, "account-sign-out-everywhere").Click();

        const string error = "Der Server ist gerade nicht erreichbar.";
        cut.WaitForAssertion(() => Assert.Contains(error, cut.Find("[data-testid='account-sessions']").TextContent));
        Assert.DoesNotContain(error, cut.Find("[data-testid='account-names']").TextContent);
        Assert.NotEqual("/anmelden", CurrentPath);
    }

    [Fact]
    public void Names_are_prefilled_and_can_be_saved()
    {
        var cut = Render<Account>();
        cut.WaitForAssertion(() => Assert.Equal("Anna", Field(cut, "account-first-name").GetAttribute("value")));

        Field(cut, "account-first-name").Change("Anne");
        Field(cut, "account-names-save").Click();

        cut.WaitForAssertion(() => Assert.Contains("Der Name wurde gespeichert.", cut.Markup));
        Assert.Equal(new UpdateMeRequest("Anne", "Muster"), Assert.Single(Users.Updated));
    }

    [Fact]
    public void Name_error_is_shown_as_german_text()
    {
        Users.UpdateResult = ApiResult.Failure(ErrorCodes.FirstNameRequired);
        var cut = Render<Account>();
        cut.WaitForAssertion(() => Assert.Equal("Anna", Field(cut, "account-first-name").GetAttribute("value")));

        Field(cut, "account-names-save").Click();

        cut.WaitForAssertion(() => Assert.Contains("Bitte gib deinen Vornamen an.", cut.Markup));
    }

    [Fact]
    public void Password_can_be_changed()
    {
        var cut = Render<Account>();

        Field(cut, "account-current-password").Change("altes-passwort-1");
        Field(cut, "account-new-password").Change("neues-passwort-2");
        Field(cut, "account-password-save").Click();

        cut.WaitForAssertion(() => Assert.Contains("Das Passwort wurde geändert.", cut.Markup));
        Assert.Equal(new ChangePasswordRequest("altes-passwort-1", "neues-passwort-2"), Assert.Single(Users.PasswordChanges));
    }

    [Fact]
    public void Wrong_current_password_is_shown_as_german_text()
    {
        Users.ChangePasswordResult = ApiResult.Failure(ErrorCodes.PasswordWrong);
        var cut = Render<Account>();

        Field(cut, "account-current-password").Change("falsch-falsch-1");
        Field(cut, "account-new-password").Change("neues-passwort-2");
        Field(cut, "account-password-save").Click();

        cut.WaitForAssertion(() => Assert.Contains("Das Passwort stimmt nicht.", cut.Markup));
    }

    [Fact]
    public void Delete_account_is_reachable_from_the_page()
    {
        var cut = Render<Account>();

        Assert.Equal("/konto/loeschen", cut.Find("[data-testid='account-delete-link']").GetAttribute("href"));
    }
}

/// <summary>Die Kontoseite ist aus den Bausteinen gebaut, jeder Abschnitt eine Karte mit eigener Meldung.</summary>
public class AccountPageBuildTests : PageTestContext
{
    [Fact]
    public void The_page_is_built_from_the_blocks_and_every_section_is_a_card()
    {
        var cut = Render<Account>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='account-names']")));

        foreach (var section in new[] { "account-names", "account-email", "account-quotas", "account-sessions" })
        {
            Assert.Contains("card", cut.Find($"[data-testid='{section}']").ClassList);
        }
        // Die Zahlenfelder (Quoten) lassen keine Id am input zu: Ihre Beschriftung hat eine eigene Id, und das Feld verweist per aria-labelledby darauf.
        Assert.Equal(["account-first-name-input", "account-last-name-input", "account-new-email-input", "account-email-password-input",
            null, null, null, "account-current-password-input", "account-new-password-input"],
            cut.FindAll("label.field-label").Select(l => l.GetAttribute("for")));
        foreach (var pot in new[] { "needs", "wants", "savings" })
        {
            Assert.Equal($"account-quota-{pot}-label", Field(cut, $"account-quota-{pot}").GetAttribute("aria-labelledby"));
            Assert.NotNull(cut.Find($"label#account-quota-{pot}-label"));
        }
    }

    [Fact]
    public void The_fields_keep_their_types_and_autocomplete_hints()
    {
        var cut = Render<Account>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='account-names']")));

        Assert.Equal("email", Field(cut, "account-new-email").GetAttribute("type"));
        Assert.Equal("password", Field(cut, "account-email-password").GetAttribute("type"));
        Assert.Equal("password", Field(cut, "account-current-password").GetAttribute("type"));
        Assert.Equal("new-password", Field(cut, "account-new-password").GetAttribute("autocomplete"));
        Assert.Equal("account-new-password-input", Field(cut, "account-new-password").Id);
    }

    [Fact]
    public void Each_section_saves_on_its_own_and_shows_its_own_message()
    {
        var cut = Render<Account>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='account-names']")));
        Field(cut, "account-first-name").Change("Anna");
        Field(cut, "account-last-name").Change("Neu");

        Field(cut, "account-names-save").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.Find("[data-testid='account-names']").QuerySelectorAll("[role='alert']")));
        Assert.Empty(cut.Find("[data-testid='account-quotas']").QuerySelectorAll("[role='alert']"));
        Assert.Empty(cut.Find("[data-testid='account-sessions']").QuerySelectorAll("[role='alert']"));
    }

    [Fact]
    public void The_delete_link_leads_to_the_delete_page_as_a_link_styled_as_a_button()
    {
        var cut = Render<Account>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='account-delete-link']")));

        var link = cut.Find("a[data-testid='account-delete-link']");

        Assert.Equal("/konto/loeschen", link.GetAttribute("href"));
        Assert.Contains("duofy-button--secondary", link.ClassList);
    }

    [Fact]
    public void The_sign_out_buttons_are_real_buttons()
    {
        var cut = Render<Account>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='account-sign-out']")));

        Assert.Contains("duofy-button", cut.Find("[data-testid='account-sign-out']").ClassList);
        Assert.Contains("duofy-button", cut.Find("[data-testid='account-sign-out-everywhere']").ClassList);
    }

    // Spec 118, R1: Auch am PC und Tablet, wo es die Seite Mehr nicht gibt, führt die Kontoseite zur Seite "Benachrichtigungen".
    [Fact]
    public void The_account_page_links_to_the_notifications_page()
    {
        ReminderApiFake.Available = true;

        var cut = Render<Account>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='account-notifications']")));
        Assert.Equal("/benachrichtigungen", cut.Find("[data-testid='account-notifications-open']").GetAttribute("href"));
        Assert.Empty(cut.FindAll("[data-testid='notifications']"));
    }

    [Fact]
    public void The_account_page_hides_the_notifications_link_without_reminders_on_the_server()
    {
        ReminderApiFake.Available = false;

        var cut = Render<Account>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='account-names']")));
        Assert.Empty(cut.FindAll("[data-testid='account-notifications']"));
    }
}
