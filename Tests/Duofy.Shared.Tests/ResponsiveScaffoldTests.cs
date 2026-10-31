using System.Text.RegularExpressions;
using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Layout;
using Duofy.Shared.Services;
using Duofy.Shared.Theme;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 9, das responsive Gerüst: drei Breitenbereiche, begrenzter und zentrierter Inhalt auf dem Desktop,
/// Mindestgrößen für Tippflächen, kein seitlicher Überlauf, Randwerte an einer Stelle und die Schriftskala
/// aus dem Theme im Rahmen. Ein echter Browser fehlt; geprüft wird der Text des Stylesheets und die
/// gezeichnete Struktur, die Sichtprüfung macht ein Mensch.
/// </summary>
public class ResponsiveScaffoldTests : PageTestContext
{
    public ResponsiveScaffoldTests()
    {
        Services.AddSingleton<IAppHost, AppHost>();
        Services.AddSingleton<IThemePreferenceStore>(new MemoryThemeStore());
    }

    private static string Css() => File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "app.css"));

    /// <summary>Der Inhalt der ersten Regel mit genau diesem Auswahltext, innerhalb des angegebenen Textes.</summary>
    private static string Rule(string css, string selector)
    {
        var match = Regex.Match(css, @"(?<![\w.-])" + Regex.Escape(selector) + @"\s*\{([^}]*)\}");
        Assert.True(match.Success, $"Regel '{selector}' fehlt");
        return match.Groups[1].Value;
    }

    /// <summary>Alles, was in Blöcken dieser Medienabfrage steht.</summary>
    private static string Media(string css, string query)
    {
        // Ein Bereich darf an mehreren Stellen stehen (Werte oben, Regeln bei ihren Bausteinen); gelesen wird alles zusammen.
        var found = new System.Text.StringBuilder();
        var from = 0;
        while ((from = css.IndexOf("@media " + query, from, StringComparison.Ordinal)) >= 0)
        {
            var open = css.IndexOf('{', from);
            var depth = 0;
            var i = open;
            for (; i < css.Length; i++)
            {
                if (css[i] == '{') depth++;
                else if (css[i] == '}' && --depth == 0) break;
            }
            found.Append(css, open + 1, i - open - 1).AppendLine();
            from = i;
        }
        Assert.True(found.Length > 0, $"Medienabfrage '{query}' fehlt");
        return found.ToString();
    }

    // A1 (R1)
    [Fact]
    public void Breakpoints_are_defined_once_as_phone_to_599_tablet_600_to_1279_desktop_from_1280()
    {
        Assert.Equal(599, WidthRanges.PhoneMax);
        Assert.Equal(600, WidthRanges.TabletMin);
        Assert.Equal(1279, WidthRanges.TabletMax);
        Assert.Equal(1280, WidthRanges.DesktopMin);
    }

    // A1 (R1): Das Stylesheet kann keine C#-Konstanten lesen, deshalb müssen seine Umbruchstellen mit ihnen übereinstimmen.
    [Fact]
    public void The_stylesheet_only_breaks_at_the_defined_breakpoints()
    {
        var css = Css();
        var widths = Regex.Matches(css, @"@media[^{]*\((min|max)-width:\s*(\d+)px\)")
            .Select(m => (m.Groups[1].Value, int.Parse(m.Groups[2].Value)))
            .ToList();

        Assert.Contains(("max", WidthRanges.PhoneMax), widths);
        Assert.Contains(("min", WidthRanges.TabletMin), widths);
        Assert.Contains(("min", WidthRanges.DesktopMin), widths);

        // Der Anmeldebildschirm hat mit 1024 px seine eigene, ältere Umbruchstelle (Markenfläche); alles andere ist festgelegt.
        int[] known = [WidthRanges.PhoneMax, WidthRanges.TabletMin, WidthRanges.DesktopMin, 1024];
        Assert.All(widths, w => Assert.Contains(w.Item2, known));
    }

    // A1 (R1): Die Doku wiederholt die Bereiche.
    [Fact]
    public void The_documentation_repeats_the_three_ranges()
    {
        var doc = File.ReadAllText(RepositoryFiles.Path("docs", "layout.md"));

        Assert.Contains("599", doc);
        Assert.Contains("600", doc);
        Assert.Contains("1279", doc);
        Assert.Contains("1280", doc);
        Assert.Contains("1180", doc);
    }

    // A2 (R2)
    [Fact]
    public void Content_is_limited_to_1180px_and_centred_from_1280px()
    {
        Assert.Equal(1180, WidthRanges.ContentMaxWidth);

        var desktop = Media(Css(), $"(min-width: {WidthRanges.DesktopMin}px)");
        var rule = Rule(desktop, ".app-page");

        Assert.Contains("max-width: 1180px", rule);
        Assert.Contains("margin-inline: auto", rule);
    }

    // A2 (R2): Unter dem Desktop füllt der Inhalt die Breite mit seitlichem Abstand.
    [Fact]
    public void Below_desktop_the_content_fills_the_width_with_a_side_gutter()
    {
        var rule = Rule(Css(), ".app-page");

        Assert.Contains("padding-inline: var(--page-gutter)", rule);
        Assert.DoesNotContain("max-width: 1180px", rule);
    }

    // A3 (R3)
    [Fact]
    public void Tap_targets_are_at_least_44px_high_and_wide()
    {
        var css = Css();
        Assert.Contains($"--tap-min: {WidthRanges.TapTargetMin}px", css);
        Assert.Equal(44, WidthRanges.TapTargetMin);

        var item = Rule(css, ".app-bottom-item.mud-button-root");
        Assert.Contains("min-width: var(--tap-min)", item);
        Assert.Contains("min-height: var(--tap-min)", item);

        var buttons = Rule(css, ".mud-button-root");
        Assert.Contains("min-height: var(--tap-min)", buttons);
        var iconButtons = Rule(css, ".mud-icon-button");
        Assert.Contains("min-width: var(--tap-min)", iconButtons);
        Assert.Contains("min-height: var(--tap-min)", iconButtons);
    }

    // A3 (R3): Auf dem Handy bleiben die vier Ziele der unteren Leiste.
    [Fact]
    public void The_bottom_bar_keeps_its_four_destinations()
    {
        var cut = Render<BottomNavigation>();

        Assert.Equal(["Start", "Plan", "Buchungen", "Mehr"], cut.FindAll("a").Select(a => a.TextContent.Trim()));
    }

    // A4 (R4)
    [Fact]
    public void The_frame_has_a_rule_against_horizontal_overflow_and_no_width_above_100_percent()
    {
        var css = Css();
        var frame = Rule(css, ".app-page");
        Assert.Contains("max-width: 100%", frame);
        Assert.Contains("overflow-x: clip", frame);

        var layout = Rule(css, ".mud-layout");
        Assert.Contains("overflow-x: clip", layout);

        // Im Rahmen (app-/auth-Regeln) steht keine Breite über 100 Prozent oder 100 Bildschirmbreiten.
        var frameRules = Regex.Matches(css, @"(?<sel>[^{}]*\.(?:app|auth)-[^{}]*)\{(?<body>[^}]*)\}");
        Assert.NotEmpty(frameRules);
        foreach (Match rule in frameRules)
        {
            foreach (Match width in Regex.Matches(rule.Groups["body"].Value, @"(?<![-\w])(?:min-|max-)?width:\s*(?<n>\d+(?:\.\d+)?)(?<unit>%|vw)"))
            {
                Assert.True(double.Parse(width.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture) <= 100,
                    $"Breite über 100{width.Groups["unit"].Value} in {rule.Groups["sel"].Value.Trim()}");
            }
        }
    }

    // A4 (R4): Die sechs Hauptseiten werden im Rahmen gezeichnet, der den Überlauf verhindert.
    [Theory]
    [InlineData("/")]
    [InlineData("/plan")]
    [InlineData("/buchungen")]
    [InlineData("/konten")]
    [InlineData("/vertraege")]
    [InlineData("/mehr")]
    public void The_main_pages_are_drawn_inside_the_page_frame(string path)
    {
        Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true));
        Navigation.NavigateTo(path);

        var cut = Render<Routes>();

        Assert.Single(cut.FindAll(".app-page"));
    }

    // A5 (R5): Die Randwerte stehen im ersten :root-Block, und nur die Bereiche darunter überschreiben sie.
    [Fact]
    public void The_gutter_and_block_gap_are_defined_once_per_range()
    {
        var css = Css();

        var root = Rule(css, ":root");
        Assert.Contains("--page-gutter:", root);
        Assert.Contains("--block-gap:", root);
        Assert.Contains("--page-gutter:", Media(css, $"(min-width: {WidthRanges.TabletMin}px)"));
        Assert.Contains("--page-gutter:", Media(css, $"(min-width: {WidthRanges.DesktopMin}px)"));
        Assert.Contains("--block-gap:", Media(css, $"(min-width: {WidthRanges.DesktopMin}px)"));
    }

    // A5 (R5): Beide Layouts benutzen die Rahmenklasse statt eigener Abstände.
    [Fact]
    public void Both_layouts_use_the_page_frame_instead_of_their_own_margins()
    {
        foreach (var file in new[] { "MainLayout.razor", "AppLayout.razor" })
        {
            var markup = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "Layout", file));
            Assert.Contains("app-page", markup);
            Assert.DoesNotContain("MaxWidth.Large", markup);
            Assert.DoesNotContain("my-4", markup);
        }
    }

    // Schriftskala: Die Werte im Stylesheet sind die des Themes (Wert für den Desktop), das Stylesheet kennt keine eigene Skala.
    [Theory]
    [InlineData("Title", "title")]
    [InlineData("Section", "section")]
    [InlineData("Amount", "amount")]
    [InlineData("Text", "text")]
    [InlineData("Extra", "extra")]
    public void The_type_scale_of_the_theme_is_what_the_frame_uses_on_desktop(string role, string name)
    {
        var style = DuofyTheme.TypeScale[role];
        var desktop = Media(Css(), $"(min-width: {WidthRanges.DesktopMin}px)");
        var scope = desktop.Contains($"--type-{name}-size:") ? desktop : Rule(Css(), ":root");

        Assert.Contains($"--type-{name}-size: {style.Size};", scope);
        Assert.Contains($"--type-{name}-weight: {style.Weight};", Rule(Css(), ":root"));
        Assert.Contains($"--type-{name}-line: {style.LineHeight};", Rule(Css(), ":root"));
    }

    // Schriftskala: Auf dem Handy ist der Abschnittstitel kleiner als auf dem Desktop.
    [Fact]
    public void The_section_size_grows_from_phone_over_tablet_to_desktop()
    {
        var css = Css();
        var phone = Regex.Match(Media(css, $"(max-width: {WidthRanges.PhoneMax}px)"), @"--type-section-size:\s*([\d.]+)rem").Groups[1].Value;
        var tablet = Regex.Match(Media(css, $"(min-width: {WidthRanges.TabletMin}px)"), @"--type-section-size:\s*([\d.]+)rem").Groups[1].Value;
        var desktopRem = double.Parse(Regex.Match(DuofyTheme.TypeScale["Section"].Size, @"([\d.]+)rem").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        var phoneRem = double.Parse(phone, System.Globalization.CultureInfo.InvariantCulture);
        var tabletRem = double.Parse(tablet, System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(phoneRem < tabletRem, "Handy muss kleiner sein als Tablet");
        Assert.True(tabletRem < desktopRem, "Tablet muss kleiner sein als Desktop");
    }

    // Schriftskala: Im Rahmen stehen keine losen Schriftgrößen mehr, nur Werte der Skala.
    [Fact]
    public void The_frame_rules_take_their_font_sizes_from_the_scale()
    {
        var css = Css();
        var frameRules = Regex.Matches(css, @"(?<sel>[^{}]*\.(?:app|auth)-[^{}]*)\{(?<body>[^}]*)\}");

        foreach (Match rule in frameRules)
        {
            foreach (Match size in Regex.Matches(rule.Groups["body"].Value, @"font-size:\s*(?<value>[^;]+);"))
            {
                Assert.Matches(@"^var\(--type-(title|section|amount|text|extra)-size\)$", size.Groups["value"].Value.Trim());
            }
        }
    }

    // Schriftskala: Der Titel der App-Leiste trägt die Rolle "Abschnitt" aus der Skala.
    [Fact]
    public void The_title_in_the_app_bar_uses_the_section_role()
    {
        Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true));
        Navigation.NavigateTo("/mehr");

        var cut = Render<Routes>();

        Assert.Contains("app-title", cut.Find("[data-testid='app-title']").ClassList);
        Assert.Contains("font-size: var(--type-section-size)", Rule(Css(), ".mud-typography.app-title"));
    }

    private sealed class MemoryThemeStore : IThemePreferenceStore
    {
        public Task<ThemeChoice> LoadAsync() => Task.FromResult(ThemeChoice.Device);

        public Task SaveAsync(ThemeChoice choice) => Task.CompletedTask;
    }
}
