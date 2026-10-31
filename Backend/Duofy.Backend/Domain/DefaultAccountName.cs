using System.Globalization;
using System.Resources;

namespace Duofy.Domain;

/// <summary>
/// Der Name des ersten Kontos, das eine neue Person bei der Registrierung bekommt (Spec 13).
///
/// Die Sprache bestimmt den Namen: Deutsch "Standardkonto", Englisch "Default account". Fehlt die Sprache oder
/// ist sie nicht unterstützt, gilt Deutsch, weil Deutsch die Standardsprache ist (R4). Die Texte stehen in
/// <c>Resources/DefaultAccountNames.resx</c> (Deutsch) und <c>DefaultAccountNames.en.resx</c> (Englisch).
/// </summary>
public static class DefaultAccountName
{
    private const string Key = "default_account";

    private static readonly ResourceManager Names =
        new("Duofy.Domain.Resources.DefaultAccountNames", typeof(DefaultAccountName).Assembly);

    /// <summary>
    /// Liefert den Namen für die Sprache, zum Beispiel "de", "en" oder "en-GB". Groß- und Kleinschreibung
    /// und die Region zählen nicht; alles andere ergibt den deutschen Namen.
    /// </summary>
    public static string For(string? language)
    {
        var culture = Supported(language);
        return Names.GetString(Key, culture)
            ?? throw new KeyNotFoundException($"Kein Name für das erste Konto in der Sprache '{culture.Name}'.");
    }

    private static CultureInfo Supported(string? language)
    {
        // Die Eingabe kommt von außen: nur die Sprache vor dem ersten Bindestrich zählt, und nur "en" und "de" gelten.
        var primary = (language ?? "").Trim().Split('-', '_')[0].ToLowerInvariant();
        return primary == "en" ? CultureInfo.GetCultureInfo("en") : CultureInfo.GetCultureInfo("de");
    }
}
