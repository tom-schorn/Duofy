using System.Xml.Linq;

namespace Duofy.App.Tests;

/// <summary>
/// Die Pakete von ASP.NET Core, Microsoft.Extensions und EF Core kommen alle aus
/// demselben .NET-Release. Stehen sie auf verschiedenen Patch-Ständen, fehlen
/// einem Teil der Lösung Sicherheitskorrekturen, die der Rest schon hat.
/// </summary>
public class PackageVersionTests
{
    private static readonly string[] SameReleasePrefixes =
        ["Microsoft.AspNetCore.", "Microsoft.Extensions.", "Microsoft.EntityFrameworkCore"];

    [Fact]
    public void Microsoft_runtime_packages_share_one_patch_level()
    {
        var references = Directory.EnumerateFiles(RepositoryFiles.Root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(path => XDocument.Load(path).Descendants("PackageReference")
                .Select(e => (Project: Path.GetFileName(path), Package: (string?)e.Attribute("Include") ?? "", Version: (string?)e.Attribute("Version") ?? "")))
            .Where(r => SameReleasePrefixes.Any(r.Package.StartsWith))
            // Die MAUI-Pakete folgen der Version der MAUI-Workload, nicht der von .NET.
            .Where(r => !r.Version.StartsWith("$("))
            .ToList();

        Assert.NotEmpty(references);
        // Das Backend steht eigenständig auf seinem Stand; es hebt nur, wenn eine Sicherheitsmeldung dort gilt (10.0.14 gilt nur für die Oberfläche).
        Assert.All(references, r =>
        {
            var expected = r.Project.Contains("Backend", StringComparison.Ordinal) ? "10.0.12" : "10.0.14";
            Assert.True(r.Version == expected, $"{r.Project}: {r.Package} {r.Version}");
        });
    }
}
