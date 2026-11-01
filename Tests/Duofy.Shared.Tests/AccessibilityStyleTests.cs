using System.Text.RegularExpressions;

namespace Duofy.Shared.Tests;

/// <summary>
/// Barrierefreiheit im Stylesheet und in den Quellen (Prüfung vom 01.11.2026): Fokuskontur, Lesefarben, Hinweisfarben,
/// Zoom. Die hellen Farben Error, Success und Warning sind Flächenfarben; als Schriftfarbe sind sie nicht lesbar.
/// </summary>
public partial class AccessibilityStyleTests
{
    private static string Read(params string[] parts) => File.ReadAllText(RepositoryFiles.Path(parts));

    private static string Css() => Read("Shared", "Duofy.Shared", "wwwroot", "app.css");

    // Der Inhalt der Regel mit genau diesem Selektor, nur außerhalb von @media; null, wenn es sie dort nicht gibt.
    private static string? TopLevelRule(string css, string selector)
    {
        var clean = CommentPattern().Replace(css, "");
        var depth = 0;
        var start = 0;
        for (var i = 0; i < clean.Length; i++)
        {
            if (clean[i] == '{')
            {
                if (depth == 0)
                {
                    var selectors = clean[start..i].Split(',').Select(s => WhitespacePattern().Replace(s.Trim(), " "));
                    if (selectors.Contains(selector))
                    {
                        return clean[(i + 1)..clean.IndexOf('}', i)];
                    }
                }
                depth++;
            }
            else if (clean[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    start = i + 1;
                }
            }
        }
        return null;
    }

    private static void AssertRule(string selector, params string[] declarations)
    {
        var rule = TopLevelRule(Css(), selector);
        Assert.True(rule is not null, $"Regel '{selector}' fehlt außerhalb von @media in app.css");
        foreach (var declaration in declarations)
        {
            Assert.Contains(declaration, rule);
        }
    }

    // M1: MudBlazor setzt die Kontur an a und button zurück und schlägt damit eine Regel nur für :focus-visible.
    [Theory]
    [InlineData("a:focus-visible")]
    [InlineData("button:focus-visible")]
    public void Links_and_buttons_show_the_focus_outline(string selector) =>
        AssertRule(selector, "outline: 3px solid var(--mud-palette-text-primary)", "outline-offset: 3px");

    [Theory]
    [InlineData(".mud-appbar a:focus-visible")]
    [InlineData(".mud-appbar button:focus-visible")]
    public void The_app_bar_shows_the_focus_outline_in_its_own_text_colour(string selector) =>
        AssertRule(selector, "outline: 3px solid var(--mud-palette-appbar-text)", "outline-offset: 3px");

    // M2: Nebentexte sind in jeder Breite lesbar, nicht erst auf der breiten Seite.
    [Fact]
    public void Secondary_text_uses_the_reading_colour_at_every_width() =>
        AssertRule(".mud-typography.mud-secondary-text", "color: var(--mud-palette-text-secondary) !important");

    // M3: Hinweise sehen aus wie Fehler: Tintentext, Rand, die helle Farbe nur als Fläche.
    [Theory]
    [InlineData("success")]
    [InlineData("info")]
    public void Alerts_have_ink_text_a_border_and_the_light_colour_as_surface(string kind) =>
        AssertRule($".mud-alert-text-{kind}",
            "color: var(--mud-palette-text-primary)",
            "border-left: 4px solid var(--mud-palette-text-primary)",
            $"background-color: var(--mud-palette-{kind})");

    // M3: Text, Text- und Umrissknöpfe sowie Symbolknöpfe zeichnen ihre Farbe auf Papier. Nur gefüllte Knöpfe dürfen sie tragen.
    [Fact]
    public void No_text_or_unfilled_button_is_drawn_in_a_light_status_colour()
    {
        var pages = RepositoryFiles.Path("Shared", "Duofy.Shared");
        var hits = new List<string>();
        foreach (var file in Directory.EnumerateFiles(pages, "*.razor", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                                 && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
        {
            var source = File.ReadAllText(file);
            foreach (Match tag in MudTagPattern().Matches(source))
            {
                if (StatusColourPattern().IsMatch(tag.Value) && !tag.Value.Contains("Variant.Filled", StringComparison.Ordinal))
                {
                    hits.Add($"{Path.GetFileName(file)}: {tag.Value[..Math.Min(tag.Value.Length, 80)]}");
                }
            }

            // Farben, die eine Methode auswählt, zeigt das Muster oben nicht.
            foreach (Match method in ColourMethodPattern().Matches(source))
            {
                hits.Add($"{Path.GetFileName(file)}: {method.Value}");
            }
        }

        Assert.True(hits.Count == 0, string.Join(Environment.NewLine, hits));
    }

    // S2: Zoomen bleibt in der App möglich.
    [Fact]
    public void The_app_can_be_zoomed()
    {
        var page = Read("Mobile", "Duofy", "wwwroot", "index.html");

        Assert.DoesNotContain("maximum-scale", page);
        Assert.DoesNotContain("user-scalable", page);
        Assert.Contains("viewport-fit=cover", page);
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex CommentPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();

    // Ein MudText oder Knopf samt Attributen, bis zur nächsten Komponente. Attribute können selbst "<" enthalten (a < 0).
    [GeneratedRegex(@"<Mud(?:Text|Button|IconButton)\b.*?(?=<Mud|</Mud)", RegexOptions.Singleline)]
    private static partial Regex MudTagPattern();

    [GeneratedRegex(@"Color\.(?:Error|Success|Warning)\b")]
    private static partial Regex StatusColourPattern();

    [GeneratedRegex(@"private\s+Color\s+\w+\([^)]*\)\s*=>[^;]*Color\.(?:Error|Success|Warning)\b[^;]*;")]
    private static partial Regex ColourMethodPattern();
}
