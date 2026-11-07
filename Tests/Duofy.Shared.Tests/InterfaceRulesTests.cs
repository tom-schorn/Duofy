using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Bunit;
using Duofy.Shared.Layout;
using Duofy.Shared.Services;

namespace Duofy.Shared.Tests;

/// <summary>
/// Die festen Regeln der Oberfläche nach dem Umbau (Spec 82): was die Stylesheets, die Projektdateien und die Routen einhalten
/// müssen. Ersetzt die Prüfungen der alten Stylesheets, soweit sie echtes Verhalten beschrieben. Ein echter Browser fehlt hier;
/// was nur dort zu sehen ist, wird in den Bildern und Messungen des Berichts belegt.
/// </summary>
public partial class InterfaceRulesTests
{
    private static string Web(string name) => File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", name)).Replace("\r\n", "\n");

    private static string Duofy => Web("duofy.css");

    private static string Theme => Web("theme.css");

    private static IEnumerable<string> SourceFiles(string folder, params string[] patterns) =>
        patterns.SelectMany(p => Directory.EnumerateFiles(RepositoryFiles.Path(folder), p, SearchOption.AllDirectories))
            .Where(f => !f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part is "bin" or "obj" or "node_modules"));

    // Der Block einer Medienabfrage (alle Stellen mit dieser Bedingung zusammen).
    private static string Media(string css, string condition)
    {
        var found = new System.Text.StringBuilder();
        var from = 0;
        while ((from = css.IndexOf("@media " + condition, from, StringComparison.Ordinal)) >= 0)
        {
            var open = css.IndexOf('{', from);
            var depth = 0;
            var i = open;
            for (; i < css.Length; i++)
            {
                if (css[i] == '{') depth++;
                else if (css[i] == '}' && --depth == 0) break;
            }
            found.Append(css, open + 1, i - open - 1).Append('\n');
            from = i;
        }
        return found.ToString();
    }

    private static string Rule(string css, string selector)
    {
        var match = Regex.Match(css, @"(?<![\w.-])" + Regex.Escape(selector) + @"\s*\{([^}]*)\}");
        Assert.True(match.Success, $"Regel '{selector}' fehlt");
        return match.Groups[1].Value;
    }

    // ---- A1 ----

    // A1 (R1): Keine Projektdatei und keine Paketliste nennt die alte Bibliothek. Doku und Historie dürfen es.
    [Fact]
    public void A1_no_project_file_or_package_list_names_the_old_library()
    {
        var oldLibrary = string.Concat("Rad", "zen");
        var files = new[] { "Shared", "Web", "Mobile", "Tests", "Backend", "deploy", "scripts" }
            .Where(f => Directory.Exists(RepositoryFiles.Path(f)))
            .SelectMany(f => SourceFiles(f, "*.cs", "*.razor", "*.csproj", "*.css", "*.js", "*.html", "*.json", "*.resx", "*.props", "*.yml", "*.yaml", "*.sh", "*.xml"))
            .Concat([RepositoryFiles.Path("Directory.Build.props"), RepositoryFiles.Path("Duofy.slnx")]);

        var findings = files.Where(f => File.ReadAllText(f).Contains(oldLibrary, StringComparison.OrdinalIgnoreCase)
                                        || Path.GetFileName(f).Contains(oldLibrary, StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetRelativePath(RepositoryFiles.Root, f)).ToList();

        Assert.True(findings.Count == 0, "Gefunden in:\n" + string.Join("\n", findings));
    }

    // ---- A3 ----

    [GeneratedRegex(@"(?<![&\w])#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{3})\b|\b(?:rgba?|hsla?|oklch|oklab|lab|lch)\(|:\s*(?:white|black)\b")]
    private static partial Regex FixedColour();

    // A3 (R2): Feste Farbwerte stehen nur in der Theme-Datei. Die Ausnahme ist das Weiß unter dem QR-Code, das ein Lesegerät braucht.
    [Fact]
    public void A3_fixed_colours_stand_only_in_the_theme_file()
    {
        var findings = new List<string>();
        foreach (var file in SourceFiles("Shared/Duofy.Shared", "*.razor", "*.css").Concat(SourceFiles("Web/Duofy.Web", "*.razor", "*.css")))
        {
            if (Path.GetFileName(file) == "theme.css") continue;
            foreach (var (line, number) in File.ReadAllLines(file).Select((l, i) => (l, i + 1)))
            {
                if (FixedColour().IsMatch(line) && !line.Contains("qr", StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add($"{Path.GetRelativePath(RepositoryFiles.Root, file)}:{number}: {line.Trim()}");
                }
            }
        }

        Assert.True(findings.Count == 0, "Feste Farben:\n" + string.Join("\n", findings));
    }

    // Gegenprobe: Der Filter findet wirklich Farben und übergeht Zeichen wie &#10005; und Sprungziele.
    [Theory]
    [InlineData("color: #fff;", true)]
    [InlineData("background: #1e3a5f;", true)]
    [InlineData("color: rgb(1, 2, 3);", true)]
    [InlineData("color: white;", true)]
    [InlineData("&#10005;", false)]
    [InlineData("<a href=\"#main\">", false)]
    [InlineData("color: var(--primary);", false)]
    public void The_colour_filter_finds_colours_and_nothing_else(string line, bool expected) =>
        Assert.Equal(expected, FixedColour().IsMatch(line));

    // ---- Breitenbereiche ----

    // A1 (R1, R2): Handy bis 599, Tablet 600 bis 839, Seitenleiste voll ab 840, Erklärspalte ab 1280.
    [Fact]
    public void A1_the_width_steps_are_599_600_839_840_and_1280()
    {
        Assert.Equal(599, WidthRanges.PhoneMax);
        Assert.Equal(WidthRanges.PhoneMax + 1, WidthRanges.TabletMin);
        Assert.Equal(600, WidthRanges.TabletMin);
        Assert.Equal(839, WidthRanges.TabletMax);
        Assert.Equal(WidthRanges.TabletMax + 1, WidthRanges.DesktopMin);
        Assert.Equal(840, WidthRanges.DesktopMin);
        Assert.Equal(1280, WidthRanges.HelpColumnMin);
        Assert.Equal(WidthRanges.HelpColumnMin - 1, WidthRanges.TouchMax);

        // Das Stylesheet wiederholt genau diese Werte.
        Assert.Contains($"@media (max-width: {WidthRanges.PhoneMax}px)", Duofy);
        Assert.Contains($"@media (min-width: {WidthRanges.TabletMin}px) and (max-width: {WidthRanges.TabletMax}px)", Duofy);
        Assert.Contains($"@media (min-width: {WidthRanges.DesktopMin}px)", Duofy);
        Assert.Contains($"@media (min-width: {WidthRanges.HelpColumnMin}px)", Duofy);
        Assert.DoesNotContain("767px", Duofy);
        Assert.DoesNotContain("768px", Duofy);
    }

    // Das Stylesheet kann keine C#-Werte lesen; es darf nur an den festgelegten Grenzen umbrechen.
    [Fact]
    public void The_stylesheet_only_breaks_at_the_defined_widths()
    {
        var allowed = new[]
        {
            $"max-width: {WidthRanges.PhoneMax}px", $"min-width: {WidthRanges.TabletMin}px",
            $"min-width: {WidthRanges.TabletMin}px) and (max-width: {WidthRanges.TabletMax}px", $"min-width: {WidthRanges.DesktopMin}px",
            $"min-width: {WidthRanges.HelpColumnMin}px", $"max-width: {WidthRanges.TouchMax}px",
        };

        var used = Regex.Matches(Duofy, @"@media\s*\(([^{]*?)\)\s*\{").Select(m => m.Groups[1].Value).Distinct().ToList();

        Assert.NotEmpty(used);
        Assert.All(used, condition => Assert.Contains(condition, allowed));
    }

    // A14, A21 (R12, R19, Tom 03.11.2026): Am Handy keine Seitenleiste, aber die untere Leiste mit fünf Zielen; am Tablet die
    // Seitenleiste auf Symbole eingeklappt und keine untere Leiste; die Erklärspalte erst ab dem PC.
    [Fact]
    public void A14_A21_phone_has_the_bottom_bar_and_tablet_the_collapsed_side_bar()
    {
        var phone = Media(Duofy, $"(max-width: {WidthRanges.PhoneMax}px)");
        var tablet = Media(Duofy, $"(min-width: {WidthRanges.TabletMin}px) and (max-width: {WidthRanges.TabletMax}px)");
        var desktop = Media(Duofy, $"(min-width: {WidthRanges.HelpColumnMin}px)");

        Assert.Contains("display: none", Rule(phone, ".shell-side, .shell-side-toggle"));
        Assert.Contains("position: fixed", Rule(phone, ".bottom-bar"));
        Assert.Contains("repeat(5, 1fr)", Rule(phone, ".bottom-bar"));
        Assert.Contains("display: none", Rule(Duofy, ".bottom-bar"));
        Assert.Contains("--side-width: 56px", Rule(tablet, ".shell:not(.shell--side-toggled)"));
        Assert.Contains("display: none", Rule(Duofy, ".help-column"));
        Assert.Contains("display: block", Rule(desktop, ".help-column"));
    }

    [Fact]
    public void The_stylesheets_have_balanced_braces()
    {
        foreach (var css in new[] { Duofy, Theme })
        {
            Assert.Equal(css.Count(c => c == '{'), css.Count(c => c == '}'));
        }
    }

    // A5 (R4): Nichts schiebt die Seite breiter als das Fenster; der Rahmen lässt Inhalt schrumpfen.
    [Fact]
    public void The_frame_lets_its_content_shrink_so_the_page_never_gets_wider_than_the_window()
    {
        Assert.Contains("min-width: 0", Rule(Duofy, ".shell-main"));
        Assert.Contains("flex-wrap: wrap", Rule(Duofy, ".row"));
        Assert.Contains("flex-wrap: wrap", Rule(Duofy, ".row-between"));
        Assert.Contains("overflow-wrap: anywhere", Rule(Duofy, ".long-link"));
        Assert.Contains("max-width: 100vw", Media(Duofy, $"(max-width: {WidthRanges.PhoneMax}px)"));
    }

    // ---- Tippen, Rand, Fokus ----

    [Fact]
    public void Below_the_desktop_everything_to_tap_is_at_least_44_px()
    {
        var below = Media(Duofy, $"(max-width: {WidthRanges.TouchMax}px)");

        Assert.Contains("min-height: 44px", Rule(below, ".duofy-button"));
        Assert.Contains("min-width: 44px", Rule(below, ".duofy-button"));
        Assert.Contains("min-height: 44px", Rule(below, ".side-link"));
        Assert.Contains("min-height: 56px", Rule(Media(Duofy, $"(max-width: {WidthRanges.PhoneMax}px)"), ".bottom-item"));
        Assert.Contains("min-height: 44px", Rule(below, ".back-link"));
        // Das Kästchen ist 18 Pixel groß; seine Tippfläche reicht 13 Pixel über jeden Rand hinaus.
        Assert.Contains("inset: -13px", Rule(below, ".tick-box::after"));
    }

    // Am PC bleiben die Knöpfe so hoch wie im alten Duofy.
    [Fact]
    public void On_the_desktop_a_button_is_32_px_high_like_in_the_old_duofy()
    {
        Assert.Contains(".duofy-button { height: 32px; }", Duofy);
        // Die Regel fürs Tippen steht danach, damit sie sie unter 1.024 Pixeln überschreibt.
        Assert.True(Duofy.IndexOf(".duofy-button { height: 32px; }", StringComparison.Ordinal) < Duofy.IndexOf("min-height: 44px", StringComparison.Ordinal));
    }

    // Gefunden im Bildvergleich: Die Regel "a { color: inherit }" schlägt die Farbe von Blueprint, weil dessen Regeln in einem Layer liegen.
    [Fact]
    public void A_primary_button_that_is_a_link_keeps_its_light_text() =>
        Assert.Contains("var(--primary-foreground)", Rule(Duofy, "a.duofy-button--primary"));

    // Die App läuft bis unter die Statusleiste und die Gestenleiste; beide dürfen nie Inhalt überdecken (R3).
    [Fact]
    public void The_bars_reserve_the_safe_areas_of_the_phone()
    {
        var below = Media(Duofy, $"(max-width: {WidthRanges.PhoneMax}px)");

        Assert.Contains("env(safe-area-inset-bottom)", Rule(below, ".bottom-bar"));
        Assert.Contains("env(safe-area-inset-bottom)", Rule(below, ".shell-content"));
        // Statusleiste und Navigationsleiste rechnet Android selbst heraus, bevor die Web-Oberfläche zeichnet.
        var activity = File.ReadAllText(RepositoryFiles.Path("Mobile", "Duofy", "Platforms", "Android", "MainActivity.cs"));
        Assert.Contains("SetOnApplyWindowInsetsListener", activity);
        Assert.Contains("WindowInsetsCompat.Type.SystemBars()", activity);
    }

    [Fact]
    public void Everything_that_takes_the_focus_shows_it()
    {
        Assert.Contains("outline: 2px solid var(--ring)", Rule(Duofy, ":focus-visible"));
        Assert.Contains("outline: 2px solid var(--ring)", Rule(Duofy, ".field-input:focus-visible"));
    }

    // Niemand darf am Zoomen gehindert werden.
    [Theory]
    [InlineData("Web/Duofy.Web/Components/App.razor")]
    [InlineData("Mobile/Duofy/wwwroot/index.html")]
    public void The_pages_can_be_zoomed(string file)
    {
        var page = File.ReadAllText(RepositoryFiles.Path(file.Split('/')));

        Assert.Contains("name=\"viewport\"", page);
        Assert.DoesNotContain("user-scalable=no", page);
        Assert.DoesNotContain("maximum-scale", page);
    }

    // Dialoge liegen über beiden Leisten; das Blueprint-Fenster nimmt z-index 50 und mehr.
    [Fact]
    public void The_bars_lie_below_the_dialogs()
    {
        var below = Media(Duofy, $"(max-width: {WidthRanges.PhoneMax}px)");

        Assert.True(int.Parse(Regex.Match(Rule(below, ".bottom-bar"), @"z-index: (\d+)").Groups[1].Value, CultureInfo.InvariantCulture) < 50);
        Assert.True(int.Parse(Regex.Match(Rule(Duofy, ".shell-top"), @"z-index: (\d+)").Groups[1].Value, CultureInfo.InvariantCulture) < 50);
    }

    // Das Einwilligungsfeld verdeckt die untere Leiste nicht: Es liegt über ihr und seine Knöpfe sind groß genug zum Tippen.
    [Fact]
    public void The_consent_banner_stays_above_the_bottom_bar()
    {
        var below = Media(Duofy, $"(max-width: {WidthRanges.PhoneMax}px)");

        Assert.Matches(@"bottom: calc\(\d+px \+ env\(safe-area-inset-bottom\)\)", Rule(below, ".consent-banner"));
    }

    // Die Einträge rollen, wenn der Bildschirm zu niedrig ist; das Benutzermenü bleibt unten stehen.
    [Fact]
    public void The_entries_of_the_side_bar_scroll()
    {
        Assert.Contains("overflow-y: auto", Rule(Duofy, ".side-nav"));
        Assert.Contains("flex: 1", Rule(Duofy, ".side-nav"));
    }

    // Ein langer Name schiebt weder das Profil noch die obere Leiste breiter: Er wird gekürzt.
    [Fact]
    public void A_long_name_is_shortened_in_the_profile_and_the_top_bar()
    {
        Assert.Contains("text-overflow: ellipsis", Rule(Duofy, ".user-trigger-text span"));
        Assert.Contains("text-overflow: ellipsis", Rule(Duofy, ".shell-view"));
        Assert.Contains("text-overflow: ellipsis", Rule(Duofy, ".side-label"));
    }

    // ---- Schrift ----

    [Fact]
    public void The_font_comes_from_our_own_file_with_its_licence_and_source_next_to_it()
    {
        Assert.Contains("url('fonts/geist.woff2')", Theme);
        Assert.Contains("--font-sans: 'Geist Variable', sans-serif", Theme);
        foreach (var file in new[] { "geist.woff2", "LICENSE-Geist-OFL.txt", "QUELLE.txt" })
        {
            Assert.True(File.Exists(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "fonts", file)), file);
        }
        Assert.Contains("@fontsource-variable/geist", Web("fonts/QUELLE.txt"));
        Assert.Contains("SIL Open Font License", Web("fonts/LICENSE-Geist-OFL.txt"));
    }

    // Datenschutz: Weder Schrift noch Stil laden etwas von einem fremden Rechner nach.
    [Fact]
    public void The_stylesheets_load_nothing_from_another_host()
    {
        foreach (var css in new[] { Duofy, Theme })
        {
            Assert.DoesNotMatch(@"url\(\s*['""]?(https?:)?//", css);
            Assert.DoesNotContain("@import", css);
        }
    }

    // A18 (R15, Tom: "Schriftarten teils total unterschiedlich"): Außer der Serifen-Schrift für Überschriften gibt es genau eine
    // Schriftfamilie, für Texte und Beträge. Keine Datei setzt eine eigene Schrift, auch keine Schreibmaschinenschrift.
    [Fact]
    public void A18_besides_the_heading_serif_there_is_exactly_one_font_family()
    {
        var families = Regex.Matches(Theme, @"--font-([\w-]+):").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["heading", "sans"], families.Order().ToArray());
        Assert.Contains("serif", Regex.Match(Theme, @"--font-heading:([^;]*);").Groups[1].Value);

        var files = SourceFiles("Shared", "*.css", "*.razor").Concat(SourceFiles("Web", "*.css", "*.razor")).ToList();
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("monospace", text);
            Assert.DoesNotContain("--font-mono", text);
            foreach (Match declaration in Regex.Matches(text, @"font-family\s*[:=]\s*([^;}""]+)"))
            {
                var value = declaration.Groups[1].Value.Trim();
                Assert.True(value is "var(--font-sans)" or "var(--font-heading)" or "'Geist Variable'",
                    $"{Path.GetFileName(file)} setzt eine eigene Schrift: {value}");
            }
        }
    }

    [Fact]
    public void Amounts_use_figures_of_the_same_width()
    {
        Assert.Contains("tabular-nums", Rule(Duofy, ".line-table-amount"));
        Assert.Contains("tabular-nums", Rule(Duofy, ".list-row-amount"));
        Assert.Contains("tabular-nums", Rule(Duofy, ".metric-amount"));
    }

    // ---- Kontrast ----

    private static (double R, double G, double B) Rgb(string hex) => (
        int.Parse(hex[1..3], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d,
        int.Parse(hex[3..5], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d,
        int.Parse(hex[5..7], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d);

    private static double Luminance(string hex)
    {
        static double Linear(double c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        var (r, g, b) = Rgb(hex);
        return 0.2126 * Linear(r) + 0.7152 * Linear(g) + 0.0722 * Linear(b);
    }

    private static double Contrast(string a, string b)
    {
        var (light, dark) = (Luminance(a), Luminance(b));
        return (Math.Max(light, dark) + 0.05) / (Math.Min(light, dark) + 0.05);
    }

    private static string Value(string mode, string variable)
    {
        var block = Theme[Theme.IndexOf(mode + " {", StringComparison.Ordinal)..];
        block = block[..block.IndexOf('}')];
        return Regex.Match(block, Regex.Escape(variable) + @":\s*(#[0-9a-fA-F]{6})").Groups[1].Value;
    }

    // Text erreicht in beiden Modi 4,5 zu 1 auf den Flächen, auf denen er steht (WCAG AA).
    [Theory]
    [InlineData(":root", "--foreground", "--background")]
    [InlineData(":root", "--foreground", "--card")]
    [InlineData(":root", "--muted-foreground", "--background")]
    [InlineData(":root", "--muted-foreground", "--card")]
    [InlineData(":root", "--muted-foreground", "--muted")]
    [InlineData(":root", "--primary-foreground", "--primary")]
    [InlineData(":root", "--secondary-foreground", "--secondary")]
    [InlineData(":root", "--accent-foreground", "--accent")]
    [InlineData(":root", "--destructive", "--background")]
    [InlineData(":root", "--sidebar-foreground", "--sidebar")]
    [InlineData(".dark", "--foreground", "--background")]
    [InlineData(".dark", "--foreground", "--card")]
    [InlineData(".dark", "--muted-foreground", "--background")]
    [InlineData(".dark", "--muted-foreground", "--card")]
    [InlineData(".dark", "--muted-foreground", "--muted")]
    [InlineData(".dark", "--primary-foreground", "--primary")]
    [InlineData(".dark", "--destructive", "--background")]
    [InlineData(".dark", "--destructive", "--card")]
    [InlineData(".dark", "--sidebar-foreground", "--sidebar")]
    public void Text_reaches_4_5_to_1(string mode, string text, string surface)
    {
        var ratio = Contrast(Value(mode, text), Value(mode, surface));

        Assert.True(ratio >= 4.5, $"{mode} {text} auf {surface}: {ratio:0.00} zu 1");
    }

    // Dunkel beim ersten Zeichnen: Die App setzt die Klasse aus dem Gerät, bevor das Stylesheet greift, der Browser über sein frühes Skript.
    [Fact]
    public void Dark_is_set_before_the_first_paint()
    {
        var app = File.ReadAllText(RepositoryFiles.Path("Mobile", "Duofy", "wwwroot", "index.html"));
        Assert.Contains("prefers-color-scheme: dark", app);
        Assert.Contains("classList.add('dark')", app);
        Assert.True(app.IndexOf("classList.add('dark')", StringComparison.Ordinal) < app.IndexOf("theme.css", StringComparison.Ordinal));

        Assert.Contains("ThemeBoot.HeadMarkup", File.ReadAllText(RepositoryFiles.Path("Web", "Duofy.Web", "Components", "App.razor")));
    }

    // ---- A7 ----

    // Die Routen auf main vor dem Umbau.
    public static TheoryData<string> RoutesOfMain =>
    [
        "/", "/admin", "/anmelden", "/bausteine", "/bestaetigen", "/buchungen", "/diagramme", "/einladung/{Token}", "/email-bestaetigen",
        "/haushalt", "/haushalt/freigaben", "/haushaltsplan", "/haushaltsplan/{Year:int}/{Month:int}", "/konten", "/konten/{AccountId:guid}",
        "/konto", "/konto/loeschen", "/lizenzen", "/mehr", "/passwort-neu", "/plan", "/plan/{Year:int}/{Month:int}", "/rechtliches/{Slug}",
        "/registrieren", "/schritt-dialog", "/sparen", "/vertraege",
    ];

    // A7 (R5): Jede Route von main gibt es weiter; keine führt ins Leere.
    [Theory]
    [MemberData(nameof(RoutesOfMain))]
    public void A7_every_route_of_main_still_exists(string route)
    {
        var pages = typeof(AppChrome).Assembly.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), false))
            .Cast<Microsoft.AspNetCore.Components.RouteAttribute>()
            .Select(r => r.Template);

        Assert.Contains(route, pages);
    }

    // ---- Lizenzen ----

    // Die Liste nennt dieselbe Version von Blueprint, die das Projekt einbindet, und keine Pakete, die entfernt sind.
    [Theory]
    [InlineData("Blazor Blueprint", "BlazorBlueprint.Components")]
    [InlineData("BlazorBlueprint.Icons.Lucide", "BlazorBlueprint.Icons.Lucide")]
    public void The_licence_list_names_the_pinned_version(string listed, string package)
    {
        var project = XDocument.Load(RepositoryFiles.Path("Shared", "Duofy.Shared", "Duofy.Shared.csproj"));
        var reference = project.Descendants("PackageReference").Single(e => (string?)e.Attribute("Include") == package);

        Assert.Equal((string?)reference.Attribute("Version"), Assert.Single(ThirdPartyLicenses.All, l => l.Name == listed).Version);
    }

    // Die Zeilen stehen so in der Datei LICENSE der jeweiligen Quelle (geprüft am 02.11.2026).
    [Theory]
    [InlineData("Blazor Blueprint", "Copyright 2025-present Mathew Taylor")]
    [InlineData("BlazorBlueprint.Icons.Lucide", "Copyright (c) 2025-present Mathew Taylor")]
    [InlineData("Lucide", "Copyright (c) 2026 Lucide Icons and Contributors")]
    [InlineData("Lucide", "Copyright (c) 2013-present Cole Bemis")]
    [InlineData("TailwindMerge.NET", "Copyright (c) 2024 desmondinho")]
    [InlineData("Geist", "Copyright 2024 The Geist Project Authors (https://github.com/vercel/geist-font)")]
    public void The_licence_list_has_the_copyright_line_of_the_source(string listed, string line) =>
        Assert.Contains(line, Assert.Single(ThirdPartyLicenses.All, l => l.Name == listed).Text);

    [Theory]
    [InlineData("Inter")]
    [InlineData("Material Symbols")]
    public void The_licence_list_names_nothing_that_is_gone(string name) =>
        Assert.DoesNotContain(ThirdPartyLicenses.All, l => l.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
}
