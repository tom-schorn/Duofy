using Bunit;
using Duofy.ApiClient;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Microsoft.AspNetCore.Components;

namespace Duofy.Shared.Tests;

/// <summary>Die Anmeldeseite (R3) mit dem Rücksprung auf die Seite, von der die Person kam.</summary>
public class LoginPageTests : PageTestContext
{
    private IRenderedComponent<Login> Open(string? returnUrl = null)
    {
        // Der Rücksprung-Parameter kommt aus der Adresse, also navigieren wir erst dorthin.
        if (returnUrl is not null)
        {
            Navigation.NavigateTo(Navigation.GetUriWithQueryParameter("returnUrl", returnUrl));
        }
        var cut = Render<Login>();
        Field(cut, "login-email").Change("anna@example.org");
        Field(cut, "login-password").Change("ein-langes-passwort");
        return cut;
    }

    [Fact]
    public void Successful_sign_in_goes_to_start()
    {
        var cut = Open();

        Field(cut, "login-submit").Click();

        cut.WaitForAssertion(() => Assert.Equal("/", CurrentPath));
        Assert.Equal(new LoginRequest("anna@example.org", "ein-langes-passwort"), Assert.Single(Auth.LoggedIn));
    }

    [Fact]
    public void Local_return_address_is_followed()
    {
        var cut = Open("/konto/loeschen");

        Field(cut, "login-submit").Click();

        cut.WaitForAssertion(() => Assert.Equal("/konto/loeschen", CurrentPath));
    }

    [Theory]
    [InlineData("https://evil.example/")]
    [InlineData("//evil.example/")]
    [InlineData("\\\\evil.example")]
    [InlineData("konto")]
    public void Foreign_return_address_is_ignored(string returnUrl)
    {
        var cut = Open(returnUrl);

        Field(cut, "login-submit").Click();

        cut.WaitForAssertion(() => Assert.Equal("/", CurrentPath));
    }

    [Fact]
    public void Failed_sign_in_shows_one_message_and_stays()
    {
        Auth.LoginResult = ApiResult.Failure(ErrorCodes.LoginFailed);
        var cut = Open();

        Field(cut, "login-submit").Click();

        cut.WaitForAssertion(() => Assert.Contains("E-Mail-Adresse oder Passwort stimmt nicht.", cut.Markup));
        Assert.DoesNotContain("/konto", CurrentPath);
    }

    [Fact]
    public void Sign_in_without_cookies_asks_to_allow_them()
    {
        Auth.LoginResult = ApiResult.Failure(ClientErrorCodes.CookiesRequired);
        var cut = Open();

        Field(cut, "login-submit").Click();

        cut.WaitForAssertion(() => Assert.Contains("Bitte Cookies erlauben, um dich anzumelden.", cut.Markup));
    }

    // Spec 67, A18: Wer von einer Einladung kommt und sich erst registriert, landet danach wieder dort.
    [Fact]
    public void The_link_to_the_registration_keeps_the_return_address()
    {
        var cut = Open("/einladung/abc");

        var link = cut.Find("a[href^='/registrieren']");

        Assert.Equal("/registrieren?returnUrl=%2Feinladung%2Fabc", link.GetAttribute("href"));
    }
}
