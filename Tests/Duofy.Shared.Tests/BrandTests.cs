using Bunit;
using Duofy.Shared.Layout;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Name und Logo der Anwendung (Issue 8): Eine selbst betriebene Instanz heißt Duofy und zeigt nur den Namen, die
/// gehostete heißt Plagevo und zeigt das Logo "Offenes P" mit der Wortmarke, hell und dunkel passend.
/// </summary>
public class BrandTests : PageTestContext
{
    private ThemeChoice theme = ThemeChoice.Light;

    public BrandTests()
    {
        Services.AddSingleton<IThemePreferenceStore>(new FixedThemeStore(this));
    }

    private void UsePlagevo() => Services.AddSingleton(AppBrand.Plagevo);

    private IRenderedComponent<Routes> Open(string path)
    {
        Navigation.NavigateTo(path);
        return Render<Routes>();
    }

    // --- Die Auswahl der Marke -------------------------------------------------------------------------------

    [Theory]
    [InlineData(null, false, "Duofy", false)]
    [InlineData("", false, "Duofy", false)]
    [InlineData(null, true, "Plagevo", true)]
    [InlineData("Plagevo", false, "Plagevo", true)]
    [InlineData("plagevo", false, "Plagevo", true)]
    [InlineData("Duofy", true, "Duofy", false)]
    [InlineData("Unser Haushalt", true, "Unser Haushalt", false)]
    public void The_brand_follows_the_setting_and_otherwise_the_hosted_flag(string? name, bool hosted, string expectedName, bool expectedLogo)
    {
        var brand = AppBrand.FromSettings(name, hosted);

        Assert.Equal(expectedName, brand.Name);
        Assert.Equal(expectedLogo, brand.HasLogo);
    }

