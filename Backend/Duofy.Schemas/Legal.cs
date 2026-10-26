namespace Duofy.Schemas;

/// <summary>Die Namen der Rechtstexte in der Adresse <c>/api/v1/legal/{name}</c> und in <c>legalDocuments</c>.</summary>
public static class LegalDocumentNames
{
    /// <summary>Das Impressum.</summary>
    public const string Imprint = "imprint";

    /// <summary>Die Datenschutzerklärung.</summary>
    public const string Privacy = "privacy";

    /// <summary>Die Nutzungsbedingungen.</summary>
    public const string Terms = "terms";

    /// <summary>Alle Namen in der Reihenfolge, in der die Liste sie nennt.</summary>
    public static IReadOnlyList<string> All { get; } = [Imprint, Privacy, Terms];
}

/// <summary>Der Fehlercode, den nur die Rechtstexte kennen.</summary>
public static class LegalErrorCodes
{
    /// <summary>Den Text gibt es nicht: unbekannter Name oder auf diesem Server nicht eingerichtet (404).</summary>
    public const string NotFound = "legal_not_found";
}

/// <summary>Die Antwort von GET /api/v1/legal: die Namen der Texte, die dieser Server anbietet.</summary>
/// <param name="Documents">Teilmenge von <see cref="LegalDocumentNames.All"/>, nur lesbare, nicht leere Texte.</param>
public sealed record LegalListResponse(IReadOnlyList<string> Documents);

/// <summary>Die Antwort von GET /api/v1/legal/{name}: der Text, reiner Text mit Zeilenumbrüchen.</summary>
public sealed record LegalTextResponse(string Text);
