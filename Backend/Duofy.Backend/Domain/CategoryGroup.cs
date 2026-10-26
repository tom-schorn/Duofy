namespace Duofy.Domain;

/// <summary>
/// Die Überschrift, unter der eine Kategorie steht, zum Beispiel Wohnen oder Kinder.
/// Die Gruppen sind fest vorgegeben; auch eigene Kategorien gehören zu einer davon.
/// </summary>
public enum CategoryGroup
{
    Household,
    Housing,
    Transport,
    Children,
    Leisure,
    Personal,
    Income,
    Finance,
}

/// <summary>
/// Übersetzt eine <see cref="CategoryGroup"/> in ihren Schlüssel (klein
/// geschrieben, zum Beispiel "household") und zurück. Der Schlüssel steht in der
/// Datenbank und in der API.
/// </summary>
public static class CategoryGroups
{
    public static string ToKey(CategoryGroup group) => group.ToString().ToLowerInvariant();

    /// <summary>
    /// Liest einen Schlüssel. Genau die klein geschriebenen Schlüssel gelten, "Household" nicht;
    /// so gibt es für jede Gruppe nur eine Schreibweise.
    /// </summary>
    public static bool TryParse(string? key, out CategoryGroup group)
    {
        foreach (var candidate in Enum.GetValues<CategoryGroup>())
        {
            if (ToKey(candidate) == key)
            {
                group = candidate;
                return true;
            }
        }
        group = default;
        return false;
    }
}
