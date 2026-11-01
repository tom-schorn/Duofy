using Bunit;
using Duofy.Shared.Layout;
using Duofy.Shared.Services;
using Duofy.Shared.Theme;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Duofy.Shared.Tests;

/// <summary>
/// Issue 5, offener Punkt: Die gewählte Hell-Dunkel-Einstellung gilt schon beim ersten Zeichnen, ohne kurzes Aufblitzen
/// der falschen Farbe. Im Browser sorgt ein kleines eigenes Skript im Kopf der Seite dafür, in der App liest das Layout
/// die Preferences früh. Den echten Browser und das Telefon prüfen diese Tests nicht; sie prüfen das Skript, den Stil und die Reihenfolge im Layout.
/// </summary>
public class ThemeFirstPaintTests : UiTestContext
{
    public ThemeFirstPaintTests() => AddAuthorization();

    // --- Web: das Skript im Kopf ---------------------------------------------------------------------------

    [Fact]
    public void The_head_script_reads_the_stored_choice_and_the_device_and_sets_one_class_on_html()
    {
        var script = ThemeBoot.Script;

        Assert.Contains($"localStorage.getItem('{LocalStorageThemePreferenceStore.StorageKey}')", script);
        Assert.Contains("matchMedia('(prefers-color-scheme: dark)')", script);
        Assert.Contains($"document.documentElement.classList.add('{ThemeBoot.DarkClass}')", script);
        Assert.Contains("v==='true'", script);
        Assert.Contains("v!=='false'", script);
        Assert.Contains("catch(e)", script);
    }

    [Fact]
    public void The_head_script_is_our_own_and_loads_nothing_from_outside()
    {
        var markup = ThemeBoot.HeadMarkup;

        Assert.DoesNotContain("src=", markup);
        Assert.DoesNotContain("http", markup);
        Assert.DoesNotContain("eval", markup);
    }

    [Fact]
    public void The_head_style_uses_the_dark_colours_of_the_theme_under_the_class()
    {
        var style = ThemeBoot.Style;
        var dark = DuofyTheme.Instance.PaletteDark;

        Assert.Contains($"html.{ThemeBoot.DarkClass}", style);
        Assert.Contains("color-scheme:dark", style);
        Assert.Contains($"--mud-palette-background:#{dark.Background.R:x2}{dark.Background.G:x2}{dark.Background.B:x2}", style);
        Assert.Contains("--mud-palette-text-primary:", style);
    }

    [Fact]
    public void The_web_page_puts_the_script_in_the_head_before_the_first_stylesheet()
    {
        var app = File.ReadAllText(RepositoryFiles.Path("Web", "Duofy.Web", "Components", "App.razor"));

        var head = app.IndexOf("ThemeBoot.HeadMarkup", StringComparison.Ordinal);
        Assert.True(head > app.IndexOf("<head>", StringComparison.Ordinal));
        Assert.True(head < app.IndexOf("<link rel=\"stylesheet\"", StringComparison.Ordinal));
    }

    // --- Layout ------------------------------------------------------------------------------------------

    [Fact]
    public void The_layout_drops_the_early_class_once_it_draws_in_the_right_colour_itself()
    {
        Services.AddSingleton<IThemePreferenceStore>(new Store(ThemeChoice.Dark));

        var cut = Render<MainLayout>();

        cut.WaitForAssertion(() => Assert.True(cut.FindComponent<MudThemeProvider>().Instance.IsDarkMode));
        cut.WaitForAssertion(() => Assert.Contains(JSInterop.Invocations,
            i => i.Identifier == ThemeBoot.RemoveFunction && i.Arguments.Count == 1 && (string?)i.Arguments[0] == ThemeBoot.DarkClass));
    }

    // --- App: Preferences vor dem ersten Zeichnen ------------------------------------------------------------

    [Theory]
    [InlineData(ThemeChoice.Dark, false, true)]
    [InlineData(ThemeChoice.Light, true, false)]
    [InlineData(ThemeChoice.Device, true, true)]
    [InlineData(ThemeChoice.Device, false, false)]
    public void A_store_that_knows_the_choice_at_once_makes_the_layout_draw_it_in_the_first_render(ThemeChoice choice, bool systemIsDark, bool expectedDark)
    {
        // LoadAsync kommt nie zurück: Was das Layout zeigt, hat es aus LoadImmediately, also vor dem ersten Zeichnen.
        var store = new Store(choice, systemIsDark, neverLoadsLater: true);
        Services.AddSingleton<IThemePreferenceStore>(store);

        var cut = Render<MainLayout>();

        Assert.Equal(expectedDark, cut.FindComponent<MudThemeProvider>().Instance.IsDarkMode);
        Assert.Equal(expectedDark, cut.Markup.Contains("--mud-palette-background: rgba(15,23,41,1)", StringComparison.Ordinal));
    }

    [Fact]
    public void A_browser_store_has_nothing_at_once_and_the_layout_starts_light_until_it_has_asked()
    {
        var store = new Store(ThemeChoice.Dark, immediate: false, neverLoadsLater: true);
        Services.AddSingleton<IThemePreferenceStore>(store);

        var cut = Render<MainLayout>();

        Assert.False(cut.FindComponent<MudThemeProvider>().Instance.IsDarkMode);
    }

    [Fact]
    public void The_app_store_gives_preferences_and_system_theme_at_once()
    {
        var source = File.ReadAllText(RepositoryFiles.Path("Mobile", "Duofy", "Services", "PreferencesThemePreferenceStore.cs"));

        Assert.Contains("public ThemeStart? LoadImmediately()", source);
        Assert.Contains("AppInfo.Current.RequestedTheme == AppTheme.Dark", source);
    }

    [Fact]
    public void The_app_start_page_has_a_dark_background_for_a_dark_device_before_the_layout_draws()
    {
        var css = File.ReadAllText(RepositoryFiles.Path("Mobile", "Duofy", "wwwroot", "app.css"));

        Assert.Contains("@media (prefers-color-scheme: dark)", css);
        Assert.Contains("background-color: #0f1729", css);
    }

    private sealed class Store(ThemeChoice choice, bool systemIsDark = false, bool immediate = true, bool neverLoadsLater = false) : IThemePreferenceStore
    {
        public Task<ThemeChoice> LoadAsync() => neverLoadsLater ? new TaskCompletionSource<ThemeChoice>().Task : Task.FromResult(choice);

        public Task SaveAsync(ThemeChoice value) => Task.CompletedTask;

        public ThemeStart? LoadImmediately() => immediate ? new ThemeStart(choice, systemIsDark) : null;
    }
}
