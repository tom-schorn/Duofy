using Duofy.Shared.Theme;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 3 (Formensprache und Schrift): die Typografie-Skala und die Fokuskontur. Palette und Kontrast prüft ThemeVariablesTests.
/// </summary>
public class ThemePaletteTests
{
    // A4: Die Skala hat Titel, Abschnitt, Betrag, Text und Zusatz, je mit Größe, Gewicht und Zeilenhöhe.
    [Fact]
    public void Type_scale_names_size_weight_and_line_height_for_every_role()
    {
        foreach (var role in new[] { "Title", "Section", "Amount", "Text", "Extra" })
        {
            Assert.True(RadzenVariables.TypeScale.TryGetValue(role, out var entry), $"Rolle {role} fehlt");
            Assert.False(string.IsNullOrWhiteSpace(entry!.Size));
            Assert.False(string.IsNullOrWhiteSpace(entry.Weight));
            Assert.False(string.IsNullOrWhiteSpace(entry.LineHeight));
        }
    }

    [Fact]
    public void Css_draws_a_3px_focus_outline()
    {
        var css = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "app.css"));

        Assert.Matches(@":focus-visible[^{]*\{[^}]*outline:\s*3px solid", css);
    }
}
