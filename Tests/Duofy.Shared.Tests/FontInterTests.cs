using System.Text.RegularExpressions;
using Duofy.Shared.Theme;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 2 (Schrift Inter lokal einbinden): Inter statt Geist, ausgeliefert mit der Anwendung, ohne Fremdserver.
/// </summary>
public partial class FontInterTests
{
    private static string Css() => File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "app.css"));

    private static string FontsDirectory() => RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "fonts");

    // A1: Die Schriftfamilie, die der Browser für den Text berechnet, ist die erste der Liste im Theme.
    // Sie muss Inter heißen und in app.css als @font-face auf eine lokale Datei zeigen.
    [Fact]
    public void Theme_uses_inter_as_first_font_family()
    {
        var family = RadzenVariables.Common["--rz-text-font-family"];

        Assert.StartsWith("'Inter Variable'", family);
        Assert.DoesNotContain("Geist", family, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Css_declares_inter_as_font_face_from_a_local_file()
    {
        var css = Css();

        Assert.Matches(@"@font-face\s*\{[^}]*font-family:\s*'Inter Variable'[^}]*src:\s*url\('fonts/inter-latin-wght-normal\.woff2'\)", css);
        Assert.True(File.Exists(Path.Combine(FontsDirectory(), "inter-latin-wght-normal.woff2")));
    }

    // R1: Fehlt die Datei, bleibt die Seite lesbar (Rückfall auf die Systemschrift).
    [Fact]
    public void Font_list_ends_in_a_system_fallback()
    {
        var family = RadzenVariables.Common["--rz-text-font-family"];

        Assert.EndsWith("sans-serif", family);
    }

    // A2: Keine Anfrage an einen fremden Host: weder @import noch url() mit Adresse in app.css.
    [Fact]
    public void Css_loads_nothing_from_another_host()
    {
        var css = Css();

        Assert.DoesNotContain("@import", css);
        Assert.DoesNotMatch(@"url\(\s*['""]?(https?:)?//", css);
        Assert.DoesNotContain("fonts.googleapis.com", css);
        Assert.DoesNotContain("fonts.gstatic.com", css);
    }

    // A3: Lizenztext neben den Schriftdateien, Quelle und Version in der Doku.
    [Fact]
    public void Inter_license_lies_next_to_the_font_and_docs_name_source_and_version()
    {
        var license = File.ReadAllText(Path.Combine(FontsDirectory(), "LICENSE-Inter-OFL.txt"));
        Assert.Contains("SIL OPEN FONT LICENSE Version 1.1", license);
        Assert.Contains("Inter", license);

        var readme = File.ReadAllText(RepositoryFiles.Path("README.md"));
        Assert.Contains("https://github.com/rsms/inter", readme);
        Assert.Contains("5.3.0", readme);
    }

    // A4: Beträge haben Ziffern gleicher Breite.
    [Fact]
    public void Css_sets_tabular_figures()
    {
        Assert.Matches(@"font-variant-numeric:\s*tabular-nums", Css());
    }

    // Issue #54: Das Theme legt nur die Variable fest; Radzen setzt sie nur an eigenen Bausteinen. Der übrige Text (Absätze,
    // Beschriftungen, Rohtext in der App) bekäme ohne diese Regel die Standardschrift des Browsers (Times). Die Regel steht
    // in der gemeinsamen app.css, die Web und App beide laden.
    [Fact]
    public void Body_text_uses_the_theme_font_family()
    {
        Assert.Matches(@"(?m)^body\s*\{[^}]*font-family:\s*var\(--rz-text-font-family\)", Css());
    }

    // A5: Keine Geist-Schriftdatei und keine Geist-Lizenz mehr im Repository.
    [Fact]
    public void No_geist_files_or_references_remain()
    {
        var found = new List<string>();
        foreach (var project in new[] { "Shared", "Web", "Mobile" })
        {
            foreach (var file in Directory.EnumerateFiles(RepositoryFiles.Path(project), "*", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
                if (Path.GetFileName(file).Contains("geist", StringComparison.OrdinalIgnoreCase)) found.Add(file);
            }
        }
        Assert.True(found.Count == 0, "Geist-Dateien gefunden: " + string.Join(", ", found));
        Assert.DoesNotContain("Geist", Css());
    }
}
