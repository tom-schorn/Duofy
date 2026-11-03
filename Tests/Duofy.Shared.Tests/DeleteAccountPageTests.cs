using System.Reflection;
using Bunit;
using Duofy.ApiClient;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace Duofy.Shared.Tests;

/// <summary>Die Seite /konto/loeschen (R5): Konto löschen nach erneuter Passwortabfrage.</summary>
public class DeleteAccountPageTests : PageTestContext
{
    [Fact]
    public void Page_is_at_the_agreed_route_and_needs_a_sign_in()
    {
        var route = typeof(DeleteAccount).GetCustomAttribute<RouteAttribute>();
        Assert.Equal("/konto/loeschen", route?.Template);
        Assert.NotNull(typeof(DeleteAccount).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void Right_password_deletes_the_account_and_goes_to_sign_in()
    {
        var cut = Render<DeleteAccount>();

        Field(cut, "delete-password").Input("ein-langes-passwort");
        Field(cut, "delete-submit").Click();

        cut.WaitForAssertion(() => Assert.Equal("/anmelden", CurrentPath));
        Assert.Equal(new DeleteMeRequest("ein-langes-passwort"), Assert.Single(Users.Deleted));
        Assert.Null(Tokens.Tokens);
    }

    [Fact]
    public void Wrong_password_shows_german_text_and_stays()
    {
        Users.DeleteResult = ApiResult.Failure(ErrorCodes.PasswordWrong);
        var cut = Render<DeleteAccount>();

        Field(cut, "delete-password").Input("falsch-falsch-1");
        Field(cut, "delete-submit").Click();

        cut.WaitForAssertion(() => Assert.Contains("Das Passwort stimmt nicht.", cut.Markup));
        Assert.NotNull(Tokens.Tokens);
    }

    [Fact]
    public void Empty_password_is_not_sent()
    {
        var cut = Render<DeleteAccount>();

        Field(cut, "delete-submit").Click();

        Assert.Empty(Users.Deleted);
    }
}

/// <summary>Konto löschen.</summary>
public class DeleteAccountPageBuildTests : PageTestContext
{
    [Fact]
    public void The_page_is_built_from_the_blocks_and_keeps_its_warning_field_and_buttons()
    {
        var cut = Render<DeleteAccount>();

        Assert.NotEmpty(cut.FindAll(".card"));
        Assert.Equal("alert", cut.Find("[role='alert']").GetAttribute("role"));
        Assert.Equal("password", Field(cut, "delete-password").GetAttribute("type"));
        Assert.Equal("current-password", Field(cut, "delete-password").GetAttribute("autocomplete"));
        Assert.Equal("delete-password-input", Field(cut, "delete-password").Id);
        Assert.Contains("duofy-button--danger", cut.Find("[data-testid='delete-submit']").ClassList);
        Assert.Equal("/konto", cut.Find("a[href='/konto']").GetAttribute("href"));
        Assert.Equal("Konto löschen", cut.Find("h1").TextContent.Trim());
    }

    [Fact]
    public void Enter_in_the_password_field_deletes_after_the_password_check()
    {
        var cut = Render<DeleteAccount>();
        Field(cut, "delete-password").Input("ein-langes-passwort");

        Field(cut, "delete-password").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        cut.WaitForAssertion(() => Assert.Equal("/anmelden", CurrentPath));
        Assert.Single(Users.Deleted);
    }

    [Fact]
    public void A_wrong_password_shows_its_text_in_a_second_alert()
    {
        Users.DeleteResult = ApiResult.Failure(ErrorCodes.PasswordWrong);
        var cut = Render<DeleteAccount>();
        Field(cut, "delete-password").Input("falsch-falsch-1");

        Field(cut, "delete-submit").Click();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[role='alert']").Count));
    }
}
