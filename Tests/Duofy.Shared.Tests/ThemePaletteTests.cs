using System.Globalization;
using Duofy.Shared.Theme;
using MudBlazor;
using MudBlazor.Utilities;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 3 (Theme mit Palette, Formensprache und Linien): bestätigte helle Palette, Kontrast, eckige Formen, Typografie-Skala.
/// Die Werte stammen aus plagevo-landing/design/design-system.md.
/// </summary>
public class ThemePaletteTests
{
    private static PaletteLight Light => (PaletteLight)DuofyTheme.Instance.PaletteLight;

    private static string Hex(MudColor color) => color.ToString(MudColorOutputFormats.Hex).ToLowerInvariant();

    // Relative Luminanz nach WCAG 2.x (sRGB).
    private static double Luminance(MudColor color)
    {
        static double Channel(byte value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    private static double Contrast(MudColor a, MudColor b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    // A1: Primär (Tinte), Einnahmen (Salbei), Fest (Korall), Frei (Gelb) und Grund (Papier) sind die bestätigten Werte.
    [Fact]
    public void Light_palette_has_the_confirmed_brand_values()
    {
        Assert.Equal("#14213d", Hex(Light.Primary));
        Assert.Equal("#bfd8c8", Hex(Light.Success));
        Assert.Equal("#f2b8a0", Hex(Light.Error));
        Assert.Equal("#f6d55c", Hex(Light.Warning));
        Assert.Equal("#fbfaf6", Hex(Light.Background));
        Assert.Equal("#14213d", Hex(Light.TextPrimary));
        Assert.Equal("#3d4a60", Hex(Light.TextSecondary));
        Assert.Equal("#e9eee8", Hex(Light.DrawerBackground));
    }

    // A2: Jedes Text-Hintergrund-Paar des hellen Themes hat mindestens 4,5 zu 1.
    [Fact]
    public void Every_text_and_background_pair_of_the_light_theme_reaches_4_5_to_1()
    {
        var pairs = new (string Name, MudColor Text, MudColor Background)[]
        {
            ("Text auf Papier", Light.TextPrimary, Light.Background),
            ("Text auf Fläche", Light.TextPrimary, Light.Surface),
            ("Sekundärtext auf Papier", Light.TextSecondary, Light.Background),
            ("Sekundärtext auf Fläche", Light.TextSecondary, Light.Surface),
            ("Papier auf Primär", Light.PrimaryContrastText, Light.Primary),
            ("Papier auf Kopfleiste", Light.AppbarText, Light.AppbarBackground),
            ("Text auf Navigation", Light.DrawerText, Light.DrawerBackground),
            ("Text auf Sekundär (Frei)", Light.SecondaryContrastText, Light.Secondary),
            ("Text auf Tertiär (Einnahmen)", Light.TertiaryContrastText, Light.Tertiary),
            ("Text auf Erfolg (Einnahmen)", Light.SuccessContrastText, Light.Success),
            ("Text auf Warnung (Frei)", Light.WarningContrastText, Light.Warning),
            ("Text auf Fehler (Fest)", Light.ErrorContrastText, Light.Error),
            ("Text auf Info (Duofy)", Light.InfoContrastText, Light.Info),
            ("Tinte auf Einnahmen", Light.TextPrimary, Light.Success),
            ("Tinte auf Fest", Light.TextPrimary, Light.Error),
            ("Tinte auf Frei", Light.TextPrimary, Light.Warning),
            ("Tinte auf Hilfe", Light.TextPrimary, Light.DrawerBackground),
        };

        foreach (var (name, text, background) in pairs)
        {
            var ratio = Contrast(text, background);
            Assert.True(ratio >= 4.5, $"{name}: {ratio:0.00} zu 1 liegt unter 4,5 zu 1");
        }
    }

    // Weißer Text auf Pastellflächen ist ausgeschlossen.
    [Fact]
    public void No_pastel_surface_carries_white_text()
    {
        var pastelTexts = new[] { Light.SuccessContrastText, Light.WarningContrastText, Light.ErrorContrastText, Light.InfoContrastText, Light.SecondaryContrastText, Light.TertiaryContrastText };

        Assert.DoesNotContain(pastelTexts, t => Hex(t) is "#ffffff" or "#fbfaf6");
    }

    // Linien sind Tinte und heben sich mindestens 3 zu 1 von der Fläche ab.
    [Fact]
    public void Lines_are_ink_and_reach_3_to_1_against_the_surface()
    {
        Assert.Equal("#14213d", Hex(Light.LinesDefault));
        Assert.Equal("#14213d", Hex(Light.Divider));
        Assert.True(Contrast(Light.LinesInputs, Light.Surface) >= 3);
    }

    // A3: Die Standard-Rundung ist höchstens 4 px.
    [Fact]
    public void Default_border_radius_is_at_most_4px()
    {
        var radius = DuofyTheme.Instance.LayoutProperties.DefaultBorderRadius;

        Assert.EndsWith("px", radius);
        Assert.True(double.Parse(radius[..^2], CultureInfo.InvariantCulture) <= 4);
    }

    // A4: Die Skala hat Titel, Abschnitt, Betrag, Text und Zusatz, je mit Größe, Gewicht und Zeilenhöhe.
    [Fact]
    public void Type_scale_names_size_weight_and_line_height_for_every_role()
    {
        foreach (var role in new[] { "Title", "Section", "Amount", "Text", "Extra" })
        {
            Assert.True(DuofyTheme.TypeScale.TryGetValue(role, out var entry), $"Rolle {role} fehlt");
            Assert.False(string.IsNullOrWhiteSpace(entry!.Size));
            Assert.False(string.IsNullOrWhiteSpace(entry.Weight));
            Assert.False(string.IsNullOrWhiteSpace(entry.LineHeight));
        }
    }

    [Fact]
    public void Typography_of_the_theme_uses_the_scale()
    {
        var typography = DuofyTheme.Instance.Typography;

        Assert.Equal(DuofyTheme.TypeScale["Title"].Size, typography.H1.FontSize);
        Assert.Equal(DuofyTheme.TypeScale["Text"].Size, typography.Body1.FontSize);
        Assert.Equal(DuofyTheme.TypeScale["Extra"].Size, typography.Caption.FontSize);
        Assert.Equal("Inter Variable", typography.H1.FontFamily![0]);
    }

    // Formensprache: Flächen ohne Schatten, Fokus als 3-px-Kontur.
    [Fact]
    public void Css_removes_shadows_from_surfaces_and_draws_a_3px_focus_outline()
    {
        var css = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "app.css"));

        Assert.Matches(@"\.mud-paper[^{]*\{[^}]*box-shadow:\s*none", css);
        Assert.Matches(@":focus-visible[^{]*\{[^}]*outline:\s*3px solid", css);
    }
}
