using System.Globalization;
using System.Text.RegularExpressions;
using Bunit;
using Duofy.Shared.Layout;
using Duofy.Shared.Services;
using Duofy.Shared.Theme;
using Microsoft.Extensions.DependencyInjection;
using Radzen.Blazor;

namespace Duofy.Shared.Tests;

/// <summary>
/// Radzen R3 (#55): Das freigegebene Aussehen steht an einer Stelle als --rz-*-Werte. Palette "Altes Duofy", 12 px Rundung,
/// Menü ohne Trenner und Balken, Inter, bestätigtes Dunkel-Design, Kontraste mindestens 4,5 zu 1 und Linien 3 zu 1.
/// </summary>
public class ThemeVariablesTests : UiTestContext
{
    private static string Light(string name) => RadzenVariables.Light[name];

    private static string Dark(string name) => RadzenVariables.Dark[name];

    // --- Toms freigegebene Werte --------------------------------------------------------------------------------

    // Issue #76: Die Höhe der Fortschrittsbalken steht einmal im Theme, nicht an jedem Balken.
    [Fact]
    public void Progress_bars_get_their_height_and_corners_from_the_theme()
    {
        Assert.Equal("0.5rem", RadzenVariables.Common["--rz-progressbar-height"]);
        Assert.Equal("var(--rz-border-radius)", RadzenVariables.Common["--rz-progressbar-border-radius"]);
    }

    [Fact]
    public void Light_has_the_palette_altes_duofy()
    {
        Assert.Equal("#1e3a5f", Light("--rz-primary"));
        Assert.Equal("#fbf9f4", Light("--rz-body-background-color"));
        Assert.Equal("#ffffff", Light("--rz-card-background-color"));
        Assert.Equal("#2b6cb0", Light("--rz-info"));
        Assert.Equal("#c68a0e", Light("--rz-warning"));
        Assert.Equal("#4f9e6a", Light("--rz-success"));
        Assert.Equal("#a63a2e", Light("--rz-danger"));
    }

    [Fact]
    public void Dark_has_the_confirmed_design()
    {
        Assert.Equal("#0b0e14", Dark("--rz-body-background-color"));
        Assert.Equal("#141922", Dark("--rz-card-background-color"));
        Assert.Equal("#86ace0", Dark("--rz-primary"));
    }

    [Theory]
    [InlineData("--rz-border-radius")]
    [InlineData("--rz-card-border-radius")]
    [InlineData("--rz-button-border-radius")]
    [InlineData("--rz-input-border-radius")]
    [InlineData("--rz-panel-menu-item-border-radius")]
    [InlineData("--rz-menu-item-border-radius")]
    [InlineData("--rz-dialog-border-radius")]
    [InlineData("--rz-alert-border-radius")]
    public void Every_corner_is_12_px(string name) => Assert.Equal("12px", RadzenVariables.Common[name]);

    [Theory]
    [InlineData("--rz-sidebar-border-inline-end", "none")]
    [InlineData("--rz-panel-menu-item-border", "none")]
    [InlineData("--rz-panel-menu-item-active-indicator", "transparent")]
    [InlineData("--rz-panel-menu-item-background-color", "transparent")]
    public void The_menu_has_no_dividers_and_no_bar_on_the_active_item(string name, string value) =>
        Assert.Equal(value, RadzenVariables.Common[name]);

    // Kopfleiste 1300, untere Leiste 1200: Dialog und Abdunklung liegen über beiden.
    [Fact]
    public void Dialogs_lie_above_the_top_bar_and_the_bottom_bar()
    {
        Assert.Equal("1400", RadzenVariables.Common["--rz-dialog-mask-zindex"]);
        Assert.Equal("1401", RadzenVariables.Common["--rz-dialog-zindex"]);
    }

