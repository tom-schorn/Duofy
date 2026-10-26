using Duofy.Schemas;

namespace Duofy.Shared.Services;

/// <summary>
/// Die drei Rechtstexte in der App (Spec 70, R7 und R8): zu jedem Namen der API gehören die Adresse der Seite
/// (<c>/rechtliches/impressum</c> und so weiter) und der Textschlüssel der Beschriftung ("Impressum",
/// "Datenschutz", "Nutzungsbedingungen"). Die Seite fragt die API nur nach einem Namen, den diese Liste
/// kennt; was in der Adresse sonst steht, erreicht den Server nie.
/// </summary>
public static class LegalPages
{
    /// <summary>Name der API, Adresse der Seite, Textschlüssel der Beschriftung, in der Reihenfolge der Links.</summary>
    public static IReadOnlyList<(string Name, string Slug, string LabelKey)> All { get; } =
    [
        (LegalDocumentNames.Imprint, "impressum", "Legal_Imprint"),
        (LegalDocumentNames.Privacy, "datenschutz", "Legal_Privacy"),
        (LegalDocumentNames.Terms, "nutzungsbedingungen", "Legal_Terms"),
    ];

    /// <summary>Die Adresse der Seite zu einem Namen der API.</summary>
    public static string PathOf(string name) => "/rechtliches/" + All.Single(p => p.Name == name).Slug;

    /// <summary>Der Name der API zu einer Adresse der Seite; null für alles, was keine der drei Adressen ist.</summary>
    public static string? NameOf(string? slug) => All.FirstOrDefault(p => p.Slug == slug).Name;

    /// <summary>Der Textschlüssel der Beschriftung zu einem Namen der API.</summary>
    public static string LabelKeyOf(string name) => All.Single(p => p.Name == name).LabelKey;

    /// <summary>Von der API genannte Namen, die die App kennt, in der Reihenfolge der Links.</summary>
    public static IReadOnlyList<string> Known(IEnumerable<string> names) =>
        All.Select(p => p.Name).Where(names.Contains).ToList();
}
