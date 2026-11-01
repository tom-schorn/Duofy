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
    private static string Css() => File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "app.css"));

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

    // A1: Titel und Betrag stehen im Block, die Variante bestimmt die Fläche, der Text ist der Haupttext.
    [Theory]
    [InlineData(BlockVariant.Income, "color-block--income", "--rz-success")]
    [InlineData(BlockVariant.Fixed, "color-block--fixed", "--rz-danger")]
    [InlineData(BlockVariant.Free, "color-block--free", "--rz-warning")]
    public void Color_block_shows_title_and_amount_on_the_surface_of_its_variant(BlockVariant variant, string cssClass, string surfaceVariable)
    {
        var cut = Render<ColorBlock>(p => p
            .Add(c => c.Variant, variant)
            .Add(c => c.Title, "Einnahmen")
            .Add(c => c.Amount, 2450.5m));

        var block = cut.Find(".color-block");
        Assert.Contains(cssClass, block.ClassList);
        Assert.Equal("Einnahmen", cut.Find(".color-block-title").TextContent);
        Assert.Equal("2.450,50 €", cut.Find(".color-block-amount").TextContent);

        var css = Css();
        var rule = Rule(css, "." + cssClass);
        Assert.Contains($"background-color: color-mix(in srgb, var({surfaceVariable}) 50%, var(--rz-card-background-color))", rule);
        // Auf dem Block steht immer der Haupttext, nie der Nebentext.
        var baseRule = Rule(css, ".color-block");
        Assert.Contains("color: var(--rz-text-title-color)", baseRule);
        Assert.DoesNotContain("text-secondary", baseRule + rule);
    }

    // A2: Zwei Zeilen, je Bezeichnung und Betrag; Linie zwischen den Zeilen; tabellarische Ziffern am Betrag.
    [Fact]
    public void Line_table_shows_label_and_amount_per_row_separated_by_lines()
    {
        var cut = Render<LineTable>(p => p
            .Add(c => c.Caption, "Feste Kosten")
            .Add(c => c.Rows, new[] { new LineTableRow("Miete", 850m), new LineTableRow("Strom", 62.4m) }));

        var rows = cut.FindAll("tbody tr");
        Assert.Equal(2, rows.Count);
        Assert.Equal("Miete", rows[0].QuerySelector(".line-table-label")!.TextContent);
        Assert.Equal("850,00 €", rows[0].QuerySelector(".line-table-amount")!.TextContent);
        Assert.Equal("Strom", rows[1].QuerySelector(".line-table-label")!.TextContent);
        Assert.Equal("62,40 €", rows[1].QuerySelector(".line-table-amount")!.TextContent);

        var css = Css();
        Assert.Contains("border-bottom: 1px solid var(--rz-input-line)", Rule(css, ".line-table td"));
        Assert.Contains("tabular-nums", Rule(css, ".line-table-amount"));
        Assert.Contains("text-align: right", Rule(css, ".line-table-amount"));
        Assert.DoesNotContain("box-shadow", Rule(css, ".line-table"));
    }

    // Spec 24: eine Zeile darf vorne einen Haken, unter der Bezeichnung einen Nebentext und hinten Knöpfe tragen.
    [Fact]
    public void Line_table_rows_may_carry_leading_detail_and_trailing_content()
    {
        var row = new LineTableRow("Miete", null, "am 1.",
            Leading: b => b.AddMarkupContent(0, "<input type=\"checkbox\" />"),
            Trailing: b => b.AddMarkupContent(0, "<button>Ändern</button>"),
            TestId: "row-miete");
        var cut = Render<LineTable>(p => p.Add(c => c.Caption, "Grundbedarf").Add(c => c.Rows, new[] { row, new LineTableRow("Strom", 62.4m, null, Leading: row.Leading) }));

        var first = cut.Find("[data-testid='row-miete']");
        Assert.NotNull(first.QuerySelector("td.line-table-lead input[type=checkbox]"));
        Assert.StartsWith("Miete", first.QuerySelector(".line-table-label")!.TextContent.Trim());
        Assert.Equal("am 1.", first.QuerySelector(".line-table-detail")!.TextContent);
        Assert.Equal("", first.QuerySelector(".line-table-amount")!.TextContent);
        Assert.NotNull(first.QuerySelector("td.line-table-trail button"));
        // Die Zeile ohne hinteren Inhalt hält die Spalte trotzdem, damit die Tabelle gerade bleibt.
        Assert.Equal(first.QuerySelectorAll("td, th").Length, cut.FindAll("tbody tr")[1].QuerySelectorAll("td, th").Length);
        Assert.Equal("62,40 €", cut.FindAll("tbody tr")[1].QuerySelector(".line-table-amount")!.TextContent);
    }

    // Spec 66, R9: Ein Zusatz steht auf dem Block und gehört zu seinem Namen für Vorlese-Programme.
    [Fact]
    public void Color_block_may_carry_a_note_under_the_amount()
    {
        var cut = Render<ColorBlock>(p => p
            .Add(c => c.Variant, BlockVariant.Free)
            .Add(c => c.Title, "Frei")
            .Add(c => c.Amount, 530m)
            .Add(c => c.Note, "Enthält Übertrag"));

        Assert.Equal("Enthält Übertrag", cut.Find(".color-block-note").TextContent);
        Assert.Equal("Frei: 530,00 €. Enthält Übertrag", cut.Find(".color-block").GetAttribute("aria-label"));
    }

    // Spec 24: Statt des Betrags darf der Block einen eigenen Text tragen (Frei bei null: Alles verplant).
    [Fact]
    public void Color_block_may_show_its_own_text_instead_of_the_amount()
    {
        var cut = Render<ColorBlock>(p => p
            .Add(c => c.Variant, BlockVariant.Free)
            .Add(c => c.Title, "Frei")
            .Add(c => c.Amount, 0m)
            .Add(c => c.AmountText, "Alles verplant"));

        Assert.Equal("Alles verplant", cut.Find(".color-block-amount").TextContent);
        Assert.Equal("Frei: Alles verplant", cut.Find(".color-block").GetAttribute("aria-label"));
    }

    // A3: Ein Knopf zeigt bei Fokus 3 px Kontur in Tintenfarbe; eckig; drei Arten.
    [Theory]
    [InlineData(ButtonKind.Primary, "duofy-button--primary")]
    [InlineData(ButtonKind.Secondary, "duofy-button--secondary")]
    [InlineData(ButtonKind.Danger, "duofy-button--danger")]
    public void Button_has_a_kind_and_a_visible_3px_ink_focus_outline(ButtonKind kind, string cssClass)
    {
        var cut = Render<DuofyButton>(p => p
            .Add(c => c.Kind, kind)
            .AddChildContent("Speichern"));

        var button = cut.Find("button");
        Assert.Contains("duofy-button", button.ClassList);
        Assert.Contains(cssClass, button.ClassList);
        Assert.Contains("Speichern", button.TextContent);

        var css = Css();
        Assert.Contains("outline: 3px solid var(--rz-text-title-color)", Rule(css, "button:focus-visible"));
    }

    // A4: Jeder Baustein hat eine Beschriftung für Screenreader.
    [Fact]
    public void Every_block_has_an_accessible_name()
    {
        var block = Render<ColorBlock>(p => p
            .Add(c => c.Variant, BlockVariant.Fixed)
            .Add(c => c.Title, "Festes")
            .Add(c => c.Amount, 1200m));
        Assert.Equal("group", block.Find(".color-block").GetAttribute("role"));
        Assert.Equal("Festes: 1.200,00 €", block.Find(".color-block").GetAttribute("aria-label"));

        var table = Render<LineTable>(p => p
            .Add(c => c.Caption, "Feste Kosten")
            .Add(c => c.Rows, new[] { new LineTableRow("Miete", 850m) }));
        Assert.Equal("Feste Kosten", table.Find("caption").TextContent);
        // Am schmalen Handy ist die Tabelle per CSS kein display: table mehr; die Rollen halten sie für Vorlese-Programme zusammen.
        Assert.Equal("table", table.Find("table").GetAttribute("role"));
        Assert.Equal("row", table.Find("tr").GetAttribute("role"));
        Assert.Equal("rowheader", table.Find("th").GetAttribute("role"));
        Assert.Equal("cell", table.Find("td").GetAttribute("role"));

        var iconOnly = Render<DuofyButton>(p => p
            .Add(c => c.Kind, ButtonKind.Danger)
            .Add(c => c.AriaLabel, "Buchung löschen")
            .AddChildContent("×"));
        Assert.Equal("Buchung löschen", iconOnly.Find("button").GetAttribute("aria-label"));
    }

    // A4: Jedes Text-Hintergrund-Paar der Bausteine hat in beiden Themes mindestens 4,5 zu 1.
    [Fact]
    public void Every_text_and_background_pair_of_the_blocks_reaches_4_5_to_1_in_light_and_dark()
    {
        var themes = new (string Name, IReadOnlyDictionary<string, string> Palette)[]
        {
            ("hell", RadzenVariables.Light),
            ("dunkel", RadzenVariables.Dark),
        };

        foreach (var (name, p) in themes)
        {
            var pairs = new (string Pair, string Text, string Background)[]
            {
                ("Farbblock Einnahmen", p["--rz-text-title-color"], Mix(p["--rz-success"], p["--rz-card-background-color"])),
                ("Farbblock Festes", p["--rz-text-title-color"], Mix(p["--rz-danger"], p["--rz-card-background-color"])),
                ("Farbblock Frei", p["--rz-text-title-color"], Mix(p["--rz-warning"], p["--rz-card-background-color"])),
                ("Knopf primär", p["--rz-on-primary"], p["--rz-primary"]),
                ("Knopf gefährlich", p["--rz-on-danger"], p["--rz-danger"]),
                ("Linientabelle auf Grund", p["--rz-text-title-color"], p["--rz-body-background-color"]),
                ("Linientabelle auf Fläche", p["--rz-text-title-color"], p["--rz-card-background-color"]),
            };

            foreach (var (pair, text, background) in pairs)
            {
                var ratio = Contrast(text, background);
                Assert.True(ratio >= 4.5, $"{pair} ({name}): {ratio:0.00} zu 1 liegt unter 4,5 zu 1");
            }
        }
    }

    // A5: Die Testseite zeigt alle Bausteine hell und dunkel nebeneinander.
    [Fact]
    public void Test_page_shows_all_blocks_in_a_light_and_a_dark_panel_in_development()
    {
        Services.AddSingleton<IDevelopmentMode>(new DevelopmentMode(true));
        var cut = Render<BuildingBlocksDemo>();

        var panels = cut.FindAll("[data-testid^='blocks-panel-']");
        Assert.Equal(2, panels.Count);
        foreach (var panel in panels)
        {
            Assert.Equal(3, panel.QuerySelectorAll(".color-block").Length);
            Assert.NotEmpty(panel.QuerySelectorAll(".line-table"));
            Assert.Equal(3, panel.QuerySelectorAll(".duofy-button").Length);
        }
        Assert.Contains("--rz-text-color: #f2efe8", cut.Find("[data-testid='blocks-panel-dark']").GetAttribute("style"));
        Assert.Contains("--rz-text-color: #424242", cut.Find("[data-testid='blocks-panel-light']").GetAttribute("style"));
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
}
