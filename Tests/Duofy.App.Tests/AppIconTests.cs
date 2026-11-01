using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Duofy.App.Tests;

/// <summary>
/// App-Symbol, Startbild und Store-Kachel der Android-App (Issue 8): Sie zeigen das Plagevo-P auf Marineblau statt der
/// lila Vorlage. Die Tests lesen nur Dateien; die Prüfung an der gerenderten Datei steht im Bericht der Aufgabe.
/// </summary>
public partial class AppIconTests
{
    private const string Background = "#14213D";
    private const string Foreground = "#F2EFE8";

    // Das Zeichen P in seinen eigenen Einheiten (viewBox 0 0 64 64 aus plagevo-landing), an der gerenderten Datei gemessen.
    private const double PLeft = 14, PRight = 51, PTop = 12, PBottom = 52;

    private static readonly XDocument Project = XDocument.Load(RepositoryFiles.Path("Mobile", "Duofy", "Duofy.csproj"));

    private static string Read(params string[] parts) => File.ReadAllText(RepositoryFiles.Path(parts));

    [Fact]
    public void Icon_and_splash_use_the_marine_background_and_no_template_purple_is_left()
    {
        var icon = Project.Descendants("MauiIcon").Single();
        var splash = Project.Descendants("MauiSplashScreen").Single();

        Assert.Equal(Background, (string?)icon.Attribute("Color"));
        Assert.Equal(Background, (string?)splash.Attribute("Color"));
        Assert.Equal(@"Resources\AppIcon\appiconfg.svg", (string?)icon.Attribute("ForegroundFile"));
        foreach (var file in new[]
        {
            Read("Mobile", "Duofy", "Resources", "AppIcon", "appicon.svg"),
            Read("Mobile", "Duofy", "Resources", "AppIcon", "appiconfg.svg"),
            Read("Mobile", "Duofy", "Resources", "Splash", "splash.svg"),
            Read("Mobile", "Duofy", "Platforms", "Android", "Resources", "values", "colors.xml"),
            Read("Mobile", "Duofy", "Duofy.csproj"),
        })
        {
            Assert.DoesNotContain("512BD4", file, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void The_background_layer_is_one_plain_marine_square_and_the_foreground_is_the_light_p()
    {
        var back = Read("Mobile", "Duofy", "Resources", "AppIcon", "appicon.svg");
        var front = Read("Mobile", "Duofy", "Resources", "AppIcon", "appiconfg.svg");

        Assert.Contains($"fill=\"{Background}\"", back, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<path", back);
        Assert.Contains(Foreground, front, StringComparison.OrdinalIgnoreCase);
        Assert.Single(PathElement().Matches(front));
        Assert.DoesNotContain("<rect", front);
    }

    // Safe-Zone: Das Zeichen liegt mit allen Ecken in einem Kreis von 66 Prozent der Kantenlänge um die Mitte.
    [Theory]
    [InlineData("AppIcon", "appiconfg.svg")]
    [InlineData("Splash", "splash.svg")]
    public void The_p_lies_inside_the_safe_zone_circle_of_66_percent_around_the_centre(string folder, string file)
    {
        var svg = XDocument.Parse(Read("Mobile", "Duofy", "Resources", folder, file)).Root!;
        var size = double.Parse(((string)svg.Attribute("width")!), CultureInfo.InvariantCulture);
        var group = svg.Descendants().Single(e => e.Attribute("transform") is not null);
        var match = Transform().Match((string)group.Attribute("transform")!);
        Assert.True(match.Success, "transform=\"translate(x y) scale(s)\" erwartet");
        var (tx, ty, s) = (D(match, 1), D(match, 2), D(match, 3));

        var corners = new[] { (PLeft, PTop), (PRight, PTop), (PLeft, PBottom), (PRight, PBottom) };
        var radius = size * 0.66 / 2;
        foreach (var (x, y) in corners)
        {
            var (cx, cy) = (tx + x * s - size / 2, ty + y * s - size / 2);
            Assert.True(Math.Sqrt(cx * cx + cy * cy) <= radius, $"Ecke ({x}, {y}) liegt außerhalb der Safe-Zone");
        }

        // ... und das Zeichen ist groß genug, um etwas zu sagen: mindestens 40 Prozent der Fläche in der Höhe.
        Assert.True((PBottom - PTop) * s >= size * 0.40);
    }

    [Fact]
    public void The_store_tile_is_there_as_a_512_pixel_png_and_as_the_svg_it_comes_from()
    {
        var png = RepositoryFiles.Path("Mobile", "Store", "store-tile-512.png");
        var svg = RepositoryFiles.Path("Mobile", "Store", "store-tile-512.svg");

        Assert.True(File.Exists(svg));
        var bytes = File.ReadAllBytes(png);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, bytes[..8]);
        Assert.Equal(512, ReadInt(bytes, 16));
        Assert.Equal(512, ReadInt(bytes, 20));
        Assert.Contains(Background, File.ReadAllText(svg), StringComparison.OrdinalIgnoreCase);
    }

    // Kontrast der hellen Schrift auf dem Marineblau nach WCAG: mindestens 3:1 für Symbole, hier weit mehr.
    [Fact]
    public void The_p_has_a_strong_contrast_on_the_background()
    {
        Assert.True(Contrast(Foreground, Background) >= 7.0);
    }

    private static int ReadInt(byte[] bytes, int offset) =>
        (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];

    private static double D(Match m, int group) => double.Parse(m.Groups[group].Value, CultureInfo.InvariantCulture);

    private static double Contrast(string a, string b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(string hex)
    {
        static double Channel(int value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        var n = Convert.ToInt32(hex.TrimStart('#'), 16);
        return 0.2126 * Channel(n >> 16 & 0xFF) + 0.7152 * Channel(n >> 8 & 0xFF) + 0.0722 * Channel(n & 0xFF);
    }

    [GeneratedRegex("<path ")]
    private static partial Regex PathElement();

    [GeneratedRegex(@"translate\(\s*([\d.\-]+)[ ,]+([\d.\-]+)\s*\)\s*scale\(\s*([\d.\-]+)\s*\)")]
    private static partial Regex Transform();
}
