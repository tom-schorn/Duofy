using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Duofy.Shared.Tests;

/// <summary>
/// A20 und A21: Kein Bootstrap mehr, und kein Text steht fest in den .razor-Dateien.
/// </summary>
public partial class RazorFilesTests
{
    // --- A20 -------------------------------------------------------------

    [Fact]
    public void No_bootstrap_files_or_classes_remain()
    {
        var found = new List<string>();
        foreach (var project in new[] { "Shared/Duofy.Shared", "Web/Duofy.Web", "Mobile/Duofy" })
        {
            foreach (var file in SourceFiles(project, "*.*"))
            {
                if (Path.GetFileName(file).Contains("bootstrap", StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(file);
                    continue;
                }
                if (!IsTextFile(file)) continue;
                if (BootstrapMarker().IsMatch(File.ReadAllText(file)))
                {
                    found.Add(file);
                }
            }
        }
        Assert.True(found.Count == 0, "Bootstrap gefunden in: " + string.Join(", ", found));
    }

    // Bootstrap-Klassen und -Dateien, die die Vorlage benutzt hat.
    [GeneratedRegex(@"bootstrap|\bnavbar\b|\bnav-link\b|\bnav-item\b|container-fluid|\bbtn btn-|\bbtn-primary\b|\bform-control\b|\bform-floating\b|\bbi bi-|\bd-flex\b|\bmb-3\b|\bcol-md-", RegexOptions.IgnoreCase)]
    private static partial Regex BootstrapMarker();

    // --- A21 -------------------------------------------------------------

    [Fact]
    public void Razor_files_of_shared_contain_no_literal_text()
    {
        var findings = new List<string>();
        foreach (var file in SourceFiles("Shared/Duofy.Shared", "*.razor"))
        {
            foreach (var text in LiteralTexts.Find(File.ReadAllText(file)))
            {
                findings.Add($"{Path.GetRelativePath(RepositoryFiles.Root, file)}: \"{text}\"");
            }
        }
        Assert.True(findings.Count == 0, "Fester Text gefunden:\n" + string.Join("\n", findings));
    }

    [Fact]
    public void Detector_finds_text_between_tags_and_in_attributes()
    {
        // Kontrolle: Der Prüfer darf nicht blind sein.
        var texts = LiteralTexts.Find(
            "<h1>Willkommen</h1>\n" +
            "<MudButton Label=\"Speichern\" />\n" +
            "<MudTextField Placeholder=\"Name\" />\n" +
            "Noch ein Satz\n" +
            "<p>@L[\"Key\"]</p>\n");

        Assert.Contains("Willkommen", texts);
        Assert.Contains("Speichern", texts);
        Assert.Contains("Name", texts);
        Assert.Contains("Noch ein Satz", texts);
        Assert.Equal(4, texts.Count);
    }

    [Fact]
    public void Every_localizer_key_used_exists_in_the_resource_file()
    {
        var resx = XDocument.Load(RepositoryFiles.Path("Shared", "Duofy.Shared", "Resources", "SharedResources.resx"));
        var keys = resx.Root!.Elements("data").Select(d => (string)d.Attribute("name")!).ToHashSet();

        var missing = new List<string>();
        foreach (var file in SourceFiles("Shared/Duofy.Shared", "*.*").Where(f => f.EndsWith(".razor") || f.EndsWith(".cs")))
        {
            foreach (Match match in LocalizerKey().Matches(File.ReadAllText(file)))
            {
                var key = match.Groups[1].Value;
                if (!keys.Contains(key))
                {
                    missing.Add($"{Path.GetFileName(file)}: {key}");
                }
            }
        }
        Assert.True(missing.Count == 0, "Schlüssel fehlen in SharedResources.resx: " + string.Join(", ", missing));
        Assert.NotEmpty(keys);
    }

    // Wir benutzen überall den Namen L für den Localizer: L["Schluessel"] oder L["Schluessel", wert].
    [GeneratedRegex(@"\bL\[""([^""]+)""")]
    private static partial Regex LocalizerKey();

    // --- Hilfen ----------------------------------------------------------

    private static IEnumerable<string> SourceFiles(string project, string pattern) =>
        Directory.EnumerateFiles(RepositoryFiles.Path(project), pattern, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

    private static bool IsTextFile(string file) =>
        Path.GetExtension(file).ToLowerInvariant() is ".razor" or ".cs" or ".css" or ".html" or ".cshtml" or ".js" or ".csproj" or ".xml" or ".xaml" or ".json";
}

/// <summary>
/// Sucht in einer .razor-Datei nach fest geschriebenem Text: zwischen Tags
/// und in den Attributen Text, Label, Placeholder, Title und ähnlichen.
/// </summary>
internal static partial class LiteralTexts
{
    public static List<string> Find(string razor)
    {
        // Den C#-Teil und Kommentare brauchen wir nicht zu prüfen.
        var codeStart = razor.IndexOf("@code", StringComparison.Ordinal);
        if (codeStart >= 0) razor = razor[..codeStart];
        razor = RazorComment().Replace(razor, "");

        var found = new List<string>();

        foreach (Match m in TextAttribute().Matches(razor))
        {
            found.Add(m.Groups[2].Value.Trim());
        }

        foreach (var rawLine in razor.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            // Text zwischen zwei Tags in einer Zeile: >Text<
            foreach (Match m in BetweenTags().Matches(line))
            {
                var text = m.Groups[1].Value.Trim();
                if (HasWords(text)) found.Add(text);
            }

            // Eine Zeile, die nur aus Text besteht.
            if (!line.StartsWith('<') && !line.StartsWith('@') && !line.StartsWith('{') && !line.StartsWith('}')
                && !line.Contains('=') && !line.Contains('"') && !line.StartsWith('/') && !line.StartsWith('>')
                && !line.StartsWith("else")
                && !line.Contains('<') && HasWords(line))
            {
                found.Add(line);
            }
        }
        return found;
    }

    private static bool HasWords(string text) => LetterRun().IsMatch(text);

    [GeneratedRegex(@"@\*.*?\*@", RegexOptions.Singleline)]
    private static partial Regex RazorComment();

    [GeneratedRegex(@"\b(Text|Label|Placeholder|Title|HelperText|ErrorText|Tooltip|aria-label|title)=""([^""@]*[A-Za-zÄÖÜäöü][^""@]*)""")]
    private static partial Regex TextAttribute();

    [GeneratedRegex(@">([^<>@]+)<")]
    private static partial Regex BetweenTags();

    [GeneratedRegex(@"[A-Za-zÄÖÜäöü]{2,}")]
    private static partial Regex LetterRun();
}
