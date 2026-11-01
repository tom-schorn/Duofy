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

    // M1: Das Theme setzt die Kontur an a und button zurück und schlägt damit eine Regel nur für :focus-visible.
    [Theory]
    [InlineData("a:focus-visible")]
    [InlineData("button:focus-visible")]
    public void Links_and_buttons_show_the_focus_outline(string selector) =>
        AssertRule(selector, "outline: 3px solid var(--rz-text-title-color)", "outline-offset: 3px");

    [Theory]
    [InlineData(".rz-header a:focus-visible")]
    [InlineData(".rz-header button:focus-visible")]
    public void The_app_bar_shows_the_focus_outline_in_its_own_text_colour(string selector) =>
        AssertRule(selector, "outline: 3px solid var(--rz-header-color)", "outline-offset: 3px");

    // S5: Das Profilfeld unten in der Seitenleiste liegt auf der Hauptfarbe; die Kontur in Tintenfarbe wäre dort unsichtbar.
    [Fact]
    public void The_profile_field_on_the_primary_card_shows_a_focus_outline_in_the_colour_on_primary() =>
        AssertRule(".member-profile-up .rz-profile-menu > .rz-navigation-item > .rz-navigation-item-wrapper:focus-visible",
            "outline: 3px solid var(--rz-on-primary)", "outline-offset: -3px");

    // S15: Der Weg zurück zu den Monaten ist eine Tippfläche von mindestens 44 px Höhe.
    [Fact]
    public void The_back_link_of_the_plan_page_is_a_tap_target()
    {
        AssertRule(".plan-back", "display: inline-flex", "align-items: center", "min-height: var(--tap-min)");
        Assert.Matches(@"<RadzenLink[^>]*Class=""plan-back[ ""][^>]*data-testid=""plan-back""", Read("Shared", "Duofy.Shared", "Pages", "Plan.razor"));
    }

    // S12: RadzenText mit einer Überschriftgröße zeichnet ohne TagName eine h6; unter der h1 der Seite wäre das ein Sprung über vier Ebenen.
    [Fact]
    public void No_text_in_a_heading_size_leaves_its_tag_to_the_default()
    {
        var pages = RepositoryFiles.Path("Shared", "Duofy.Shared");
        var hits = new List<string>();
        foreach (var file in Directory.EnumerateFiles(pages, "*.razor", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                                 && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
        {
            foreach (Match tag in TagPattern().Matches(File.ReadAllText(file)))
            {
                if (tag.Value.StartsWith("<RadzenText", StringComparison.Ordinal)
                    && HeadingSizePattern().IsMatch(tag.Value) && !tag.Value.Contains("TagName", StringComparison.Ordinal))
                {
                    hits.Add($"{Path.GetFileName(file)}: {tag.Value[..Math.Min(tag.Value.Length, 80)]}");
                }
            }
        }

        Assert.True(hits.Count == 0, string.Join(Environment.NewLine, hits));
    }

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
            foreach (Match tag in TagPattern().Matches(source))
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

    [GeneratedRegex(@"TextStyle=""TextStyle.H[1-6]""")]
    private static partial Regex HeadingSizePattern();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex CommentPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();

    // Ein Text oder Knopf samt Attributen, bis zur nächsten Komponente. Attribute können selbst "<" enthalten (a < 0).
    [GeneratedRegex(@"<Radzen(?:Text|Button)\b.*?(?=<Radzen|</Radzen)", RegexOptions.Singleline)]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"Color\.(?:Error|Success|Warning)\b")]
    private static partial Regex StatusColourPattern();

    [GeneratedRegex(@"private\s+Color\s+\w+\([^)]*\)\s*=>[^;]*Color\.(?:Error|Success|Warning)\b[^;]*;")]
    private static partial Regex ColourMethodPattern();
}
