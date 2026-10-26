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
        Assert.All(references, r => Assert.True(r.Version == "10.0.12", $"{r.Project}: {r.Package} {r.Version}"));
    }
}
