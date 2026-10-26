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

        Field(cut, "delete-password").Change("ein-langes-passwort");
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

        Field(cut, "delete-password").Change("falsch-falsch-1");
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
