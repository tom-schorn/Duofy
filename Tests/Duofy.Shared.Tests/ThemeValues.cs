using System.Text.RegularExpressions;

namespace Duofy.Shared.Tests;

/// <summary>Liest die Werte aus der Theme-Datei, für Tests von Kontrast und Farben.</summary>
internal static partial class ThemeValues
{
    private static string Theme() => File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "theme.css"));

    public static IReadOnlyDictionary<string, string> Light => Block(":root");

    public static IReadOnlyDictionary<string, string> Dark => Block(".dark");

    private static Dictionary<string, string> Block(string selector)
    {
        var css = Theme();
        var open = css.IndexOf('{', css.IndexOf(selector + " {", StringComparison.Ordinal));
        var body = css[(open + 1)..css.IndexOf('}', open)];
        return VariablePattern().Matches(body).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Trim());
    }

    [GeneratedRegex(@"(--[a-z0-9-]+):\s*([^;]+);")]
    private static partial Regex VariablePattern();
}
