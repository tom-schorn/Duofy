using System.Text.RegularExpressions;

namespace Duofy.Api.Tests;

/// <summary>
/// ExecuteUpdate und ExecuteDelete schreiben direkt in die Datenbank, an der Prüfung beim Speichern vorbei
/// (Spec 56, A8). Deshalb steht jede dieser Stellen im Produktcode in einer genehmigten Liste, und jede, die Daten mit
/// Besitzer schreibt, steht hinter der zentralen Prüfung: Vor dem Aufruf fragt dieselbe Methode IAccess nach dem
/// Bereich und der Stufe. Eine neue Stelle lässt diesen Test scheitern, bis sie genehmigt und hier eingetragen ist.
/// </summary>
public partial class OwnerBulkListTests
{
    /// <summary>
    /// Eine genehmigte Stelle: Datei und Methode mit dem Aufruf. <paramref name="Guard"/> ist die Prüfung, die vorher
    /// stehen muss ("Area.Book, AccessLevel.Delete"), null bei Tabellen ohne Besitzer mit dem Grund in
    /// <paramref name="Reason"/>. Steht der Aufruf in einer Hilfsmethode, muss die Prüfung vor jedem Aufruf der
    /// Hilfsmethode in derselben Datei stehen.
    /// </summary>
    private sealed record Site(string File, string Method, string? Guard, string Reason);

    private static readonly Site[] Approved =
    [
        new("Backend/Duofy.Backend/Api/AccountEndpoints.cs", "ClearDefaultAsync", "Area.Accounts, AccessLevel.Edit",
            "Standardkonto wechseln: die alte Markierung fällt vor dem Setzen der neuen"),
        new("Backend/Duofy.Backend/Api/BookingEndpoints.cs", "DeleteAsync", "Area.Book, AccessLevel.Delete", "Buchung löschen"),
        new("Backend/Duofy.Backend/Api/PlanEndpoints.cs", "UntickAsync", "Area.Book, AccessLevel.Delete", "Haken entfernen löscht seine Buchung"),
        new("Backend/Duofy.Backend/Api/PlanEndpoints.cs", "DeleteCarryOverAsync", "Area.Book, AccessLevel.Delete", "Übertrag entfernen löscht sein Paar"),
        new("Backend/Duofy.Backend/Api/AdminEndpoints.cs", "RevokeInvitationAsync", null, "Einladung zur Registrierung: keine Daten mit Besitzer, nur für Administratoren"),
        new("Backend/Duofy.Backend/Api/HouseholdStore.cs", "DropGrantsAsync", null, "Freigaben beim Austreten und Entfernen: keine Daten mit Besitzer"),
    ];

    [GeneratedRegex(@"\.Execute(Update|Delete)(Async)?\(")]
    private static partial Regex BulkCall();

    // Der Kopf einer Methode: Sichtbarkeit, Rückgabetyp, Name, öffnende Klammer.
    [GeneratedRegex(@"^\s*(public|private|internal|protected)\s+(static\s+)?(async\s+)?[\w<>\[\],?\s]+?\s(?<name>\w+)\s*\(")]
    private static partial Regex MethodHead();

    /// <summary>Eine Fundstelle: Datei, Methode, Zeile des Aufrufs und alle Zeilen der Datei.</summary>
    private sealed record Found(string File, string Method, int Line, string[] Lines);

    private static List<Found> FindBulkCalls()
    {
        var root = OwnerBypassListTests.SolutionRoot();
        var found = new List<Found>();
        foreach (var file in OwnerBypassListTests.ProductSources(root))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].TrimStart().StartsWith("//") && BulkCall().IsMatch(lines[i]))
                {
                    found.Add(new Found(Path.GetRelativePath(root, file).Replace('\\', '/'), MethodAt(lines, i), i, lines));
                }
            }
        }
        return found;
    }

    /// <summary>Der Name der Methode, in der die Zeile steht: der nächste Methodenkopf darüber.</summary>
    private static string MethodAt(string[] lines, int line)
    {
        for (var i = line; i >= 0; i--)
        {
            if (MethodHead().Match(lines[i]) is { Success: true } head)
            {
                return head.Groups["name"].Value;
            }
        }
        return "";
    }

    /// <summary>True, wenn zwischen dem Kopf der Methode und der Zeile eine Prüfung über IAccess mit dieser Stufe steht.</summary>
    private static bool GuardedBefore(string[] lines, int line, string guard)
    {
        for (var i = line; i >= 0; i--)
        {
            if (lines[i].Contains("RequireAsync(") && lines[i].Contains(guard))
            {
                return true;
            }
            if (MethodHead().IsMatch(lines[i]))
            {
                return false;
            }
        }
        return false;
    }

    // A8 (R4)
    [Fact]
    public void Every_bulk_update_and_delete_in_the_product_code_is_on_the_approved_list()
    {
        var found = FindBulkCalls().Select(f => (f.File, f.Method)).Order().ToList();

        Assert.Equal(Approved.Select(a => (a.File, a.Method)).Order(), found);
    }

    // A8 (R4)
    [Fact]
    public void Every_approved_bulk_write_on_owned_data_sits_behind_the_central_check()
    {
        foreach (var site in Approved.Where(a => a.Guard is not null))
        {
            var call = Assert.Single(FindBulkCalls(), f => (f.File, f.Method) == (site.File, site.Method));
            var lines = call.Lines;

            // Steht die Prüfung nicht in derselben Methode, ist es eine Hilfsmethode: Dann zählt jeder ihrer Aufrufe.
            var places = GuardedBefore(lines, call.Line, site.Guard!)
                ? [call.Line]
                : Enumerable.Range(0, lines.Length)
                    .Where(i => lines[i].Contains($"{site.Method}(") && !MethodHead().IsMatch(lines[i]))
                    .ToList();

            Assert.NotEmpty(places);
            Assert.All(places, place => Assert.True(GuardedBefore(lines, place, site.Guard!),
                $"{site.File}:{place + 1}: {site.Method} schreibt Daten mit Besitzer ohne vorherige Prüfung {site.Guard}."));
        }
    }
}
