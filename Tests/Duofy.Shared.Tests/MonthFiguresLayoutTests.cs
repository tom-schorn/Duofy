using System.Text.RegularExpressions;
using Duofy.Shared.Layout;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 101, die Kennzahlen des Monats (Einnahmen, Verplanbar, Anstehend, Übertrag): am Handy zwei mal zwei, ab 600 px in einer
/// Reihe, immer innerhalb der Seitenränder und ohne waagerechten Streifen. Ein echter Browser fehlt hier; gelesen wird das
/// Stylesheet, gemessen wird in der Browserprüfung (siehe ResponsiveWidthsCheck).
/// </summary>
public class MonthFiguresLayoutTests
{
    private static string Css => File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "duofy.css")).Replace("\r\n", "\n");

    // Der Block einer Medienabfrage, alle Stellen zusammen.
    private static string Media(string condition)
    {
        var css = Css;
        var found = new System.Text.StringBuilder();
        var from = 0;
        while ((from = css.IndexOf("@media " + condition, from, StringComparison.Ordinal)) >= 0)
        {
            var open = css.IndexOf('{', from);
            var depth = 0;
            var i = open;
            for (; i < css.Length; i++)
            {
                if (css[i] == '{') depth++;
                else if (css[i] == '}' && --depth == 0) break;
            }
            found.Append(css, open + 1, i - open - 1).Append('\n');
            from = i;
        }
        return found.ToString();
    }

    private static string Rule(string css, string selector)
    {
        var match = Regex.Match(css, @"(?<![\w.>-])" + Regex.Escape(selector) + @"\s*\{([^}]*)\}");
        Assert.True(match.Success, $"Regel '{selector}' fehlt");
        return match.Groups[1].Value;
    }

    // A2 (R3): Unter 600 px zwei Spalten, also zwei Reihen mit je zwei Karten, und kein Wischstreifen.
    [Fact]
    public void A2_on_a_phone_the_cards_stand_two_by_two()
    {
        var phone = Media($"(max-width: {WidthRanges.PhoneMax}px)");
        var metrics = Rule(phone, ".metrics");

        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr))", metrics);
        Assert.DoesNotContain("overflow-x", metrics);
        Assert.DoesNotContain("scroll-snap", phone);
        // Kein negativer Rand: die Karten bleiben innerhalb der Seitenränder.
        Assert.DoesNotMatch(@"margin:\s*0\s+-", metrics);
        Assert.DoesNotContain("flex: 0 0 44%", phone);
    }

    // A3 (R3): Ab 600 px eine Reihe mit vier Karten, ebenfalls ohne Streifen.
    [Fact]
    public void A3_from_600_the_cards_stand_in_one_row()
    {
        var basis = Rule(Css, ".metrics");

        Assert.Contains("grid-template-columns: repeat(4, minmax(0, 1fr))", basis);
        Assert.DoesNotContain("overflow-x", basis);
    }

    // Beträge werden am Handy eine Stufe kleiner, nicht abgeschnitten; die Beschriftung darf umbrechen.
    [Fact]
    public void Amounts_shrink_one_step_on_a_phone_instead_of_being_cut()
    {
        var phone = Media($"(max-width: {WidthRanges.PhoneMax}px)");

        Assert.Contains("font-size: 16px", Rule(phone, ".metrics .metric-amount"));
        Assert.Contains("overflow-wrap: anywhere", Rule(Css, ".metric-amount"));
        Assert.DoesNotContain("overflow: hidden", Rule(Css, ".metric-amount"));
    }
}
