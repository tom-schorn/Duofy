using System.Text.RegularExpressions;
using Bunit;
using Duofy.Shared.Blocks;
using Duofy.Shared.Charts;
using Duofy.Shared.Theme;
using Microsoft.Extensions.DependencyInjection;
using Radzen.Blazor;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 12 (Diagramm-Baustein): Balkendiagramm und Soll-Ist-Balken mit Leerzustand und Textalternative.
/// Die Darstellung im Browser prüft diese Datei nur über das Stylesheet; ein Mensch sieht die Testseite einmal an.
/// </summary>
public class ChartBlockTests : UiTestContext
{
    private static string Css() => File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "app.css"));

    // Der Abschnitt "Diagramme" in app.css: vom Kommentar "Diagramme (Spec 12)" bis zum nächsten Abschnitt "/* =====".
    private static string ChartCss()
    {
        var css = Css();
        var start = css.IndexOf("/* Diagramme (Spec 12)", StringComparison.Ordinal);
        Assert.True(start >= 0, "Abschnitt 'Diagramme (Spec 12)' fehlt in app.css");
        var end = css.IndexOf("/* =====", start, StringComparison.Ordinal);
        return end >= 0 ? css[start..end] : css[start..];
    }

    private static readonly ChartBar[] Bars = [new("Miete", 100m), new("Strom", 50m), new("Internet", 25m)];

    private IRenderedComponent<BarChart> RenderBars(IReadOnlyList<ChartBar> items) =>
        Render<BarChart>(p => p.Add(c => c.Caption, "Ausgaben").Add(c => c.Items, items));

    private IRenderedComponent<TargetActualBar> RenderTargetActual(decimal target, decimal actual) =>
        Render<TargetActualBar>(p => p.Add(c => c.Caption, "Lebensmittel").Add(c => c.Target, target).Add(c => c.Actual, actual));

    private static string Text(double value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    private static IEnumerable<string> Lengths(IRenderedComponent<BarChart> cut) =>
        cut.FindComponents<RadzenProgressBar>().Select(bar => Text(bar.Instance.Value));

    // Spec 24: Der Aufrufer kann die Werte selbst schreiben (Lückendiagramm in Euro); die Tabelle für Vorlese-Programme folgt.
    [Fact]
    public void Bar_chart_writes_values_with_the_text_function_of_the_caller()
    {
        var cut = Render<BarChart>(p => p
            .Add(c => c.Caption, "Lücke")
            .Add(c => c.Items, new[] { new ChartBar("Grundbedarf", 200m) })
            .Add(c => c.ValueText, v => v.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("de-DE")) + " €"));

        Assert.Equal("200,00 €", cut.Find(".chart-bar-value").TextContent);
        Assert.Equal("200,00 €", cut.Find(".visually-hidden td").TextContent);
    }

    // A1 (R1): Der längste Wert füllt die volle Breite, die anderen anteilig.
    [Fact]
    public void Bar_chart_scales_bars_to_the_largest_value()
    {
        var cut = RenderBars(Bars);

        Assert.Equal(["100", "50", "25"], Lengths(cut));
        var rows = cut.FindAll(".chart-bar-row");
        Assert.Equal("Miete", rows[0].QuerySelector(".chart-bar-label")!.TextContent);
        Assert.Equal("100", rows[0].QuerySelector(".chart-bar-value")!.TextContent);
    }

    // A1 (R1): Kommazahlen, Null und lauter Nullen ergeben nie eine ungültige Breite.
    [Fact]
    public void Bar_chart_handles_fractions_zero_and_negative_values()
    {
        var cut = RenderBars([new("a", 3m), new("b", 1m), new("c", 0m), new("d", -5m)]);
        Assert.Equal(["100", "33.33", "0", "0"], Lengths(cut));

        var allZero = RenderBars([new("a", 0m), new("b", 0m)]);
        Assert.Equal(["0", "0"], Lengths(allZero));
    }

    // N2: Eine Quote ist ein Prozentwert; "Ist 0,00 von Soll 50,00" läse sich wie Euro.
    [Fact]
    public void A_quota_writes_its_target_and_actual_as_percent_not_as_amounts()
    {
        var cut = Render<TargetActualBar>(p => p.Add(c => c.Caption, "Wünsche").Add(c => c.Target, 50m).Add(c => c.Actual, 0m).Add(c => c.AsPercent, true));

        Assert.Equal("Ist 0 % von Soll 50 %", cut.Find(".chart-target-summary").TextContent.Trim());
        var cells = cut.FindAll(".visually-hidden td").Select(c => c.TextContent).ToList();
        Assert.Equal(["50 %", "0 %", "0 %"], cells);
        Assert.DoesNotContain("€", cut.Markup);
    }

    [Fact]
    public void A_quota_with_a_fraction_keeps_one_decimal_place()
    {
        var cut = Render<TargetActualBar>(p => p.Add(c => c.Caption, "Grundbedarf").Add(c => c.Target, 50m).Add(c => c.Actual, 38.5m).Add(c => c.AsPercent, true));

        Assert.Equal("Ist 38,5 % von Soll 50 %", cut.Find(".chart-target-summary").TextContent.Trim());
    }

    // N3: Ohne Soll, aber mit Gebuchtem ist nicht "Noch keine Daten": Der gebuchte Betrag steht da, ohne Balken.
    [Fact]
    public void Without_a_target_but_with_something_booked_the_booked_amount_is_shown_instead_of_the_empty_state()
    {
        var cut = RenderTargetActual(0m, 490m);

        Assert.Empty(cut.FindAll(".chart-empty"));
        Assert.Equal("Ist 490,00 von Soll 0,00", cut.Find(".chart-target-summary").TextContent.Trim());
        Assert.Empty(cut.FindAll(".chart-target-bar"));
    }

    [Fact]
    public void Without_a_target_and_nothing_booked_the_empty_state_stays()
    {
        var cut = RenderTargetActual(0m, 0m);

        Assert.Single(cut.FindAll(".chart-empty"));
    }

    // A2 (R2): Soll 200, Ist 250 ergibt 125 Prozent als Text; keine Fehlerfarbe.
    [Fact]
    public void Target_actual_shows_125_percent_as_text_without_an_error_colour()
    {
        var cut = RenderTargetActual(200m, 250m);

        Assert.Equal("125 %", cut.Find(".chart-percent").TextContent);
        Assert.Contains("über dem Soll", cut.Find(".chart-over-note").TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("error", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--rz-danger", ChartCss());
    }

    // A2 (R2): Die Länge zeigt Ist von Soll; über 100 Prozent bleibt der Balken in der Fläche (Soll-Marke bei 100).
    [Theory]
    [InlineData(200, 100, "50", false, "100%")]
    [InlineData(200, 200, "100", false, "100%")]
    [InlineData(200, 250, "80", true, "80%")]
    [InlineData(100, 400, "25", true, "25%")]
    public void Target_actual_bar_keeps_inside_its_area_above_100_percent(int target, int actual, string baseWidth, bool over, string mark)
    {
        var cut = RenderTargetActual(target, actual);

        var bar = cut.FindComponent<RadzenProgressBar>().Instance;
        Assert.Equal(baseWidth, Text(bar.Value));
        Assert.Contains(mark, cut.Find(".chart-target-mark").GetAttribute("style"));
        if (over)
        {
            // Der Überschuss ist der Rest des Bereichs, der blassere Teil des Balkens.
            Assert.Equal(100d, bar.BufferValue);
            Assert.NotEmpty(cut.FindAll(".chart-over-note"));
        }
        else
        {
            Assert.Null(bar.BufferValue);
            Assert.Empty(cut.FindAll(".chart-over-note"));
        }
    }

    [Fact]
    public void Target_actual_rounds_the_percentage_to_whole_numbers()
    {
        Assert.Equal("33 %", RenderTargetActual(3m, 1m).Find(".chart-percent").TextContent);
        Assert.Equal("0 %", RenderTargetActual(100m, 0m).Find(".chart-percent").TextContent);
    }

    // A3 (R3): Der Abschnitt nennt Farben nur als Variablen des Themes, nie als festen Wert.
    [Fact]
    public void Chart_styles_take_colours_only_from_theme_variables()
    {
        var section = ChartCss();
        var withoutComments = Regex.Replace(section, @"/\*.*?\*/", "", RegexOptions.Singleline);

        Assert.DoesNotMatch(@"#[0-9a-fA-F]{3,8}\b", withoutComments);
        // Nur die Farbfunktionen rgb( und rgba( sind feste Werte; "srgb" in color-mix(in srgb, ...) ist der Farbraum, keine Farbe.
        Assert.DoesNotMatch(@"rgba?\(", withoutComments);
        Assert.DoesNotContain("hsl", withoutComments, StringComparison.OrdinalIgnoreCase);

        var colourProperties = Regex.Matches(withoutComments, @"(?:^|[\s;{])(?:background-color|background|color|border(?:-[a-z]+)?|outline|fill|stroke)\s*:\s*([^;}]+)", RegexOptions.Multiline);
        Assert.NotEmpty(colourProperties);
        foreach (Match m in colourProperties)
        {
            var value = m.Groups[1].Value;
            // Breite, Art und Null der Ränder sind keine Farben.
            var colourPart = Regex.Replace(value, @"\b(\d+(\.\d+)?(px|rem)|solid|none|0|transparent|inherit)\b", "").Trim();
            // Eine Mischung aus zwei Theme-Variablen bleibt aus dem Theme, auch sie ist erlaubt.
            var themeMix = Regex.IsMatch(colourPart, @"^color-mix\(in srgb, var\(--rz-[a-z0-9-]+\)( \d+%)?, var\(--rz-[a-z0-9-]+\)( \d+%)?\)$");
            Assert.True(colourPart.Length == 0 || colourPart.StartsWith("var(--rz-") || themeMix, $"Keine Theme-Variable: '{value.Trim()}'");
        }
    }

    // A3 (R3): Der Text steht in beiden Themes auf Grund und Fläche mit mindestens 4,5 zu 1.
    [Fact]
    public void Chart_text_reaches_4_5_to_1_on_background_and_surface_in_light_and_dark()
    {
        static double Luminance(string hex)
        {
            static double Ch(int v) { var x = v / 255.0; return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }
            var c = Convert.ToInt32(hex.TrimStart('#'), 16);
            return 0.2126 * Ch(c >> 16 & 255) + 0.7152 * Ch(c >> 8 & 255) + 0.0722 * Ch(c & 255);
        }
        static double Contrast(string a, string b)
        {
            var (la, lb) = (Luminance(a), Luminance(b));
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        foreach (var (name, p) in new[] { ("hell", RadzenVariables.Light), ("dunkel", RadzenVariables.Dark) })
        {
            foreach (var background in new[] { p["--rz-body-background-color"], p["--rz-card-background-color"] })
            {
                Assert.True(Contrast(p["--rz-text-title-color"], background) >= 4.5, $"Haupttext ({name})");
                Assert.True(Contrast(p["--rz-text-secondary-color"], background) >= 4.5, $"Nebentext ({name})");
            }
        }
        // Die Füllungen tragen keinen Text; die Schrift steht neben dem Balken auf Grund oder Fläche.
        Assert.DoesNotContain("--rz-on-", ChartCss());
    }

    // A4 (R4): Ohne Einträge steht der Hinweis statt eines leeren Diagramms.
    [Fact]
    public void Bar_chart_without_items_shows_the_empty_state()
    {
        var cut = RenderBars([]);

        Assert.Equal("Noch keine Daten für dieses Diagramm.", cut.Find(".chart-empty").TextContent);
        Assert.Empty(cut.FindComponents<RadzenProgressBar>());
        Assert.Empty(cut.FindAll("table"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Target_actual_without_a_usable_target_shows_the_empty_state(int target)
    {
        // Seit N3 gilt der Leerzustand nur noch, wenn auch nichts gebucht ist.
        var cut = RenderTargetActual(target, 0m);

        Assert.Equal("Noch keine Daten für dieses Diagramm.", cut.Find(".chart-empty").TextContent);
        Assert.Empty(cut.FindComponents<RadzenProgressBar>());
    }

    // A5 (R5): Für Screenreader gibt es eine Tabelle mit allen Werten; der gemalte Teil bleibt stumm.
    [Fact]
    public void Bar_chart_has_a_table_with_every_value_and_hides_the_drawing_from_screen_readers()
    {
        var cut = RenderBars(Bars);

        var table = cut.Find(".visually-hidden table");
        Assert.Equal("Ausgaben", table.QuerySelector("caption")!.TextContent);
        var rows = table.QuerySelectorAll("tbody tr");
        Assert.Equal(3, rows.Length);
        Assert.Equal(["Miete", "100"], rows[0].Children.Select(c => c.TextContent));
        Assert.Equal(["Internet", "25"], rows[2].Children.Select(c => c.TextContent));
        Assert.Equal("true", cut.Find(".chart-bars").GetAttribute("aria-hidden"));
    }

    [Fact]
    public void Target_actual_has_a_table_with_target_actual_and_percentage()
    {
        var cut = RenderTargetActual(200m, 250m);

        var cells = cut.FindAll(".visually-hidden table tbody tr").Select(r => r.Children.Select(c => c.TextContent).ToArray()).ToArray();
        Assert.Equal(["Soll", "200,00"], cells[0]);
        Assert.Equal(["Ist", "250,00"], cells[1]);
        Assert.Equal(["Anteil am Soll", "125 %"], cells[2]);
        Assert.Equal("true", cut.Find(".chart-target-bar").GetAttribute("aria-hidden"));
    }

    // A5 (R5): Jeder Wert steht auch als Zahl sichtbar, nicht nur als Länge.
    [Fact]
    public void Every_value_is_also_visible_as_a_number()
    {
        var bars = RenderBars(Bars);
        Assert.Equal(["100", "50", "25"], bars.FindAll(".chart-bar-value").Select(v => v.TextContent));

        var target = RenderTargetActual(200m, 250m);
        Assert.Contains("250,00", target.Find(".chart-target-summary").TextContent);
        Assert.Contains("200,00", target.Find(".chart-target-summary").TextContent);
    }

    // Testseite: nur im Entwicklungsbetrieb, hell und dunkel nebeneinander, mit Leerzustand.
    [Fact]
    public void Test_page_shows_all_charts_in_a_light_and_a_dark_panel_in_development()
    {
        Services.AddSingleton<IDevelopmentMode>(new DevelopmentMode(true));
        var cut = Render<ChartsDemo>();

        var panels = cut.FindAll("[data-testid^='charts-panel-']");
        Assert.Equal(2, panels.Count);
        foreach (var panel in panels)
        {
            Assert.NotEmpty(panel.QuerySelectorAll(".chart-bar-row"));
            Assert.NotEmpty(panel.QuerySelectorAll(".chart-target-bar"));
            Assert.NotEmpty(panel.QuerySelectorAll(".chart-empty"));
        }
        Assert.Contains("--rz-text-color: #f2efe8", cut.Find("[data-testid='charts-panel-dark']").GetAttribute("style"));
        Assert.Contains("--rz-text-color: #424242", cut.Find("[data-testid='charts-panel-light']").GetAttribute("style"));
    }

    [Fact]
    public void Test_page_shows_nothing_unless_development_is_announced()
    {
        Assert.Empty(Render<ChartsDemo>().FindAll("[data-testid^='charts-panel-']"));
    }

    [Fact]
    public void Test_page_shows_nothing_in_production()
    {
        Services.AddSingleton<IDevelopmentMode>(new DevelopmentMode(false));
        Assert.Empty(Render<ChartsDemo>().FindAll("[data-testid^='charts-panel-']"));
    }
}
