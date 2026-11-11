using System.Text;
using System.Xml.Linq;

namespace Duofy.Web.Tests;

/// <summary>
/// Die Ordnerstruktur des Repositorys (Spec 77): elf Projekte in den Ordnern Backend, Shared, Web,
/// Mobile und Tests, und der alte Name der Datenformate kommt nirgends mehr vor.
/// </summary>
public class ProjectStructureTests
{
    private static readonly string[] ExpectedProjects =
    [
        "Backend/Duofy.Backend/Duofy.Backend.csproj",
        "Backend/Duofy.Schemas/Duofy.Schemas.csproj",
        "Shared/Duofy.ApiClient/Duofy.ApiClient.csproj",
        "Shared/Duofy.Shared/Duofy.Shared.csproj",
        "Web/Duofy.Web/Duofy.Web.csproj",
        "Mobile/Duofy/Duofy.csproj",
        "Tests/Duofy.ApiClient.Tests/Duofy.ApiClient.Tests.csproj",
        "Tests/Duofy.App.Tests/Duofy.App.Tests.csproj",
        "Tests/Duofy.Backend.Tests/Duofy.Backend.Tests.csproj",
        "Tests/Duofy.Shared.Tests/Duofy.Shared.Tests.csproj",
        "Tests/Duofy.Web.Tests/Duofy.Web.Tests.csproj",
    ];

    // A1 (R1, R3)
    [Fact]
    public void The_solution_lists_exactly_the_eleven_projects_in_their_five_folders()
    {
        var solution = XDocument.Load(Path.Combine(SolutionRoot(), "Duofy.slnx")).Root!;

        var projects = solution.Descendants("Project").Select(p => (string)p.Attribute("Path")!).Order().ToList();
        Assert.Equal(ExpectedProjects.Order(), projects);

        // Jedes Projekt steht in dem Lösungsordner, der seinem Ordner im Repository entspricht.
        foreach (var folder in solution.Elements("Folder"))
        {
            var name = ((string)folder.Attribute("Name")!).Trim('/');
            Assert.All(folder.Elements("Project"), p => Assert.StartsWith(name + "/", (string)p.Attribute("Path")!));
        }
        Assert.Equal(["Backend", "Mobile", "Shared", "Tests", "Web"],
            solution.Elements("Folder").Select(f => ((string)f.Attribute("Name")!).Trim('/')).Order());
        Assert.All(ExpectedProjects, project => Assert.True(File.Exists(Path.Combine(SolutionRoot(), project)), project));
    }

    // A2 (R2)
    [Fact]
    public void No_file_outside_git_mentions_the_old_name_of_the_schemas()
    {
        // Der alte Name wird zusammengesetzt, damit dieser Test sich nicht selbst findet.
        var oldName = Encoding.UTF8.GetBytes("Duofy." + "Contracts");
        var root = SolutionRoot();
        var found = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            var parts = relative.Split('/');
            // Die Ablage von Git, fremde Worktrees und Build-Ausgaben zählen nicht; die Spec 77 beschreibt die Umbenennung.
            // Die Aufzeichnungen des Teams (.workday) erzählen die Geschichte und gehören nicht ins öffentliche Repository.
            // Der Cache von Visual Studio (.vs) entsteht lokal und liegt nicht in Git.
            if (parts.Any(part => part is ".git" or ".worktrees" or ".workday" or ".vs" or "bin" or "obj" or "node_modules")
                || relative.StartsWith("docs/specs/77-project-structure/", StringComparison.Ordinal))
            {
                continue;
            }
            if (File.ReadAllBytes(file).AsSpan().IndexOf(oldName) >= 0)
            {
                found.Add(relative);
            }
        }

        Assert.True(found.Count == 0, "Der alte Name steht noch in: " + string.Join(", ", found));
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
