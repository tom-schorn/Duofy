using Bunit;
using Duofy.Shared.Layout;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Duofy.Shared.Theme;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

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
        Assert.Empty(cut.FindComponents<MudAppBar>());
        Assert.Empty(cut.FindComponents<MudDrawer>());
        Assert.Empty(cut.FindComponents<MudNavMenu>());
    }

    [Fact]
    public void Other_pages_keep_app_bar_and_show_the_bottom_bar_in_the_app()
    {
        var cut = Open("/konto");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<MudAppBar>()));
        Assert.NotEmpty(cut.FindAll("[data-testid='bottom-bar']"));
        Assert.Empty(cut.FindComponents<MudNavMenu>());
        Assert.Empty(cut.FindAll(".auth-layout"));
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
        Assert.Equal(3, brand.QuerySelectorAll(".auth-pot-square").Length);

        Assert.NotEmpty(cut.FindAll(".auth-mode-switch .mode-switch"));
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

    [Fact]
    public void Theme_switch_on_the_auth_pages_turns_dark_mode_on()
    {
        var cut = Open("/anmelden");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mode-switch")));

        cut.Find(".mode-switch").Click();

        Assert.True(cut.FindComponent<MudThemeProvider>().Instance.IsDarkMode);
    }

    [Theory]
    [InlineData("/anmelden")]
    [InlineData("/registrieren")]
    [InlineData("/konto")]
    [InlineData("/konto/loeschen")]
    public void Every_text_field_is_outlined_with_its_label_above(string path)
    {
        var cut = Open(path);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<MudTextField<string>>()));

        var fields = cut.FindComponents<MudTextField<string>>();
        Assert.All(fields, field => Assert.Equal(Variant.Outlined, field.Instance.Variant));
        // Zahlenfelder wie die Quoten auf /konto folgen derselben Regel.
        var numbers = cut.FindComponents<MudNumericField<int>>();
        Assert.All(numbers, field => Assert.Equal(Variant.Outlined, field.Instance.Variant));

        // Die Beschriftung steht als eigenes <label> darüber und gehört per for/id zu je einem Feld.
        var labels = cut.FindAll("label.field-label");
        Assert.Equal(fields.Count + numbers.Count, labels.Count);
        Assert.All(labels, label =>
        {
            var target = cut.Find($"input[id='{label.GetAttribute("for")}'], textarea[id='{label.GetAttribute("for")}']");
            Assert.NotNull(target);
        });
    }

    [Fact]
    public void Theme_sets_buttons_in_normal_case_and_the_radius_of_the_original()
    {
        Assert.Equal("none", DuofyTheme.Instance.Typography.Button.TextTransform);
        Assert.Equal("0.5rem", DuofyTheme.Instance.LayoutProperties.DefaultBorderRadius);

        var cut = Render<MudThemeProvider>(p => p.Add(x => x.Theme, DuofyTheme.Instance));
        Assert.Contains("text-transform: none", cut.Markup);
        Assert.Contains("--mud-default-borderradius: 0.5rem", cut.Markup);
    }

    private sealed class NoThemeStore : IThemePreferenceStore
    {
        public Task<bool?> LoadIsDarkAsync() => Task.FromResult<bool?>(null);
        public Task SaveIsDarkAsync(bool isDark) => Task.CompletedTask;
    }
}
