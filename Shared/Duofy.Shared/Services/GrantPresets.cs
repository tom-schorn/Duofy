using Duofy.Schemas;

namespace Duofy.Shared.Services;

/// <summary>
/// Die Vorlagen und Hinweise der Freigaben-Seite (Spec 68, R6, R7).
///
/// Eine Vorlage ist nur eine Abkürzung: Sie setzt ihre vier Stufen, und wer danach einen Bereich ändert, hat eine
/// eigene Auswahl. Ein Hinweis ändert nie etwas von selbst; eine Seite, die still eine zweite Stufe anhebt, gäbe
/// mehr frei, als gewählt wurde.
/// </summary>
public static class GrantPresets
{
    public const string Partner = "partner";
    public const string Read = "read";
    public const string Kid = "kid";
    public const string Nothing = "none";

    /// <summary>Die Vorlagen in der Reihenfolge auf der Seite.</summary>
    public static readonly IReadOnlyList<string> Order = [Partner, Read, Kid, Nothing];

    /// <summary>Die vier Stufen einer Vorlage.</summary>
    public static GrantLevels Levels(string preset) => preset switch
    {
        Partner => new(AccessLevelKeys.Delete, AccessLevelKeys.Delete, AccessLevelKeys.Delete, AccessLevelKeys.Delete),
        Read => new(AccessLevelKeys.View, AccessLevelKeys.View, AccessLevelKeys.View, AccessLevelKeys.View),
        Kid => GrantLevels.None with { Plan = AccessLevelKeys.View },
        Nothing => GrantLevels.None,
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unbekannte Vorlage."),
    };

    /// <summary>Die Vorlage, die genau diesen Stufen entspricht; null für eine eigene Auswahl.</summary>
    public static string? PresetOf(GrantLevels levels) => Order.FirstOrDefault(preset => Levels(preset) == levels);

    /// <summary>Die Schlüssel der Hinweise, wenn ein Recht ohne ein anderes wenig nützt.</summary>
    public static IReadOnlyList<string> Hints(GrantLevels levels)
    {
        var hints = new List<string>();
        if (AccessLevelKeys.Includes(levels.Book, AccessLevelKeys.Create) && !AccessLevelKeys.Includes(levels.Accounts, AccessLevelKeys.View))
        {
            hints.Add("Grants_HintBookNeedsAccounts");
        }
        if (AccessLevelKeys.Includes(levels.Book, AccessLevelKeys.View) && !AccessLevelKeys.Includes(levels.Plan, AccessLevelKeys.View))
        {
            hints.Add("Grants_HintBookNeedsPlan");
        }
        return hints;
    }
}
