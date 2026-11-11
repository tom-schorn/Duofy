using System.Globalization;
using System.Xml.Linq;

namespace Duofy.App.Tests;

/// <summary>
/// Issue 117, Spec 118 R10: Die Knöpfe des Zeitfensters von Android (Festlegen, Abbrechen, Löschen) nehmen colorAccent. Im dunklen
/// Modus des Geräts muss diese Farbe auf den dunklen Fensterflächen lesbar sein (Kontrast mindestens 4,5:1).
/// </summary>
public class AndroidDialogColorsTests
{
    // Die dunklen Flächen der Systemfenster von Android (Material Dark: erhöhte Fläche; älteres AppCompat Dark: #424242).
    private static readonly string[] DarkDialogSurfaces = ["#2C2C2C", "#303030", "#424242"];

    private static string Color(string folder, string name) =>
        (string)XDocument.Load(RepositoryFiles.Path("Mobile", "Duofy", "Platforms", "Android", "Resources", folder, "colors.xml"))
            .Descendants("color").Single(c => (string?)c.Attribute("name") == name);

    private static double Luminance(string hex)
    {
        double Channel(int offset)
        {
            var c = int.Parse(hex.AsSpan(1 + offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(0) + 0.7152 * Channel(2) + 0.0722 * Channel(4);
    }

    private static double Contrast(string a, string b)
    {
        var (high, low) = (Math.Max(Luminance(a), Luminance(b)), Math.Min(Luminance(a), Luminance(b)));
        return (high + 0.05) / (low + 0.05);
    }

    [Fact]
    public void The_light_mode_accent_stays_the_dark_navy_of_the_brand()
    {
        Assert.Equal("#0F1729", Color("values", "colorAccent"));
    }

    [Theory]
    [InlineData("#2C2C2C")]
    [InlineData("#303030")]
    [InlineData("#424242")]
    public void In_the_dark_mode_of_the_device_the_dialog_buttons_have_a_contrast_of_at_least_four_and_a_half(string surface)
    {
        var accent = Color("values-night", "colorAccent");

        Assert.True(Contrast(accent, surface) >= 4.5, $"{accent} auf {surface}: {Contrast(accent, surface):0.0}:1");
    }

    [Fact]
    public void The_navy_accent_of_the_light_mode_would_not_be_readable_on_the_dark_surfaces()
    {
        // Das war der Fehler: dasselbe Marineblau auch im dunklen Modus.
        Assert.All(DarkDialogSurfaces, surface => Assert.True(Contrast(Color("values", "colorAccent"), surface) < 4.5));
    }
}
