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
