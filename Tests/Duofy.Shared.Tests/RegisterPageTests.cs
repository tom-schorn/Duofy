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
        if (accept) Field(cut, "register-terms").Click();
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

    // Die Registrierung ist aus den Bausteinen gebaut, die Prüfungen melden dieselben Texte unter den Feldern.
    [Fact]
    public void The_page_is_built_from_the_blocks_and_keeps_its_field_types_and_hints()
    {
        var cut = Render<Register>();

        Assert.Equal("email", Field(cut, "register-email").GetAttribute("type"));
        Assert.Equal("password", Field(cut, "register-password").GetAttribute("type"));
        Assert.Equal("new-password", Field(cut, "register-password").GetAttribute("autocomplete"));
        Assert.Equal("given-name", Field(cut, "register-first-name").GetAttribute("autocomplete"));
        Assert.Equal("family-name", Field(cut, "register-last-name").GetAttribute("autocomplete"));
        Assert.Equal("register-password-input", Field(cut, "register-password").Id);
        Assert.Equal(["register-first-name-input", "register-last-name-input", "register-email-input", "register-password-input", "register-terms"],
            cut.FindAll("label.field-label").Select(l => l.GetAttribute("for")!));
        Assert.False(string.IsNullOrWhiteSpace(cut.Find("#register-password-hint").TextContent));
        Assert.Equal("register-password-hint", Field(cut, "register-password").GetAttribute("aria-describedby"));
    }

    [Fact]
    public void An_empty_form_names_every_problem_under_its_field_and_sends_nothing()
    {
        var cut = Render<Register>();

        Field(cut, "register-submit").Click();

        cut.WaitForAssertion(() => Assert.Equal(5, cut.FindAll(".field-error").Count));
        var messages = cut.FindAll(".field-error").Select(m => m.TextContent.Trim()).ToList();
        Assert.Contains(messages, m => m.Contains("Vorname"));
        Assert.Contains(messages, m => m.Contains("Nachname"));
        Assert.Contains(messages, m => m.Contains("E-Mail"));
        Assert.Contains(messages, m => m.Contains("mindestens 10 Zeichen"));
        Assert.Contains(messages, m => m.Contains("Datenschutzerklärung"));
        Assert.Empty(Auth.Registered);
    }

    [Fact]
    public void A_wrong_email_form_is_named_and_the_message_goes_once_the_field_is_right()
    {
        var cut = Fill();
        Field(cut, "register-email").Change("keine-adresse");

        Field(cut, "register-submit").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".field-error")));
        Assert.Empty(Auth.Registered);

        Field(cut, "register-email").Change("anna@example.org");
        Field(cut, "register-submit").Click();

        cut.WaitForAssertion(() => Assert.Single(Auth.Registered));
    }

    // Mangel S2: Der Haken hat einen Namen: Die Beschriftung mit dem Text der Zustimmung ist über for und id mit ihm verbunden.
    [Fact]
    public void The_consent_checkbox_has_the_consent_text_as_its_name()
    {
        var cut = Render<Register>();

        var box = cut.Find("[data-testid='register-terms']");
        Assert.Equal("register-terms", box.Id);
        Assert.Equal("BUTTON", box.TagName);
        Assert.Contains("Datenschutz", cut.Find("label[for='register-terms']").TextContent);
    }

    // Mangel S3: Ein falsches Feld sagt es dem Screenreader (aria-invalid, aria-describedby auf den Fehlertext mit role=alert),
    // und der Fokus springt auf das erste falsche Feld.
    [Fact]
    public void A_wrong_field_is_marked_invalid_points_to_its_alert_and_takes_the_focus()
    {
        var focus = JSInterop.SetupModule("./_content/Duofy.Shared/focus-notice.js");
        var cut = Render<Register>();
        Field(cut, "register-first-name").Change("Anna");
        Field(cut, "register-last-name").Change("Muster");
        Field(cut, "register-email").Change("keine-adresse");
        Field(cut, "register-password").Change("kurz");
        Field(cut, "register-terms").Click();

        Field(cut, "register-submit").Click();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".field-error").Count));
        var email = Field(cut, "register-email");
        Assert.Equal("true", email.GetAttribute("aria-invalid"));
        var error = cut.Find("#" + email.GetAttribute("aria-describedby"));
        Assert.Equal("alert", error.GetAttribute("role"));
        Assert.Contains("E-Mail", error.TextContent);
        var password = Field(cut, "register-password");
        Assert.Equal("true", password.GetAttribute("aria-invalid"));
        Assert.Contains("register-password-hint", password.GetAttribute("aria-describedby"));
        Assert.NotEqual("true", Field(cut, "register-first-name").GetAttribute("aria-invalid"));
        cut.WaitForAssertion(() => Assert.Equal("register-email-input", focus.VerifyInvoke("focusById").Arguments[0]));
    }
}
