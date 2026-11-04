namespace Duofy.Schemas;

/// <summary>Die Bereiche der Freigaben (Spec 68), so wie die API sie schickt.</summary>
public static class GrantAreas
{
    public const string Plan = "plan";
    public const string Book = "book";
    public const string Accounts = "accounts";
    public const string Commitments = "commitments";
    public const string Savings = "savings";

    /// <summary>
    /// Erinnerungen an fällige Posten (Spec 84); nur auf der gehosteten Version. Gehört nicht zu <see cref="All"/>:
    /// Der Bereich gibt keinen Zugriff auf Daten, die Oberfläche zeigt ihn gesondert.
    /// </summary>
    public const string Reminders = "reminders";

    /// <summary>Die fünf Bereiche mit Zugriff auf Daten in der Reihenfolge, in der die Oberfläche sie zeigt.</summary>
    public static readonly IReadOnlyList<string> All = [Plan, Book, Accounts, Commitments, Savings];
}

/// <summary>Die Stufen der Freigaben, von Nichts bis Löschen; jede schließt die davor ein.</summary>
public static class AccessLevelKeys
{
    public const string None = "none";
    public const string View = "view";
    public const string Create = "create";
    public const string Edit = "edit";
    public const string Delete = "delete";

    /// <summary>Alle fünf nach ihrem Rang, die niedrigste zuerst.</summary>
    public static readonly IReadOnlyList<string> All = [None, View, Create, Edit, Delete];

    /// <summary>Der Rang einer Stufe (0 bis 4); -1 für einen unbekannten Schlüssel.</summary>
    public static int Rank(string? level) => level is null ? -1 : All.ToList().IndexOf(level);

    /// <summary>True, wenn <paramref name="granted"/> mindestens <paramref name="needed"/> ist.</summary>
    public static bool Includes(string? granted, string needed) => Rank(granted) >= Rank(needed);
}

/// <summary>Die Stufen einer Person für die fünf Bereiche; jeder Wert ist einer aus <see cref="AccessLevelKeys"/>.</summary>
public sealed record GrantLevels(string Plan, string Book, string Accounts, string Commitments, string Savings = AccessLevelKeys.None, string Reminders = AccessLevelKeys.None)
{
    /// <summary>Nichts in allen Bereichen, so wie eine Person ohne Freigabe dasteht.</summary>
    public static GrantLevels None { get; } = new(AccessLevelKeys.None, AccessLevelKeys.None, AccessLevelKeys.None, AccessLevelKeys.None, AccessLevelKeys.None, AccessLevelKeys.None);

    /// <summary>Die Stufe eines Bereichs nach seinem Schlüssel aus <see cref="GrantAreas"/>.</summary>
    public string For(string area) => area switch
    {
        GrantAreas.Plan => Plan,
        GrantAreas.Book => Book,
        GrantAreas.Accounts => Accounts,
        GrantAreas.Commitments => Commitments,
        GrantAreas.Savings => Savings,
        GrantAreas.Reminders => Reminders,
        _ => throw new ArgumentOutOfRangeException(nameof(area), area, "Unbekannter Bereich."),
    };

    /// <summary>Dieselben Stufen, nur ein Bereich geändert.</summary>
    public GrantLevels With(string area, string level) => area switch
    {
        GrantAreas.Plan => this with { Plan = level },
        GrantAreas.Book => this with { Book = level },
        GrantAreas.Accounts => this with { Accounts = level },
        GrantAreas.Commitments => this with { Commitments = level },
        GrantAreas.Savings => this with { Savings = level },
        GrantAreas.Reminders => this with { Reminders = level },
        _ => throw new ArgumentOutOfRangeException(nameof(area), area, "Unbekannter Bereich."),
    };

    /// <summary>True, wenn mindestens ein Bereich Ansehen oder mehr hat.</summary>
    public bool SharesAnything => GrantAreas.All.Any(area => AccessLevelKeys.Includes(For(area), AccessLevelKeys.View));
}

/// <summary>
/// Setzt, was die angemeldete Person einem Mitglied erlaubt (PUT /api/v1/households/{id}/grants/{granteeId}).
/// Nur die geschickten Bereiche ändern sich; "none" nimmt die Freigabe zurück.
/// </summary>
public sealed record UpdateGrantsRequest(string? Plan = null, string? Book = null, string? Accounts = null, string? Commitments = null, string? Savings = null, string? Reminders = null);
