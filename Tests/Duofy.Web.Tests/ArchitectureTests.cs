using System.Xml.Linq;

namespace Duofy.Web.Tests;

/// <summary>
/// Nur die API kennt die Datenbank (R1, A1). Web, gemeinsame Oberfläche und App
/// dürfen weder direkt noch über ein anderes Projekt an Duofy.Backend oder an Entity
/// Framework Core kommen.
/// </summary>
public class ArchitectureTests
{
    // A1
    [Theory]
    [InlineData("Web/Duofy.Web/Duofy.Web.csproj")]
    [InlineData("Shared/Duofy.Shared/Duofy.Shared.csproj")]
    [InlineData("Mobile/Duofy/Duofy.csproj")]
    public void Web_shared_ui_and_app_do_not_reference_the_database(string project)
    {
        var references = ProjectReferences.Collect(project);

        Assert.DoesNotContain("Duofy.Backend", references.Projects);
        Assert.DoesNotContain(references.Packages, ProjectReferences.IsDatabasePackage);
    }

    [Fact]
    public void The_check_finds_the_database_in_the_api()
    {
        // Gegenprobe: Die API darf und muss die Datenbank kennen. Fände die Prüfung
        // sie dort nicht, wäre der Test oben wertlos.
        var references = ProjectReferences.Collect("Backend/Duofy.Backend/Duofy.Backend.csproj");

        Assert.Contains(references.Packages, ProjectReferences.IsDatabasePackage);
    }
}

/// <summary>Liest Projekt- und Paketverweise aus den .csproj-Dateien, auch über mehrere Stufen.</summary>
internal static class ProjectReferences
{
    public static bool IsDatabasePackage(string package) =>
        package.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase)
        || package.StartsWith("EFCore.", StringComparison.OrdinalIgnoreCase)
        || package.StartsWith("Npgsql", StringComparison.OrdinalIgnoreCase);

    public static (HashSet<string> Projects, HashSet<string> Packages) Collect(string project)
    {
        var projects = new HashSet<string>();
        var packages = new HashSet<string>();
        Visit(Path.Combine(SolutionRoot(), project), projects, packages);
        return (projects, packages);
    }

    private static void Visit(string projectFile, HashSet<string> projects, HashSet<string> packages)
    {
        var document = XDocument.Load(projectFile);
        foreach (var package in document.Descendants("PackageReference"))
        {
            packages.Add((string)package.Attribute("Include")!);
        }
        foreach (var reference in document.Descendants("ProjectReference"))
        {
            var relative = ((string)reference.Attribute("Include")!).Replace('\\', Path.DirectorySeparatorChar);
            var file = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectFile)!, relative));
            if (projects.Add(Path.GetFileNameWithoutExtension(file)))
            {
                Visit(file, projects, packages);
            }
        }
    }

    private static string SolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Duofy.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Duofy.slnx nicht gefunden.");
    }
}
