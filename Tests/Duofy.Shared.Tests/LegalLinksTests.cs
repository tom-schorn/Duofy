using Bunit;
using Duofy.Shared.Layout;
using Duofy.Shared.Services;
using Duofy.Shared.Theme;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>Die Zeile "Impressum · Datenschutz · Nutzungsbedingungen" auf jeder Seite (Spec 109, R1, R3, R4).</summary>
public class LegalLinksTests : PageTestContext
{
    public LegalLinksTests()
    {
        Services.AddSingleton<IThemePreferenceStore>(new NoThemeStore());
    }

    private IRenderedComponent<Routes> Open(string path)
    {
        Navigation.NavigateTo(path);
        return Render<Routes>();
    }

    private void AllThree()
    {
        LegalApiFake.Documents["imprint"] = "I";
        LegalApiFake.Documents["privacy"] = "P";
        LegalApiFake.Documents["terms"] = "T";
    }

    // A1
    [Theory]
    [InlineData("/anmelden")]
    [InlineData("/registrieren")]
    [InlineData("/")]
    [InlineData("/plan")]
    public void A1_every_layout_shows_the_legal_line(string path)
    {
        AllThree();

        var cut = Open(path);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='legal-links'] a")));
        var links = cut.Find("[data-testid='legal-links']").QuerySelectorAll("a");
        Assert.Equal(["Impressum", "Datenschutz", "Nutzungsbedingungen"], links.Select(a => a.TextContent.Trim()));
        Assert.Single(cut.FindAll("[data-testid='legal-links']"));
    }

    // A1: Auf Anmelden und Registrieren steht die Zeile gleich unter dem Formular, vor der Serverzeile.
    [Theory]
    [InlineData("/anmelden")]
    [InlineData("/registrieren")]
    public void A1_on_the_auth_pages_the_line_is_the_next_element_after_the_form(string path)
    {
        AllThree();

        var cut = Open(path);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='legal-links']")));
        var next = cut.Find(".auth-form").NextElementSibling;
        Assert.Equal("legal-links", next!.GetAttribute("data-testid"));
    }

    // A3
    [Fact]
    public void A3_an_external_address_replaces_the_page_of_the_server_for_that_text_only()
    {
        AllThree();
        LegalApiFake.Urls["imprint"] = "https://example.org/impressum";

        var cut = Open("/anmelden");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='legal-link-imprint']")));
        var imprint = cut.Find("[data-testid='legal-link-imprint']");
        Assert.Equal("https://example.org/impressum", imprint.GetAttribute("href"));
        Assert.Contains("noopener", imprint.GetAttribute("rel"));
        Assert.Equal("/rechtliches/datenschutz", cut.Find("[data-testid='legal-link-privacy']").GetAttribute("href"));
    }

    [Fact]
    public void A3_without_an_address_the_link_goes_to_the_page_of_the_server()
    {
        AllThree();

        var cut = Open("/anmelden");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='legal-link-imprint']")));
        Assert.Equal("/rechtliches/impressum", cut.Find("[data-testid='legal-link-imprint']").GetAttribute("href"));
    }

    // Was kein https ist, wird nie ein Link, auch wenn ein Server es schickt (kein javascript:).
    [Fact]
    public void An_address_that_is_not_https_is_ignored_and_the_page_of_the_server_is_used()
    {
        AllThree();
        LegalApiFake.Urls["privacy"] = "javascript:alert(1)";

        var cut = Open("/anmelden");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='legal-link-privacy']")));
        Assert.Equal("/rechtliches/datenschutz", cut.Find("[data-testid='legal-link-privacy']").GetAttribute("href"));
    }

    // A5
    [Theory]
    [InlineData("/anmelden")]
    [InlineData("/plan")]
    public void A5_missing_texts_are_left_out(string path)
    {
        LegalApiFake.Documents["imprint"] = "I";
        LegalApiFake.Documents["privacy"] = "P";

        var cut = Open(path);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='legal-links'] a")));
        Assert.DoesNotContain("Nutzungsbedingungen", cut.Find("[data-testid='legal-links']").TextContent);
        Assert.Empty(cut.FindAll("[data-testid='legal-link-terms']"));
    }

    [Fact]
    public void Without_any_text_there_is_no_line()
    {
        var cut = Open("/anmelden");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".auth-brand")));
        Assert.Empty(cut.FindAll("[data-testid='legal-links']"));
    }

    private sealed class NoThemeStore : IThemePreferenceStore
    {
        public Task<ThemeChoice> LoadAsync() => Task.FromResult(ThemeChoice.Device);
        public Task SaveAsync(ThemeChoice choice) => Task.CompletedTask;
    }
}
