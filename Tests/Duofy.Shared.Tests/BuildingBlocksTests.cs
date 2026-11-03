using Bunit;
using Duofy.Shared.Blocks;
using Duofy.Shared.Pages;
using Duofy.Shared.Theme;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 6 (Bausteine Farbblock, Linientabelle, eckige Knöpfe): Aufbau, Fläche, Beschriftung und Kontrast
/// in beiden Themes. Die Darstellung im Browser (Fokuskontur, Linien) prüft diese Datei nur über das Stylesheet.
/// </summary>
public class BuildingBlocksTests : UiTestContext
{
    private static string Css() => File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "duofy.css"));

    // Eine Regel der CSS-Datei ab dem Selektor bis zur schließenden Klammer (die Regeln der Bausteine sind nicht verschachtelt).
    private static string Rule(string css, string selector)
    {
        var start = css.IndexOf(selector + " {", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Regel '{selector}' fehlt in app.css");
        return css[start..css.IndexOf('}', start)];
    }

    private static double Luminance(string hex)
    {
        static double Channel(int value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        var rgb = Convert.ToInt32(hex.TrimStart('#'), 16);
        return 0.2126 * Channel(rgb >> 16 & 255) + 0.7152 * Channel(rgb >> 8 & 255) + 0.0722 * Channel(rgb & 255);
    }

    // Die Fläche eines Farbblocks: je zur Hälfte Statusfarbe und Karte (color-mix im Stylesheet).
    private static string Mix(string a, string b)
    {
        var (x, y) = (Convert.ToInt32(a.TrimStart('#'), 16), Convert.ToInt32(b.TrimStart('#'), 16));
        return $"#{((x >> 16 & 255) + (y >> 16 & 255)) / 2:x2}{((x >> 8 & 255) + (y >> 8 & 255)) / 2:x2}{((x & 255) + (y & 255)) / 2:x2}";
    }

    private static double Contrast(string a, string b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    // Spec 66, R9: Ein Zusatz steht auf dem Block und gehört zu seinem Namen für Vorlese-Programme.
    [Fact]
    public void Metric_may_carry_a_note_under_the_amount()
    {
        var cut = Render<Metric>(p => p
            .Add(c => c.Title, "Frei")
            .Add(c => c.Amount, 530m)
            .Add(c => c.Note, "Enthält Übertrag"));

        Assert.Equal("Enthält Übertrag", cut.Find(".metric-note").TextContent);
        Assert.Equal("Frei: 530,00 €. Enthält Übertrag", cut.Find(".metric").GetAttribute("aria-label"));
    }

    // Spec 24: Statt des Betrags darf der Block einen eigenen Text tragen (Frei bei null: Alles verplant).
    [Fact]
    public void Metric_may_show_its_own_text_instead_of_the_amount()
    {
        var cut = Render<Metric>(p => p
            .Add(c => c.Title, "Frei")
            .Add(c => c.Amount, 0m)
            .Add(c => c.AmountText, "Alles verplant"));

        Assert.Equal("Alles verplant", cut.Find(".metric-amount").TextContent);
        Assert.Equal("Frei: Alles verplant", cut.Find(".metric").GetAttribute("aria-label"));
    }

    // A5: Ohne Meldung "Entwicklungsbetrieb" (Produktion, App) zeigt die Seite nichts.
    [Fact]
    public void Test_page_shows_nothing_unless_development_is_announced()
    {
        var cut = Render<BuildingBlocksDemo>();

        Assert.Empty(cut.FindAll("[data-testid^='blocks-panel-']"));
    }

    [Fact]
    public void Test_page_shows_nothing_in_production()
    {
        Services.AddSingleton<IDevelopmentMode>(new DevelopmentMode(false));
        var cut = Render<BuildingBlocksDemo>();

        Assert.Empty(cut.FindAll("[data-testid^='blocks-panel-']"));
    }

    // Gefunden im Browser bei 1912 px: Ab 1280 px ist der Text ein Raster; die Art darf dort keine eigene Spalte werden.
    [Fact]
    public void Line_table_keeps_tag_and_label_together_in_one_cell_of_the_grid()
    {
        var cut = Render<LineTable>(p => p
            .Add(c => c.Caption, "Buchungen")
            .Add(c => c.Rows, new[] { new LineTableRow("Miete", 850m, "01.11.2026", Tag: b => b.AddMarkupContent(0, "<b>Ausgabe</b>")) }));

        var text = cut.Find(".line-table-text");
        Assert.Equal(["line-table-title", "line-table-detail"], text.Children.Select(c => c.ClassName));
        Assert.Equal("AusgabeMiete", text.Children[0].TextContent);
    }
}
