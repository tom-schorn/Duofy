namespace Duofy.Shared.Tests;

/// <summary>
/// Findet das Hauptverzeichnis des Repositorys, damit Tests Projektdateien lesen können.
/// </summary>
internal static class RepositoryFiles
{
    public static string Root { get; } = FindRoot();

    public static string Path(params string[] parts) =>
        System.IO.Path.Combine([Root, .. parts]);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "Duofy.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Duofy.slnx wurde nicht gefunden.");
    }
}
