using Bunit;
using Duofy.ApiClient;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Microsoft.AspNetCore.Components;

namespace Duofy.Shared.Tests;

/// <summary>Die Registrierungsseite (R2): meldet nach dem Anlegen gleich an.</summary>
public class RegisterPageTests : PageTestContext
{
    private IRenderedComponent<Register> Fill(string password = "ein-langes-passwort", bool accept = true)
    {
        var cut = Render<Register>();
        Field(cut, "register-first-name").Change("Anna");
        Field(cut, "register-last-name").Change("Muster");
        Field(cut, "register-email").Change("anna@example.org");
        Field(cut, "register-password").Change(password);
        if (accept) Field(cut, "register-terms").Change(true);
        return cut;
    }

    [Fact]
    public void Valid_form_registers_then_signs_in_and_goes_to_start()
    {
        var cut = Fill();

        Field(cut, "register-submit").Click();

        cut.WaitForAssertion(() => Assert.Equal("/", CurrentPath));
        var request = Assert.Single(Auth.Registered);
        Assert.Equal(new RegisterRequest("Anna", "Muster", "anna@example.org", "ein-langes-passwort", true), request);
        Assert.Equal(new LoginRequest("anna@example.org", "ein-langes-passwort"), Assert.Single(Auth.LoggedIn));
    }

    // Spec 67, A18: Nach dem Registrieren geht es zurück zur Einladung, von der die Person kam.
    [Fact]
    public void A_local_return_address_is_followed_after_the_automatic_sign_in()
    {
        Navigation.NavigateTo(Navigation.GetUriWithQueryParameter("returnUrl", "/einladung/abc"));
        var cut = Fill();
        Assert.Equal("/anmelden?returnUrl=%2Feinladung%2Fabc", cut.Find("a[href^='/anmelden']").GetAttribute("href"));

        Field(cut, "register-submit").Click();

        cut.WaitForAssertion(() => Assert.Equal("/einladung/abc", CurrentPath));
    }

    [Fact]
    public void A_foreign_return_address_is_ignored()
    {
        Navigation.NavigateTo(Navigation.GetUriWithQueryParameter("returnUrl", "https://evil.example/"));
        var cut = Fill();

        Field(cut, "register-submit").Click();

        cut.WaitForAssertion(() => Assert.Equal("/", CurrentPath));
    }

    [Fact]
    public void Api_error_is_shown_as_german_text_and_nobody_is_signed_in()
    {
        Auth.RegisterResult = ApiResult.Failure(ErrorCodes.EmailTaken);
        var cut = Fill();

        Field(cut, "register-submit").Click();

        cut.WaitForAssertion(() => Assert.Contains("Diese E-Mail-Adresse ist schon registriert.", cut.Markup));
        Assert.Empty(Auth.LoggedIn);
    }

    [Fact]
    public void Too_short_password_is_not_sent()
    {
        var cut = Fill(password: "kurz");

        Field(cut, "register-submit").Click();

        cut.WaitForAssertion(() => Assert.Contains("mindestens 10 Zeichen", cut.Markup));
        Assert.Empty(Auth.Registered);
    }

    [Fact]
    public void Without_consent_nothing_is_sent()
    {
        var cut = Fill(accept: false);

        Field(cut, "register-submit").Click();

        cut.WaitForAssertion(() => Assert.Contains("Bitte stimme der Datenschutzerklärung", cut.Markup));
        Assert.Empty(Auth.Registered);
    }
}
