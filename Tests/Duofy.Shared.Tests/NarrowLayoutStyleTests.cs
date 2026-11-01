using System.Text.RegularExpressions;

namespace Duofy.Shared.Tests;

/// <summary>
/// Offene Punkte aus der Durchsicht vom 01.11.2026 am schmalen Handy: Linientabelle, Beschriftung der Balken und die
/// Einwilligungsleiste. Das Bild prüft der Browser; diese Datei hält die Regeln fest, die es tragen.
/// </summary>
public partial class NarrowLayoutStyleTests
{
    private static string Css()
    {
        var css = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "app.css"));
        return Whitespace().Replace(Comment().Replace(css, ""), " ");
    }

    private static string Block(string head)
    {
        var css = Css();
        var start = css.IndexOf(head + " {", StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{head}' fehlt in app.css");
        var depth = 0;
        for (var i = css.IndexOf('{', start); i < css.Length; i++)
        {
            depth += css[i] == '{' ? 1 : css[i] == '}' ? -1 : 0;
            if (depth == 0)
            {
                return css[start..(i + 1)];
            }
        }
        Assert.Fail($"'{head}' ist nicht geschlossen");
        return "";
    }

    // Betrag und Plakette passen bei 360 px nicht neben die Bezeichnung: Die letzte Spalte bekommt eine eigene Zeile.
    [Fact]
    public void A_line_table_row_wraps_its_trailing_cell_on_a_narrow_phone()
    {
        var narrow = Block("@media (max-width: 479.98px)");

        Assert.Contains(".line-table tr { display: flex; flex-wrap: wrap;", narrow);
        Assert.Contains(".line-table-trail { flex: 1 0 100%;", narrow);
        Assert.Contains(".line-table-label { flex: 1 1 0; min-width: 0;", narrow);
        Assert.Contains("hyphens: auto;", narrow);
    }

    // Fünf Einträge teilen sich 360 px: "Buchungen" passt nur ohne den seitlichen Abstand der Knöpfe und etwas kleiner.
    [Fact]
    public void All_five_bottom_bar_labels_fit_on_a_narrow_phone()
    {
        Assert.Contains("padding-inline: 0;", Block(".app-bottom-item.mud-button-root"));
        Assert.Contains("--type-extra-size: min(0.875rem, 3.4vw);", Block(".app-bottom-bar"));
    }

    // Rundgang mit echten Daten: Stern, Symbol, Name, Betrag und Knöpfe passen nicht in eine Reihe; "Auflösen" lag außerhalb des Bildschirms.
    [Fact]
    public void Row_buttons_wrap_instead_of_leaving_the_screen_on_a_narrow_phone()
    {
        var narrow = Block("@media (max-width: 479.98px)");

        Assert.Contains(".data-row-frame > * { flex-wrap: wrap !important;", narrow);
        Assert.Contains(".data-row-frame > * > .flex-grow-1 { flex: 1 1 8rem;", narrow);
        Assert.Contains("white-space: nowrap;", Block(".data-amount"));
    }

    // Die Tabellenzeile am schmalen Handy ist flex: Ohne das liegt der Haken mit width: 1 % über der Bezeichnung.
    [Fact]
    public void The_tick_box_keeps_its_own_width_on_a_narrow_phone() =>
        Assert.Contains(".line-table-lead { flex: none; width: auto;", Block("@media (max-width: 479.98px)"));

    // "Abbrechen" bricht im Kopf des Dialogs nicht mitten im Wort.
    [Fact]
    public void The_cancel_button_keeps_its_word_in_one_piece() =>
        Assert.Contains("white-space: nowrap;", Block(".step-dialog-head .duofy-button"));

    // Der runde Knopf steht über der unteren Leiste, nicht halb dahinter.
    [Fact]
    public void The_bookings_plus_button_sits_above_the_bottom_bar()
    {
        var page = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "Pages", "Bookings.razor"));

        Assert.DoesNotContain("bottom: 24px", page);
        Assert.Contains("app-fab", page);
    }

    // "Grundbedarf" passt in die Spalte, lange Namen trennen an der Silbe statt irgendwo.
    [Fact]
    public void A_bar_label_keeps_a_word_like_grundbedarf_in_one_piece()
    {
        Assert.Contains("grid-template-columns: minmax(6.5rem, 1fr) minmax(0, 2fr) auto;", Block(".chart-bar-row"));

        var label = Block(".chart-bar-label");
        Assert.Contains("overflow-wrap: break-word;", label);
        Assert.Contains("hyphens: auto;", label);
        Assert.DoesNotContain("anywhere", label);
    }

    // Die Leiste kann zwei Dienste zeigen und wird hoch: Sie endet über der unteren Leiste, neben der Seitenleiste und
    // unter der App-Leiste; was nicht passt, rollt in ihr selbst.
    [Fact]
    public void The_consent_banner_covers_neither_navigation_nor_profile_button()
    {
        var banner = Block(".consent-banner");
        Assert.Contains("max-height: calc(100dvh - var(--consent-bottom, 0px) - var(--mud-appbar-height, 4rem));", banner);
        Assert.Contains("overflow-y: auto;", banner);
        Assert.Contains("bottom: var(--consent-bottom, 0px);", banner);

        Assert.Contains("--consent-bottom: calc(var(--app-bottom-bar-height) + env(safe-area-inset-bottom, 0px));", Block("body:has(.app-bottom-bar)"));
        Assert.Contains("inset-inline-start: var(--side-nav-width);", Block("body:has(.app-side-nav) .consent-banner"));
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
