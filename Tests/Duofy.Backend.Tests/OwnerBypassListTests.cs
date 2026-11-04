using System.Text.RegularExpressions;

namespace Duofy.Api.Tests;

/// <summary>
/// Der Besitzer-Filter hat keine versteckten Löcher (Spec 56, R4): Jede Stelle im Produktcode, die ihn ausschaltet,
/// steht in der genehmigten Liste. Eine neue Stelle lässt diesen Test scheitern, bis sie genehmigt und hier
/// eingetragen ist. Die Tests selbst dürfen am Filter vorbei in die Datenbank schauen und zählen nicht mit.
/// </summary>
public partial class OwnerBypassListTests
{
    /// <summary>Die genehmigten Stellen: Datei und Grund.</summary>
    private static readonly (string File, string Reason)[] Approved =
    [
        ("Backend/Duofy.Backend/Api/Program.cs", "Migrationen beim Start"),
        ("Backend/Duofy.Backend/Api/UserEndpoints.cs", "Konto löschen"),
        ("Backend/Duofy.Backend/Api/PlanStore.cs", "Mitgliedermonat anlegen"),
        ("Backend/Duofy.Backend/Api/HouseholdPlanStore.cs", "Haushaltsplan zusammensetzen"),
        ("Backend/Duofy.Backend/Api/HouseholdPlanStore.cs", "Haushaltsplan zusammensetzen"),
        ("Backend/Duofy.Backend/Api/AuthEndpoints.cs", "Erstes Konto bei der Registrierung"),
        ("Backend/Duofy.Backend/Api/ReminderRunner.cs", "Erinnerungen: Job ohne angemeldete Person"),
        ("Backend/Duofy.Backend/Api/ReminderEndpoints.cs", "Erinnerung lesen"),
        ("Backend/Duofy.Backend/Api/ReminderEndpoints.cs", "Erinnerung lesen"),
        // Ebenfalls genehmigt, aber noch nicht gebaut; der Eintrag kommt mit dem Code:
        // Prüfungen beim Start.
    ];

    [GeneratedRegex(@"\.Bypass\(")]
    private static partial Regex AnyCall();

    [GeneratedRegex(@"\.Bypass\(""(?<reason>[^""]+)""\)")]
    private static partial Regex CallWithReason();

    // A5 (R4)
    [Fact]
    public void Every_bypass_in_the_product_code_is_on_the_approved_list()
    {
        var found = new List<(string File, string Reason)>();
        var root = SolutionRoot();
        foreach (var file in ProductSources(root))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            foreach (var line in File.ReadLines(file).Where(l => !l.TrimStart().StartsWith("//")))
            {
                var calls = AnyCall().Matches(line).Count;
                var named = CallWithReason().Matches(line).Select(m => (relative, m.Groups["reason"].Value)).ToList();
                // Der Grund muss als Text in der Zeile stehen, sonst lässt sich die Stelle nicht prüfen.
                Assert.True(calls == named.Count, $"{relative}: Bypass ohne festen Grund in \"{line.Trim()}\"");
                found.AddRange(named);
            }
        }

        Assert.Equal(Approved.Order(), found.Order());
    }

    /// <summary>Alle C#-Dateien der Produktprojekte, ohne Tests, Build-Ausgaben und versteckte Ordner wie .worktrees.</summary>
    internal static IEnumerable<string> ProductSources(string root) =>
        new[] { "Backend", "Shared", "Web", "Mobile" }
            .Select(area => Path.Combine(root, area))
            .Where(Directory.Exists)
            .SelectMany(area => Directory.EnumerateFiles(area, "*.cs", SearchOption.AllDirectories))
            .Where(file => !Path.GetRelativePath(root, file).Split(Path.DirectorySeparatorChar)
                .Any(part => part is "bin" or "obj" || part.StartsWith('.')));

    internal static string SolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Duofy.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Duofy.slnx nicht gefunden.");
    }
}
