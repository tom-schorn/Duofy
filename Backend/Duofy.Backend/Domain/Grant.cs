namespace Duofy.Domain;

/// <summary>
/// Eine Freigabe (Spec 68): Eine Person erlaubt einem anderen Mitglied ihres Haushalts in einem Bereich
/// eine Stufe, zum Beispiel "Anna erlaubt Ben in den Buchungen: Anlegen".
///
/// Gesetzt wird sie nur von der Person, der die Daten gehören (<see cref="GranterId"/>). Es gibt keine
/// Spalte für den Haushalt: Eine Freigabe zählt nur, solange beide im selben Haushalt sind, und Austreten
/// oder Entfernen löscht alle Freigaben von und an die Person. Keine Zeile heißt <see cref="AccessLevel.None"/>;
/// Nichts setzen löscht die Zeile.
/// </summary>
public class Grant
{
    /// <summary>Die Person, der die Daten gehören; nur sie setzt die Freigabe.</summary>
    public Guid GranterId { get; set; }

    /// <summary>Das Mitglied, das die Freigabe bekommt; nie dieselbe Person wie <see cref="GranterId"/>.</summary>
    public Guid GranteeId { get; set; }

    /// <summary>Der Bereich, für den die Freigabe gilt.</summary>
    public Area Area { get; set; }

    /// <summary>Die Stufe; gespeichert wird nie <see cref="AccessLevel.None"/>, dann gibt es keine Zeile.</summary>
    public AccessLevel Level { get; set; }

    /// <summary>
    /// Die Stufen aller vier Bereiche aus den Zeilen zwischen zwei Personen; ein Bereich ohne Zeile ist
    /// <see cref="AccessLevel.None"/>.
    /// </summary>
    public static Dictionary<Area, AccessLevel> LevelsOf(IEnumerable<Grant> rows)
    {
        var levels = Enum.GetValues<Area>().ToDictionary(area => area, _ => AccessLevel.None);
        foreach (var row in rows)
        {
            levels[row.Area] = row.Level;
        }
        return levels;
    }
}

/// <summary>Die Bereiche, für die es Freigaben gibt. Die Bereiche hängen nicht voneinander ab.</summary>
public enum Area
{
    /// <summary>Der Monatsplan mit seinen Posten.</summary>
    Plan,

    /// <summary>Die Buchungen ("Buch"), auch das Abhaken und Zuordnen zu Posten.</summary>
    Book,

    /// <summary>Die Konten.</summary>
    Accounts,

    /// <summary>Die Verträge.</summary>
    Commitments,
}

/// <summary>
/// Was ein Mitglied mit den Daten eines Bereichs tun darf. Die Stufen werden nur nach ihrem Rang
/// verglichen; jede schließt die davor ein.
/// </summary>
public enum AccessLevel
{
    /// <summary>Nichts: Die Daten bleiben unsichtbar.</summary>
    None = 0,

    /// <summary>Ansehen.</summary>
    View = 1,

    /// <summary>Ansehen und Anlegen.</summary>
    Create = 2,

    /// <summary>Ansehen, Anlegen und Ändern.</summary>
    Edit = 3,

    /// <summary>Alles, auch Löschen.</summary>
    Delete = 4,
}

/// <summary>Vergleiche der Stufen.</summary>
public static class AccessLevels
{
    /// <summary>True, wenn <paramref name="granted"/> mindestens so hoch ist wie <paramref name="needed"/>.</summary>
    public static bool Includes(this AccessLevel granted, AccessLevel needed) => granted >= needed;
}
