using Bunit;
using Duofy.Shared.Icons;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 7 (Tabler-Symbole als Baustein mit Lizenzdatei): Inline-SVG in der Textfarbe, lokal, mit Lizenz,
/// nur die Symbole der Liste, beschriftet oder verborgen.
/// </summary>
public class TablerIconTests : BunitContext
{
    private static readonly string[] InitialSet =
        ["cash", "building-bank", "credit-card", "pig-money", "wallet", "repeat", "arrows-exchange", "coins"];

    private static string IconsDirectory() => RepositoryFiles.Path("Shared", "Duofy.Shared", "Icons");

    // A1 (R1): Das SVG erbt die Textfarbe über currentColor.
    [Fact]
    public void Icon_renders_inline_svg_with_the_current_text_color()
    {
        var cut = Render<TablerIcon>(p => p.Add(c => c.Name, "credit-card"));

        var svg = cut.Find("svg");
        Assert.Equal("currentColor", svg.GetAttribute("stroke"));
        Assert.Equal("none", svg.GetAttribute("fill"));
        Assert.Contains("<path", svg.InnerHtml);
    }

    // A2 (R2): Lizenztext im Projekt, Quelle und Version in der Doku.
    [Fact]
    public void Tabler_license_lies_in_the_project_and_docs_name_source_and_version()
    {
        var license = File.ReadAllText(Path.Combine(IconsDirectory(), "LICENSE-Tabler-MIT.txt"));
        Assert.Contains("MIT License", license);
        Assert.Contains("Copyright (c) 2020-2026", license);

        var readme = File.ReadAllText(RepositoryFiles.Path("README.md"));
        Assert.Contains("https://github.com/tabler/tabler-icons", readme);
        Assert.Contains("@tabler/icons", readme);
        Assert.Contains("3.50.0", readme);
    }

    // A3 (R3): Die ausgelieferten SVG-Dateien sind genau die der Liste, auf der Platte und im Assembly.
    [Fact]
    public void Shipped_svg_files_are_exactly_the_listed_ones()
    {
        var onDisk = Directory.EnumerateFiles(Path.Combine(IconsDirectory(), "svg"), "*.svg")
            .Select(Path.GetFileNameWithoutExtension).Order().ToArray();
        Assert.Equal(TablerIcons.Names.Order().ToArray(), onDisk);

        var embedded = typeof(TablerIcon).Assembly.GetManifestResourceNames()
            .Where(n => n.EndsWith(".svg", StringComparison.Ordinal)).ToArray();
        Assert.Equal(TablerIcons.Names.Count, embedded.Length);
    }

    // A4 (R4): Mit Beschriftung Rolle img und Name, ohne Beschriftung aria-hidden.
    [Fact]
    public void Labelled_icon_is_an_image_with_a_name_and_unlabelled_icon_is_hidden()
    {
        var labelled = Render<TablerIcon>(p => p.Add(c => c.Name, "wallet").Add(c => c.Label, "Geldbörse"));
        var svg = labelled.Find("svg");
        Assert.Equal("img", svg.GetAttribute("role"));
        Assert.Equal("Geldbörse", svg.GetAttribute("aria-label"));
        Assert.Null(svg.GetAttribute("aria-hidden"));

        var decorative = Render<TablerIcon>(p => p.Add(c => c.Name, "wallet"));
        var hidden = decorative.Find("svg");
        Assert.Equal("true", hidden.GetAttribute("aria-hidden"));
        Assert.Null(hidden.GetAttribute("role"));
        Assert.Null(hidden.GetAttribute("aria-label"));
    }

    // A5 (R5): Alle acht Namen des Anfangsbestands ergeben je ein SVG.
    [Theory]
    [MemberData(nameof(InitialNames))]
    public void Every_initial_icon_renders_an_svg(string name)
    {
        var cut = Render<TablerIcon>(p => p.Add(c => c.Name, name));

        Assert.Single(cut.FindAll("svg"));
        Assert.Contains("<path", cut.Find("svg").InnerHtml);
    }

    [Fact]
    public void The_list_holds_the_initial_set()
    {
        Assert.Equal(InitialSet.Order().ToArray(), TablerIcons.Names.Order().ToArray());
    }

    public static TheoryData<string> InitialNames() => new(InitialSet);

    // Risiko: Ein unbekannter Name fällt auf, statt still nichts zu zeigen.
    [Fact]
    public void Unknown_name_is_reported_in_development_builds()
    {
#if DEBUG
        Assert.ThrowsAny<Exception>(() => Render<TablerIcon>(p => p.Add(c => c.Name, "no-such-icon")));
#else
        var cut = Render<TablerIcon>(p => p.Add(c => c.Name, "no-such-icon"));
        Assert.Empty(cut.FindAll("svg"));
#endif
    }
}
