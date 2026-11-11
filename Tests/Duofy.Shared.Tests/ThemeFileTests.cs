using System.Text.RegularExpressions;

namespace Duofy.Shared.Tests;

/// <summary>
/// A2 (Spec 82): Die Theme-Datei hält die Werte des alten Duofy (frontend/src/index.css), hell und dunkel.
/// Die erwarteten Werte stehen hier fest, weil das alte Duofy nicht zum Repository gehört.
/// </summary>
public partial class ThemeFileTests
{
    private static string Theme() => File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "theme.css"));

    // Die Variablen des Blocks, dessen Selektor genau so heißt.
    private static Dictionary<string, string> Block(string selector)
    {
        var css = Theme();
        var start = css.IndexOf(selector + " {", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Block {selector} fehlt in theme.css");
        var open = css.IndexOf('{', start);
        var body = css[(open + 1)..css.IndexOf('}', open)];
        return VariablePattern().Matches(body).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Trim());
    }

    // A3 (Spec 118), Issue 117: Das Theme sagt den Steuerelementen des Systems, ob sie hell oder dunkel sind.
    [Fact]
    public void The_theme_declares_a_dark_colour_scheme_for_the_dark_theme_and_its_native_controls()
    {
        var css = Theme();

        Assert.Matches(@"\.dark\s*\{\s*color-scheme:\s*dark;", css);
        Assert.Matches(@":root\s*\{\s*color-scheme:\s*light;", css);
        Assert.Matches(@"\.dark input,[^{]*\.dark select[^{]*\{\s*color-scheme:\s*dark;", css);
    }

    [Theory]
    [InlineData(":root", "--background", "#fbf9f4")]
    [InlineData(":root", "--foreground", "#0f141c")]
    [InlineData(":root", "--card", "#ffffff")]
    [InlineData(":root", "--primary", "#1e3a5f")]
    [InlineData(":root", "--primary-foreground", "#fbf9f4")]
    [InlineData(":root", "--muted", "#f1eee6")]
    [InlineData(":root", "--muted-foreground", "#5a6270")]
    [InlineData(":root", "--accent", "#ebe8df")]
    [InlineData(":root", "--destructive", "#a63a2e")]
    [InlineData(":root", "--border", "#dbd7cc")]
    [InlineData(":root", "--input", "#dbd7cc")]
    [InlineData(":root", "--ring", "#1e3a5f")]
    [InlineData(":root", "--radius", "0.5rem")]
    [InlineData(":root", "--sidebar", "#f6f3ec")]
    [InlineData(":root", "--chart-1", "#2b6cb0")]
    [InlineData(":root", "--chart-2", "#c68a0e")]
    [InlineData(":root", "--chart-3", "#8e3b6b")]
    [InlineData(":root", "--chart-4", "#4f9e6a")]
    [InlineData(":root", "--chart-5", "#5a6270")]
    [InlineData(".dark", "--background", "#0b0e14")]
    [InlineData(".dark", "--foreground", "#eceff3")]
    [InlineData(".dark", "--card", "#141922")]
    [InlineData(".dark", "--primary", "#86ace0")]
    [InlineData(".dark", "--primary-foreground", "#0b0e14")]
    [InlineData(".dark", "--muted", "#1f2531")]
    [InlineData(".dark", "--muted-foreground", "#8e98a8")]
    [InlineData(".dark", "--accent", "#252c39")]
    [InlineData(".dark", "--destructive", "#dd7263")]
    [InlineData(".dark", "--border", "#252c39")]
    [InlineData(".dark", "--input", "#2c3441")]
    [InlineData(".dark", "--ring", "#86ace0")]
    [InlineData(".dark", "--sidebar", "#0e1218")]
    [InlineData(".dark", "--chart-1", "#4285c9")]
    [InlineData(".dark", "--chart-2", "#c2952f")]
    [InlineData(".dark", "--chart-3", "#a55b86")]
    [InlineData(".dark", "--chart-4", "#4da87c")]
    [InlineData(".dark", "--chart-5", "#8e98a8")]
    public void Theme_holds_the_value_of_the_old_duofy(string selector, string variable, string expected)
    {
        var block = Block(selector);
        Assert.True(block.TryGetValue(variable, out var actual), $"{variable} fehlt in {selector}");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Theme_names_the_fonts_of_the_old_duofy()
    {
        var css = Theme();
        Assert.Contains("--font-heading: 'Iowan Old Style', 'Palatino Linotype', Palatino, Georgia, serif", css);
        Assert.Contains("--font-sans: 'Geist Variable', sans-serif", css);
        Assert.Contains("fonts/geist.woff2", css);
    }

    [GeneratedRegex(@"(--[a-z0-9-]+):\s*([^;]+);")]
    private static partial Regex VariablePattern();
}