    [Fact]
    public void Outlined_cards_have_a_visible_frame_in_both_modes()
    {
        // Radzen zeichnet umrandete Karten ohne Fläche: Ohne Rahmen wären sie nicht zu sehen (Mia, 01.11.2026).
        Assert.Equal("var(--rz-border-width) solid #c3cad4", RadzenVariables.Light["--rz-card-border"]);
        Assert.Equal("var(--rz-border-width) solid #3a4863", RadzenVariables.Dark["--rz-card-border"]);
    }

    [Fact]
    public void The_text_font_is_inter_with_a_system_fallback()
    {
        var font = RadzenVariables.Common["--rz-text-font-family"];

        Assert.StartsWith("'Inter Variable'", font);
        Assert.EndsWith("sans-serif", font);
    }

    // Der Standard von Radzen wäre ein kräftiges Pink, das nirgends freigegeben ist.
    [Theory]
    [InlineData("--rz-secondary")]
    [InlineData("--rz-link-color")]
    public void No_default_pink_is_left(string name)
    {
        Assert.Equal(Light("--rz-primary"), Light(name));
        Assert.Equal(Dark("--rz-primary"), Dark(name));
    }

    [Theory]
    [InlineData("--rz-body-background-color")]
    [InlineData("--rz-base-background-color")]
    [InlineData("--rz-card-background-color")]
    [InlineData("--rz-sidebar-background-color")]
    [InlineData("--rz-header-background-color")]
    public void No_dark_surface_is_pure_black(string name) => Assert.NotEqual("#000000", Dark(name));

    // --- Kontraste ---------------------------------------------------------------------------------------------

