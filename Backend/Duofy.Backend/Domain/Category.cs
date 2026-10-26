namespace Duofy.Domain;

/// <summary>
/// Wofür das Geld war, genauer als der Topf: zum Beispiel Lebensmittel oder
/// Miete. Eine Kategorie ist entweder eingebaut oder gehört einer Person.
///
/// - Eingebaut (<see cref="OwnerId"/> ist null): stammt aus dem <see cref="CategoryCatalog"/>,
///   gilt für alle gleich und hat einen festen <see cref="Key"/>. Der deutsche Name
///   steht in einer Textdatei, nicht in der Datenbank.
/// - Eigene (<see cref="OwnerId"/> ist gesetzt): hat einen <see cref="Name"/> und
///   ist nur für diese Person sichtbar.
/// </summary>
public class Category
{
    /// <summary>Die Kennung. Bei eingebauten Kategorien fest, siehe <see cref="CategoryCatalog.IdFor"/>.</summary>
    public Guid Id { get; set; }

    /// <summary>Die Person, der die Kategorie gehört; null bei eingebauten Kategorien.</summary>
    public Guid? OwnerId { get; set; }

    /// <summary>Der feste Schlüssel einer eingebauten Kategorie, zum Beispiel "household.groceries"; sonst null.</summary>
    public string? Key { get; set; }

    /// <summary>Die Gruppe, unter der die Kategorie steht.</summary>
    public required CategoryGroup Group { get; set; }

    /// <summary>Der Name einer eigenen Kategorie, 1 bis 60 Zeichen; bei eingebauten null.</summary>
    public string? Name { get; set; }

    /// <summary>Der Topf, der beim Wählen der Kategorie vorgewählt wird. Pro Buchung änderbar.</summary>
    public required Pot SuggestedPot { get; set; }

    /// <summary>True, wenn die Kategorie eingebaut ist und niemandem gehört.</summary>
    public bool IsBuiltIn => OwnerId is null;
}
