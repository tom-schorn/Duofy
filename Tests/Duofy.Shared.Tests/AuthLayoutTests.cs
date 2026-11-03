using Bunit;
using Duofy.Shared.Layout;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Duofy.Shared.Theme;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Anmelden und Registrieren sehen aus wie im Original (AuthLayout.tsx): keine App-Leiste,
/// keine Navigation, Texte aus der Ressourcendatei, umrandete Felder mit Beschriftung darüber,
/// Knöpfe in normaler Schreibung.
/// </summary>
public class AuthLayoutTests : PageTestContext
{
    public AuthLayoutTests()
    {
        Services.AddSingleton<IThemePreferenceStore>(new NoThemeStore());
    }

    // Über den echten Router, damit das @layout der Seite wirklich greift.
    private IRenderedComponent<Routes> Open(string path)
    {
        Navigation.NavigateTo(path);
        return Render<Routes>();
    }

    [Theory]
    [InlineData("/anmelden")]
    [InlineData("/registrieren")]
    public void Auth_pages_have_no_app_bar_and_no_drawer(string path)
    {
        var cut = Open(path);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".auth-layout")));
    }

    [Fact]
    public void Sign_in_shows_the_texts_of_the_original_from_the_resources()
    {
        var cut = Open("/anmelden");

        cut.WaitForAssertion(() => Assert.Equal("Willkommen zurück", cut.Find("h1").TextContent));
        Assert.Contains("Plan den Monat, bevor er anfängt.", cut.Markup);
        Assert.Contains("Noch kein Konto?", cut.Markup);
        Assert.Equal("/registrieren", cut.Find(".auth-switch a").GetAttribute("href"));
        Assert.Contains("Anmelden", Field(cut, "login-submit").ParentElement!.TextContent);
        // Seit Spec 53 gibt es "Passwort vergessen?", wie im Original.
        Assert.Equal("Passwort vergessen?", Field(cut, "login-forgot").TextContent.Trim());
    }

    [Fact]
    public void Register_shows_the_texts_of_the_original_from_the_resources()
    {
        var cut = Open("/registrieren");

        cut.WaitForAssertion(() => Assert.Equal("Konto anlegen", cut.Find("h1").TextContent));
        Assert.Contains("Dein Geld bleibt deins — der Haushalt kommt später dazu.", cut.Markup);
        Assert.Contains("Schon ein Konto?", cut.Markup);
        Assert.Equal("/anmelden", cut.Find(".auth-switch a").GetAttribute("href"));
    }

    [Fact]
    public void Brand_panel_carries_claim_pots_and_tagline()
    {
        var cut = Open("/anmelden");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".auth-brand")));
        var brand = cut.Find(".auth-brand");
        Assert.Contains("Duofy", brand.TextContent);
        Assert.Contains("Duofy plant Geld, es zählt es nicht.", brand.TextContent);
        Assert.Contains("Gemeinsam planen, getrennt besitzen.", brand.TextContent);
        Assert.Equal(["Grundbedarf", "Wünsche", "Sparen"], brand.QuerySelectorAll(".auth-pots li").Select(li => li.TextContent.Trim()));
        Assert.Equal(3, brand.QuerySelectorAll(".auth-pots .dot").Length);

        // Spec 82, R11: ein einziger Knopf für hell und dunkel.
        Assert.Single(cut.FindAll(".auth-mode-switch [data-testid='theme-toggle']"));
    }

    // Spec 70, A13 und R8: Die Links stehen unter Anmelden und Registrieren und gehen nur zu den Texten, die der Server anbietet.
    [Theory]
    [InlineData("/anmelden")]
    [InlineData("/registrieren")]
    public void The_footer_links_only_the_configured_legal_documents(string path)
    {
        LegalApiFake.Documents["imprint"] = "Max Muster";
        LegalApiFake.Documents["privacy"] = "Wir speichern wenig.";

        var cut = Open(path);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".auth-legal a")));
        var links = cut.Find(".auth-legal").QuerySelectorAll("a").ToArray();
        Assert.Equal(["Impressum", "Datenschutz"], links.Select(a => a.TextContent.Trim()));
        Assert.Equal(["/rechtliches/impressum", "/rechtliches/datenschutz"], links.Select(a => a.GetAttribute("href")));
        Assert.DoesNotContain("Nutzungsbedingungen", cut.Find(".auth-legal").TextContent);
    }

    [Fact]
    public void The_footer_shows_the_terms_as_nutzungsbedingungen_not_agb()
    {
        LegalApiFake.Documents["terms"] = "Bedingungen";

        var cut = Open("/anmelden");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".auth-legal a")));
        var link = Assert.Single(cut.Find(".auth-legal").QuerySelectorAll("a"));
        Assert.Equal("Nutzungsbedingungen", link.TextContent.Trim());
        Assert.Equal("/rechtliches/nutzungsbedingungen", link.GetAttribute("href"));
    }

    [Fact]
    public void The_footer_has_no_legal_links_without_configured_documents()
    {
        var none = Open("/anmelden");
        none.WaitForAssertion(() => Assert.NotEmpty(none.FindAll(".auth-brand")));
        Assert.Empty(none.FindAll(".auth-legal"));
    }

    // Spec 5, A1, geändert durch Spec 82 R11: Die Anmeldeseiten haben denselben einen Knopf für hell und dunkel.
    [Fact]
    public void A1_the_auth_pages_offer_the_one_theme_toggle()
    {
        var cut = Open("/anmelden");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".auth-mode-switch button")));

        var toggle = Assert.Single(cut.FindAll(".auth-mode-switch button"));
        Assert.Equal("theme-toggle", toggle.GetAttribute("data-testid"));
        Assert.Equal("false", toggle.GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Theme_switch_on_the_auth_pages_turns_dark_mode_on()
    {
        var cut = Open("/anmelden");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='theme-toggle']")));

        cut.Find("[data-testid='theme-toggle']").Click();

        Assert.True(cut.FindComponent<Duofy.Shared.Layout.ThemeVariables>().Instance.Dark);
    }

    private sealed class NoThemeStore : IThemePreferenceStore
    {
        public Task<ThemeChoice> LoadAsync() => Task.FromResult(ThemeChoice.Device);
        public Task SaveAsync(ThemeChoice choice) => Task.CompletedTask;
    }
}
