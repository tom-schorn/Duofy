using Bunit;
using Duofy.Shared.Layout;
using Duofy.Shared.Services;
using Duofy.Shared.Theme;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Utilities;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 4 (Dunkelmodus als eigenes Theme): bestätigte Dunkelwerte, Kontrast, kein reines Schwarz oder Weiß,
/// Hauptseiten im Dunkelmodus. Die Werte stammen aus plagevo-landing/STRATEGIE.md (Abschnitt Dunkelmodus).
/// </summary>
public class ThemeDarkTests : UiTestContext
{
    private static PaletteDark Dark => (PaletteDark)DuofyTheme.Instance.PaletteDark;

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

    // A1: Grund, Fläche, Text, Nebentext, die drei Blöcke, Linie und Hauptknopf sind die bestätigten Werte.
    [Fact]
    public void Dark_palette_has_the_confirmed_values()
    {
        Assert.Equal("#0f1729", Hex(Dark.Background));
        Assert.Equal("#18233b", Hex(Dark.Surface));
        Assert.Equal("#f2efe8", Hex(Dark.TextPrimary));
        Assert.Equal("#b8c2d4", Hex(Dark.TextSecondary));
        Assert.Equal("#2e5a47", Hex(Dark.Success));
        Assert.Equal("#7c4330", Hex(Dark.Error));
        Assert.Equal("#745a0d", Hex(Dark.Warning));
        Assert.Equal("#3a4863", Hex(Dark.LinesDefault));
        Assert.Equal("#3a4863", Hex(Dark.Divider));
        Assert.Equal("#f6d55c", Hex(Dark.Primary));
        Assert.Equal("#14213d", Hex(Dark.PrimaryContrastText));
    }

    // A2: Jedes Text-Hintergrund-Paar des dunklen Themes hat mindestens 4,5 zu 1; auf Blöcken steht nur der Haupttext.
    [Fact]
    public void Every_text_and_background_pair_of_the_dark_theme_reaches_4_5_to_1()
    {
        var pairs = new (string Name, MudColor Text, MudColor Background)[]
        {
            ("Text auf Grund", Dark.TextPrimary, Dark.Background),
            ("Text auf Fläche", Dark.TextPrimary, Dark.Surface),
            ("Nebentext auf Grund", Dark.TextSecondary, Dark.Background),
            ("Nebentext auf Fläche", Dark.TextSecondary, Dark.Surface),
            ("Text auf Hauptknopf", Dark.PrimaryContrastText, Dark.Primary),
            ("Text auf Kopfleiste", Dark.AppbarText, Dark.AppbarBackground),
            ("Text auf Navigation", Dark.DrawerText, Dark.DrawerBackground),
            ("Text auf Einnahmen", Dark.SuccessContrastText, Dark.Success),
            ("Text auf Tertiär (Einnahmen)", Dark.TertiaryContrastText, Dark.Tertiary),
            ("Text auf Fest", Dark.ErrorContrastText, Dark.Error),
            ("Text auf Frei", Dark.WarningContrastText, Dark.Warning),
            ("Text auf Sekundär (Frei)", Dark.SecondaryContrastText, Dark.Secondary),
            ("Text auf Info", Dark.InfoContrastText, Dark.Info),
            ("Haupttext auf Einnahmen", Dark.TextPrimary, Dark.Success),
            ("Haupttext auf Fest", Dark.TextPrimary, Dark.Error),
            ("Haupttext auf Frei", Dark.TextPrimary, Dark.Warning),
        };

        foreach (var (name, text, background) in pairs)
        {
            var ratio = Contrast(text, background);
            Assert.True(ratio >= 4.5, $"{name}: {ratio:0.00} zu 1 liegt unter 4,5 zu 1");
        }
    }

    // Der Nebentext steht nur auf Grund und Fläche, nie auf einem farbigen Block.
    [Fact]
    public void Block_text_is_the_main_text_never_the_secondary_text()
    {
        var blockTexts = new[] { Dark.SuccessContrastText, Dark.ErrorContrastText, Dark.WarningContrastText, Dark.TertiaryContrastText, Dark.SecondaryContrastText, Dark.InfoContrastText };

        Assert.All(blockTexts, t => Assert.Equal(Hex(Dark.TextPrimary), Hex(t)));
    }

    // A3: Kein Farbwert des dunklen Themes ist reines Schwarz oder reines Weiß.
    [Fact]
    public void No_color_of_the_dark_theme_is_pure_black_or_pure_white()
    {
        var colors = typeof(PaletteDark).GetProperties()
            .Where(p => p.PropertyType == typeof(MudColor))
            .Select(p => (p.Name, Color: (MudColor)p.GetValue(Dark)!))
            .ToList();

        Assert.NotEmpty(colors);
        var pure = colors
            .Where(c => (c.Color.R == 0 && c.Color.G == 0 && c.Color.B == 0) || (c.Color.R == 255 && c.Color.G == 255 && c.Color.B == 255))
            .Select(c => $"{c.Name} = {Hex(c.Color)}")
            .ToList();
        Assert.True(pure.Count == 0, "Reines Schwarz oder Weiß: " + string.Join(", ", pure));
    }

    // A4 (soweit ohne echten Browser prüfbar): Das gemeinsame Layout öffnet im Dunkelmodus ohne Fehler,
    // und die Theme-Variablen tragen die dunklen Werte. Der Blick im echten Browser steht aus.
    [Fact]
    public void Main_layout_opens_in_dark_mode_with_the_dark_palette()
    {
        Services.AddSingleton<IThemePreferenceStore>(new DarkStore());
        AddAuthorization();

        var cut = Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(b => b.AddMarkupContent(0, "<p id=\"inhalt\">Hauptinhalt</p>"))));

        cut.WaitForAssertion(() => Assert.True(cut.FindComponent<MudThemeProvider>().Instance.IsDarkMode));
        Assert.Contains("Hauptinhalt", cut.Find("#inhalt").TextContent);
        Assert.Contains("--mud-palette-background: rgba(15,23,41,1)", cut.Markup);
    }

    private sealed class DarkStore : IThemePreferenceStore
    {
        public Task<ThemeChoice> LoadAsync() => Task.FromResult(ThemeChoice.Dark);
        public Task SaveAsync(ThemeChoice choice) => Task.CompletedTask;
    }
}
