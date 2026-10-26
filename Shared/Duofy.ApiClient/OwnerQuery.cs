namespace Duofy.ApiClient;

/// <summary>
/// Hängt den Besitzer an eine Adresse (Spec 68): <c>owner=&lt;id&gt;</c>, wenn die Oberfläche die Zahlen eines
/// anderen Mitglieds zeigt. Ohne Besitzer bleibt die Adresse, wie sie ist; dann gilt die angemeldete Person.
/// </summary>
internal static class OwnerQuery
{
    public static string With(string path, Guid? owner) =>
        owner is { } id ? $"{path}{(path.Contains('?') ? '&' : '?')}owner={id}" : path;
}