    [Fact]
    public void Every_file_a_brand_names_exists_in_the_shared_assets()
    {
        foreach (var path in new[] { AppBrand.Plagevo.WordmarkLight, AppBrand.Plagevo.WordmarkDark, AppBrand.Plagevo.SymbolLight, AppBrand.Plagevo.SymbolDark, AppBrand.Plagevo.FaviconLight, AppBrand.Plagevo.FaviconDark })
        {
            Assert.NotNull(path);
            var file = RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", path!.Replace("_content/Duofy.Shared/", "", StringComparison.Ordinal));
            Assert.True(File.Exists(file), $"Fehlt: {file}");
        }
        Assert.True(File.Exists(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "favicon.png")));
    }

    // Die dunkle Wortmarke steht auf der Fläche der Leiste und hat keinen eigenen Hintergrund.
    [Fact]
    public void The_dark_wordmark_has_no_background_of_its_own()
    {
        var svg = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "brand", "plagevo-wortmarke-dunkel.svg"));

        Assert.DoesNotContain("<rect", svg);
    }

    // --- Anmeldeseite ----------------------------------------------------------------------------------------

    [Fact]
    public void Sign_in_of_the_hosted_instance_shows_the_plagevo_wordmark_on_the_brand_panel_and_above_the_form()
    {
        UsePlagevo();

        var cut = Open("/anmelden");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".auth-brand")));
        var panel = Assert.Single(cut.FindAll(".auth-brand img[data-testid='brand-logo']"));
        Assert.EndsWith("brand/plagevo-wortmarke-dunkel.svg", panel.GetAttribute("src"));
        Assert.Equal("Plagevo", panel.GetAttribute("alt"));
        var above = Assert.Single(cut.FindAll(".auth-mobile-brand img[data-testid='brand-logo']"));
        Assert.EndsWith("brand/plagevo-wortmarke-hell.svg", above.GetAttribute("src"));
        Assert.Contains("Plagevo plant Geld, es zählt es nicht.", cut.Find(".auth-brand").TextContent);
        Assert.DoesNotContain("Duofy", cut.Find(".auth-layout").TextContent);
    }

    [Fact]
    public void Sign_in_above_the_form_follows_the_dark_mode()
    {
        UsePlagevo();
        theme = ThemeChoice.Dark;

        var cut = Open("/anmelden");

        cut.WaitForAssertion(() =>
            Assert.EndsWith("brand/plagevo-wortmarke-dunkel.svg", cut.Find(".auth-mobile-brand img").GetAttribute("src")));
    }

    [Fact]
    public void Sign_in_of_a_self_hosted_instance_shows_the_name_duofy_and_no_logo()
    {
        var cut = Open("/anmelden");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".auth-brand")));
        Assert.Empty(cut.FindAll("[data-testid='brand-logo']"));
        Assert.Contains("Duofy", cut.Find(".auth-brand").TextContent);
        Assert.Contains("Duofy plant Geld, es zählt es nicht.", cut.Find(".auth-brand").TextContent);
    }

    [Theory]
    [InlineData("/anmelden", "Willkommen zurück", false)]
    [InlineData("/anmelden", "Willkommen zurück", true)]
    [InlineData("/registrieren", "Konto anlegen", false)]
    public void The_browser_title_of_the_auth_pages_names_the_brand(string path, string pageTitle, bool hosted)
    {
        if (hosted)
        {
            UsePlagevo();
        }

        var cut = Open(path);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".auth-layout")));
        var title = Assert.Single(cut.FindComponents<PageTitle>().Where(t => Content(t).StartsWith(pageTitle, StringComparison.Ordinal)));
        Assert.Equal($"{pageTitle} · {(hosted ? "Plagevo" : "Duofy")}", Content(title));
    }

    // Der Titel steckt im Inhalt des Bausteins; er wird erst im Kopf der Seite (HeadOutlet) ausgegeben.
    private string Content(IRenderedComponent<PageTitle> title) => Render(title.Instance.ChildContent!).Markup.Trim();

    // --- Kopfzeile -------------------------------------------------------------------------------------------

    [Fact]
    public void The_app_bar_of_the_browser_shows_the_plagevo_wordmark_in_light_mode()
    {
        UsePlagevo();
        AddAuthorization();

        var cut = Render<MainLayout>();

        var logo = Assert.Single(cut.FindAll(".mud-appbar img[data-testid='brand-logo']"));
        // Die Leiste ist auch im hellen Modus dunkel, also das Logo für dunklen Grund.
        Assert.EndsWith("brand/plagevo-wortmarke-dunkel.svg", logo.GetAttribute("src"));
        Assert.Equal("Plagevo", logo.GetAttribute("alt"));
    }

    [Fact]
    public void The_app_bar_of_the_browser_shows_the_dark_wordmark_in_dark_mode()
    {
        UsePlagevo();
        AddAuthorization();
        theme = ThemeChoice.Dark;

        var cut = Render<MainLayout>();

        cut.WaitForAssertion(() =>
            Assert.EndsWith("brand/plagevo-wortmarke-dunkel.svg", cut.Find(".mud-appbar img[data-testid='brand-logo']").GetAttribute("src")));
    }

    [Fact]
    public void The_app_bar_of_a_self_hosted_instance_shows_the_name_only()
    {
        AddAuthorization();

        var cut = Render<MainLayout>();

        Assert.Empty(cut.FindAll("[data-testid='brand-logo']"));
        Assert.Equal("Duofy", cut.Find(".mud-appbar .app-title").TextContent.Trim());
    }

    [Fact]
    public void The_app_bar_of_the_app_shows_the_plagevo_symbol_before_the_title_of_the_page()
    {
        UsePlagevo();
        Services.AddSingleton<IAppHost, AppHost>();

        var cut = Open("/konto");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-appbar img[data-testid='brand-logo']")));
        var symbol = cut.Find(".mud-appbar img[data-testid='brand-logo']");
        Assert.EndsWith("brand/plagevo-symbol-dunkel.svg", symbol.GetAttribute("src"));
        Assert.Equal("", symbol.GetAttribute("alt"));
        Assert.NotEmpty(cut.Find("[data-testid='app-title']").TextContent);
    }

    private sealed class FixedThemeStore(BrandTests test) : IThemePreferenceStore
    {
        public Task<ThemeChoice> LoadAsync() => Task.FromResult(test.theme);
        public Task SaveAsync(ThemeChoice choice) => Task.CompletedTask;
    }
}