    public static TheoryData<string, string, string> TextPairs()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var mode in new[] { "Light", "Dark" })
        {
            foreach (var surface in new[] { "--rz-body-background-color", "--rz-card-background-color", "--rz-sidebar-background-color" })
            {
                foreach (var text in new[] { "--rz-text-color", "--rz-text-title-color", "--rz-text-secondary-color", "--rz-text-tertiary-color", "--rz-link-color" })
                {
                    data.Add(mode, text, surface);
                }
            }

            data.Add(mode, "--rz-header-color", "--rz-header-background-color");
            data.Add(mode, "--rz-sidebar-color", "--rz-sidebar-background-color");

            foreach (var colour in new[] { "primary", "secondary", "info", "warning", "success", "danger" })
            {
                foreach (var shade in new[] { "", "-light", "-dark", "-darker" })
                {
                    data.Add(mode, $"--rz-on-{colour}{shade}", $"--rz-{colour}{shade}");
                }

                data.Add(mode, $"--rz-on-{colour}-lighter", $"--rz-{colour}-lighter");
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(TextPairs))]
    public void Text_reaches_4_5_to_1(string mode, string text, string background)
    {
        var values = mode == "Light" ? RadzenVariables.Light : RadzenVariables.Dark;
        var surface = values["--rz-card-background-color"];

        var ratio = Contrast(values[text], Over(values[background], surface));

        Assert.True(ratio >= 4.5, $"{mode}: {text} auf {background} hat nur {ratio:0.00} zu 1");
    }

    [Theory]
    [InlineData("Light", "--rz-input-line", "--rz-card-background-color")]
    [InlineData("Light", "--rz-input-line", "--rz-body-background-color")]
    [InlineData("Dark", "--rz-input-line", "--rz-card-background-color")]
    [InlineData("Dark", "--rz-input-line", "--rz-body-background-color")]
    public void Lines_of_inputs_reach_3_to_1(string mode, string line, string surface)
    {
        var values = mode == "Light" ? RadzenVariables.Light : RadzenVariables.Dark;

        var ratio = Contrast(values[line], values[surface]);

        Assert.True(ratio >= 3, $"{mode}: {line} auf {surface} hat nur {ratio:0.00} zu 1");
    }

    // Mangel S11: Der Rand von Checkbox und Radioknopf kommt aus --rz-input-border-block-end, im Theme ein helles Grau (#bdbdbd, kaum 1,8 zu 1).
    [Fact]
    public void The_border_of_checkboxes_and_radios_uses_the_line_of_the_inputs()
    {
        Assert.Contains("var(--rz-input-line)", RadzenVariables.Common["--rz-input-border-block-end"]);
        Assert.Contains("--rz-input-border-block-end:", RadzenVariables.Css(dark: false) + RadzenVariables.Css(dark: true));
    }

    // --- Das fertige Stylesheet --------------------------------------------------------------------------------

    // html:root wiegt mehr als das :root der Theme-Datei; so gewinnt die Regel unabhängig von der Reihenfolge der Dateien.
    [Fact]
    public void The_style_overrides_the_theme_file_whatever_the_order()
    {
        Assert.StartsWith("html:root{", RadzenVariables.Css(dark: false));
        Assert.StartsWith("html:root{", RadzenVariables.Css(dark: true));
    }

    [Fact]
    public void The_style_holds_common_and_mode_values()
    {
        var light = RadzenVariables.Css(dark: false);
        var dark = RadzenVariables.Css(dark: true);

        Assert.Contains("--rz-primary:#1e3a5f;", light);
        Assert.Contains("--rz-primary:#86ace0;", dark);
        Assert.Contains("--rz-border-radius:12px;", light);
        Assert.Contains("--rz-border-radius:12px;", dark);
        Assert.Contains("color-scheme:dark", dark);
    }

    // --- Eingebunden in Layouts und Seiten ---------------------------------------------------------------------

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_component_draws_the_style_of_the_mode(bool dark)
    {
        var cut = Render<ThemeVariables>(p => p.Add(c => c.Dark, dark));

        Assert.Equal(RadzenVariables.Css(dark), cut.Find("style").TextContent);
    }

    [Fact]
    public void The_style_follows_a_change_of_the_mode_without_a_new_component()
    {
        var cut = Render<ThemeVariables>(p => p.Add(c => c.Dark, false));

        cut.Render(p => p.Add(c => c.Dark, true));

        Assert.Equal(RadzenVariables.Css(dark: true), cut.Find("style").TextContent);
    }

    [Fact]
    public void The_browser_layout_draws_the_variables()
    {
        Services.AddSingleton<IThemePreferenceStore>(new OpenStore());
        AddAuthorization();

        Assert.Single(Render<MainLayout>().FindComponents<ThemeVariables>());
    }

    [Theory]
    [InlineData("MainLayout.razor")]
    [InlineData("AppLayout.razor")]
    [InlineData("AuthLayout.razor")]
    public void Every_layout_file_names_the_variables(string file) =>
        Assert.Contains("<ThemeVariables", File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "Layout", file)));

    // Das Theme hängt über den Layouts, damit die Datei beim Wechsel von der Anmeldung in die App nicht neu lädt.
    [Fact]
    public void The_theme_file_comes_from_radzen_theme_on_the_web_page_and_in_the_app()
    {
        var web = File.ReadAllText(RepositoryFiles.Path("Web", "Duofy.Web", "Components", "App.razor"));
        var routes = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "Routes.razor"));
        var app = File.ReadAllText(RepositoryFiles.Path("Mobile", "Duofy", "wwwroot", "index.html"));

        Assert.Contains("<RadzenTheme Theme=\"@RadzenVariables.LightTheme\"", web);
        Assert.Contains("<RadzenTheme Theme=\"@Duofy.Shared.Theme.RadzenVariables.LightTheme\"", routes);
        Assert.DoesNotContain("css/material", web);
        Assert.DoesNotContain("css/material", app);
        Assert.Equal("material", RadzenVariables.LightTheme);
        Assert.Equal("material-dark", RadzenVariables.DarkTheme);
    }

    [Fact]
    public void Radzen_theme_draws_the_link_of_the_light_theme()
    {
        var cut = Render<RadzenTheme>(p => p.Add(c => c.Theme, RadzenVariables.LightTheme));

        Assert.Contains("css/material-base.css", cut.Find("link#radzen-theme-link").GetAttribute("href"));
    }

    // Im Browser setzt das Skript im Kopf die Klasse vor dem ersten Zeichnen; die Regel muss die helle Regel des Vorrenderns schlagen.
    [Fact]
    public void The_first_paint_style_carries_the_dark_values_and_outweighs_the_light_style()
    {
        var style = ThemeBoot.Style;

        Assert.Contains($"html.{ThemeBoot.DarkClass}:root{{", style);
        Assert.Contains("--rz-primary:#86ace0;", style);
        Assert.Contains("--rz-body-background-color:#0b0e14;", style);
    }

    [Fact]
    public void Inter_comes_from_our_own_files()
    {
        var css = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "app.css"));
        var face = Regex.Match(css, @"@font-face\s*\{[^}]*'Inter Variable'[^}]*\}").Value;

        Assert.Contains("url('fonts/", face);
        Assert.DoesNotContain("http", face);
        Assert.True(File.Exists(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "fonts", "inter-latin-wght-normal.woff2")));
    }

    // --- Gegen die Theme-Datei von Radzen geprüft ------------------------------------------------------------

    // Ein Tippfehler im Namen fiele sonst nicht auf: Die Variable gälte einfach nirgends. --rz-input-line ist unsere eigene Hilfsgröße.
    public static TheoryData<string> AllNames()
    {
        var names = new TheoryData<string>();
        foreach (var name in RadzenVariables.Common.Keys.Concat(RadzenVariables.Light.Keys).Concat(RadzenVariables.Dark.Keys).Distinct().Where(n => n != "--rz-input-line"))
        {
            names.Add(name);
        }

        return names;
    }

    [Theory]
    [MemberData(nameof(AllNames))]
    public void Every_variable_is_one_the_theme_file_knows(string name) =>
        Assert.Contains(name + ":", ThemeFile("material-base.css"));

    [Theory]
    [InlineData("var(--rz-panel-menu-item-active-indicator)")]
    [InlineData("var(--rz-panel-menu-item-border)")]
    [InlineData("var(--rz-sidebar-border-inline-end)")]
    public void The_menu_variables_are_really_used_by_the_theme_file(string use) =>
        Assert.Contains(use, ThemeFile("material-base.css"));

    private static string ThemeFile(string name)
    {
        var version = typeof(RadzenTheme).Assembly.GetName().Version!;
        var root = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        return File.ReadAllText(Path.Combine(root, "radzen.blazor", $"{version.Major}.{version.Minor}.{version.Build}", "staticwebassets", "css", name));
    }

    private sealed class OpenStore : IThemePreferenceStore
    {
        public Task<ThemeChoice> LoadAsync() => Task.FromResult(ThemeChoice.Light);

        public Task SaveAsync(ThemeChoice choice) => Task.CompletedTask;
    }

    // --- Rechnen nach WCAG 2.x ---------------------------------------------------------------------------------

    // Durchscheinende Flächen werden auf die Karte gerechnet, darauf liegen sie am Ende.
    private static string Over(string colour, string surface)
    {
        if (!colour.StartsWith("rgba(", StringComparison.Ordinal))
        {
            return colour;
        }

        var parts = colour[5..^1].Split(',', StringSplitOptions.TrimEntries);
        var alpha = double.Parse(parts[3], CultureInfo.InvariantCulture);
        var (r, g, b) = Channels(surface);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"#{Mix(parts[0], r, alpha):x2}{Mix(parts[1], g, alpha):x2}{Mix(parts[2], b, alpha):x2}");
    }

    private static int Mix(string top, int bottom, double alpha) =>
        (int)Math.Round((int.Parse(top, CultureInfo.InvariantCulture) * alpha) + (bottom * (1 - alpha)));

    private static (int R, int G, int B) Channels(string hex) => (
        int.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
        int.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
        int.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));

    private static double Luminance(string hex)
    {
        static double Channel(int value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        var (r, g, b) = Channels(hex);
        return (0.2126 * Channel(r)) + (0.7152 * Channel(g)) + (0.0722 * Channel(b));
    }

    private static double Contrast(string a, string b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }
}
