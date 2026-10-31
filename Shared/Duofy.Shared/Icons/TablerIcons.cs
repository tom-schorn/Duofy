using System.Text.RegularExpressions;

namespace Duofy.Shared.Icons;

/// <summary>
/// Der feste Bestand an Tabler-Symbolen (Stil outline, @tabler/icons 3.50.0, MIT). Die SVG-Dateien
/// liegen in Icons/svg und werden als eingebettete Ressourcen mit dem Assembly ausgeliefert.
/// </summary>
public static partial class TablerIcons
{
    /// <summary>Die Namen aller ausgelieferten Symbole.</summary>
    public static IReadOnlyList<string> Names { get; } =
    [
        "cash",
        "building-bank",
        "credit-card",
        "pig-money",
        "wallet",
        "repeat",
        "arrows-exchange",
        "coins",
    ];

    /// <summary>Der innere Teil des SVG (die Pfade) zum Namen, oder null bei unbekanntem Namen.</summary>
    public static string? Body(string name)
    {
        if (!Names.Contains(name)) return null;

        var assembly = typeof(TablerIcons).Assembly;
        var resource = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith($".svg.{name}.svg", StringComparison.Ordinal));
        if (resource is null) return null;

        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        var svg = reader.ReadToEnd();

        // Rahmen des Pakets weg, die Pfade bleiben; das leere Hintergrundrechteck braucht niemand.
        var inner = OuterSvg().Replace(svg, "$1");
        return BoundingBox().Replace(inner, "").Trim();
    }

    [GeneratedRegex(@"^\s*<svg[^>]*>(.*)</svg>\s*$", RegexOptions.Singleline)]
    private static partial Regex OuterSvg();

    [GeneratedRegex(@"<path\s+stroke=""none""[^>]*/>")]
    private static partial Regex BoundingBox();
}
