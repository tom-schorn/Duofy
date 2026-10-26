namespace Duofy.Schemas;

/// <summary>
/// Eine Kategorie in der Antwort der API (GET /api/v1/categories).
/// Eingebaute und eigene Kategorien sehen gleich aus; <paramref name="IsBuiltIn"/> unterscheidet sie.
/// </summary>
/// <param name="Id">Die Kennung der Kategorie.</param>
/// <param name="Key">Der feste Schlüssel einer eingebauten Kategorie, zum Beispiel "household.groceries"; bei eigenen null.</param>
/// <param name="Name">Der deutsche Name, bei eigenen Kategorien der selbst gewählte.</param>
/// <param name="Group">Der Schlüssel der Gruppe, siehe <see cref="CategoryGroupKeys"/>.</param>
/// <param name="SuggestedPot">Der vorgeschlagene Topf, siehe <see cref="PotKeys"/>.</param>
/// <param name="IsBuiltIn">True bei eingebauten Kategorien; nur eigene lassen sich ändern und löschen.</param>
public sealed record CategoryResponse(Guid Id, string? Key, string Name, string Group, string SuggestedPot, bool IsBuiltIn);

/// <summary>Eine neue oder geänderte eigene Kategorie (POST und PUT /api/v1/categories).</summary>
public sealed record SaveCategoryRequest(string? Name, string? Group, string? Pot);

/// <summary>Die Schlüssel der Töpfe, so wie sie in der API und in der Datenbank stehen.</summary>
public static class PotKeys
{
    public const string Income = "income";
    public const string Needs = "needs";
    public const string Wants = "wants";
    public const string Savings = "savings";

    /// <summary>Alle Töpfe in der Reihenfolge, in der die Oberfläche sie zeigt.</summary>
    public static readonly IReadOnlyList<string> All = [Income, Needs, Wants, Savings];
}

/// <summary>Die Schlüssel der Kategoriegruppen in der Reihenfolge, in der die Oberfläche sie zeigt.</summary>
public static class CategoryGroupKeys
{
    public const string Income = "income";

    public static readonly IReadOnlyList<string> All =
        ["household", "housing", "transport", "children", "leisure", "personal", Income, "finance"];
}

/// <summary>Die Regeln für eigene Kategorien.</summary>
public static class CategoryRules
{
    /// <summary>Die größte erlaubte Länge des Namens einer eigenen Kategorie.</summary>
    public const int NameMaxLength = 60;
}
