namespace Duofy.Shared.Tests;

/// <summary>
/// Radzen-Umstellung: Die Knöpfe bleiben Tippflächen; dass nichts von der alten Bibliothek übrig ist, prüft RazorFilesTests.
/// </summary>
public class RadzenPlanSlicesTests
{
    [Fact]
    public void Radzen_buttons_in_the_content_are_tap_targets_of_44_px()
    {
        var css = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "app.css")).Replace("\r\n", "\n");

        Assert.Contains(".app-page .rz-button,\n.rz-dialog-content .rz-button {\n    min-height: var(--tap-min);", css);
        Assert.Contains(".rz-dialog-content .rz-button.rz-button-icon-only {\n    min-width: var(--tap-min);", css);
    }

    private static string Css() =>
        File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "app.css")).Replace("\r\n", "\n");

    // Ohne die alte Bibliothek setzt niemand sonst die Grundschrift: Die Anmeldeseiten stünden in einer Serifenschrift.
    [Fact]
    public void The_page_sets_the_base_font_itself()
    {
        var css = Css();

        Assert.Matches(@"(?m)^body \{[^}]*font-family: var\(--rz-text-font-family\)", css);
        Assert.Contains("textarea {\n    font-family: inherit;", css);
    }

    [Theory]
    [InlineData(@"\.start-section")]
    [InlineData(@"\.booking-line")]
    public void Framed_boxes_have_the_rounded_corners_of_the_theme(string selector) =>
        Assert.Matches(@"(?m)^" + selector + @" \{[^}]*border-radius: var\(--rz-card-border-radius\)", Css());
}
