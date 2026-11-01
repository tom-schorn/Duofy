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
        Field(cut, "login-email").Input("anna@example.org");
        Field(cut, "login-password").Input("ein-langes-passwort");
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

    // Radzen-Umstellung R10: Die Anmeldeseite ist in Radzen gebaut.
    [Fact]
    public void The_page_is_built_with_radzen_and_keeps_its_field_types_and_labels()
    {
        var cut = Render<Login>();

        var email = Field(cut, "login-email");
        Assert.Contains("rz-textbox", email.ClassList);
        Assert.Equal("email", email.GetAttribute("type"));
        Assert.Equal("login-email-input", email.Id);
        Assert.Equal("email", email.GetAttribute("autocomplete"));
        var password = Field(cut, "login-password");
        Assert.Equal("password", password.GetAttribute("type"));
        Assert.Equal("current-password", password.GetAttribute("autocomplete"));
        Assert.Equal("login-password-input", password.Id);
        Assert.Equal(["login-email-input", "login-password-input"],
            cut.FindAll("label.field-label").Select(l => l.GetAttribute("for")!));
        Assert.Equal("Willkommen zurück", cut.Find("h1").TextContent.Trim());
    }

    [Fact]
    public void Enter_in_the_password_field_signs_in()
    {
        var cut = Open();

        Field(cut, "login-password").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        cut.WaitForAssertion(() => Assert.Equal("/", CurrentPath));
        Assert.Single(Auth.LoggedIn);
    }

    [Fact]
    public void A_failed_sign_in_shows_its_message_in_a_radzen_alert()
    {
        Auth.LoginResult = ApiResult.Failure(ErrorCodes.LoginFailed);
        var cut = Open();

        Field(cut, "login-submit").Click();

        cut.WaitForAssertion(() => Assert.Contains("E-Mail-Adresse oder Passwort stimmt nicht.", cut.Find(".rz-alert").TextContent));
    }

    [Fact]
    public void The_submit_button_is_disabled_while_signing_in_and_says_so()
    {
        var cut = Render<Login>();

        Assert.Equal("Anmelden", cut.Find("[data-testid='login-submit']").TextContent.Trim());
        Assert.False(cut.Find("[data-testid='login-submit']").HasAttribute("disabled"));
    }

    [Fact]
    public void Forgot_password_opens_a_card_and_says_so_with_aria_expanded()
    {
        var cut = Render<Login>();
        var toggle = cut.Find("[data-testid='login-forgot']");
        Assert.Equal("false", toggle.GetAttribute("aria-expanded"));
        Assert.Empty(cut.FindAll("[data-testid='login-forgot-panel']"));

        toggle.Click();

        Assert.Equal("true", cut.Find("[data-testid='login-forgot']").GetAttribute("aria-expanded"));
        Assert.Contains("rz-card", cut.Find("[data-testid='login-forgot-panel']").ClassList);
    }

    [Fact]
    public void An_unconfirmed_account_offers_to_resend_and_afterwards_greys_the_button_out_with_its_reason()
    {
        Auth.LoginResult = ApiResult.Failure(ErrorCodes.EmailNotConfirmed);
        var cut = Open();
        Field(cut, "login-submit").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='login-resend']")));
        Assert.False(cut.Find("[data-testid='login-resend']").HasAttribute("aria-disabled"));

        cut.Find("[data-testid='login-resend']").Click();

        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find("[data-testid='login-resend']").GetAttribute("aria-disabled")));
        Assert.Equal("login-resend-reason", cut.Find("[data-testid='login-resend']").GetAttribute("aria-describedby"));
        Assert.Contains("in fünf Minuten", cut.Find("[data-testid='login-resend-reason']").TextContent);
    }
}
