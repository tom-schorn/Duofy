namespace Duofy.Shared.Services;

/// <summary>
/// Wie sich die Anwendung nennt und zeigt (Issue 8): Eine selbst betriebene Instanz heißt "Duofy" und zeigt nur den
/// Namen, weil es für Duofy kein Logo gibt; die gehostete Instanz heißt "Plagevo" und zeigt das Logo "Offenes P" mit der
/// Wortmarke. Die Wortmarke und das Symbol gibt es für hellen und dunklen Grund; die Dateien liegen unter wwwroot/brand.
/// Der Host meldet die Marke an (Web aus Duofy:AppName und Duofy:Hosted, siehe <see cref="FromSettings"/>); ohne Anmeldung
/// gilt <see cref="Duofy"/>, so wie in der App.
/// </summary>
/// <param name="Name">Der Name in Kopfzeile, Anmeldeseite und Browser-Titel.</param>
/// <param name="WordmarkLight">Logo samt Namen für hellen Grund; null, wenn es kein Logo gibt.</param>
/// <param name="WordmarkDark">Dasselbe für dunklen Grund, ohne eigenen Hintergrund.</param>
/// <param name="SymbolLight">Nur das Zeichen für hellen Grund, für schmale Leisten.</param>
/// <param name="SymbolDark">Nur das Zeichen für dunklen Grund.</param>
/// <param name="FaviconLight">Das Symbol im Browser-Tab; Adresse relativ zur Basisadresse der Seite.</param>
/// <param name="FaviconDark">Das Symbol für ein dunkles Fenster des Browsers; null, wenn eins für beide gilt.</param>
/// <param name="FaviconType">Der MIME-Typ der Favicon-Dateien.</param>
public sealed record AppBrand(
    string Name,
    string? WordmarkLight = null,
    string? WordmarkDark = null,
    string? SymbolLight = null,
    string? SymbolDark = null,
    string FaviconLight = "_content/Duofy.Shared/favicon.png",
    string? FaviconDark = null,
    string FaviconType = "image/png")
{
    private const string Assets = "_content/Duofy.Shared/brand/";

    /// <summary>True, wenn die Marke ein Logo hat; sonst steht nur der Name da.</summary>
    public bool HasLogo => WordmarkLight is not null;

    /// <summary>Die selbst betriebene Instanz: Name ohne Logo, das Symbol im Tab ist das bisherige.</summary>
    public static AppBrand Duofy { get; } = new("Duofy");

    /// <summary>Die gehostete Instanz: das Logo "Offenes P" von Plagevo.</summary>
    public static AppBrand Plagevo { get; } = new(
        "Plagevo",
        WordmarkLight: Assets + "plagevo-wortmarke-hell.svg",
        WordmarkDark: Assets + "plagevo-wortmarke-dunkel.svg",
        SymbolLight: Assets + "plagevo-symbol-hell.svg",
        SymbolDark: Assets + "plagevo-symbol-dunkel.svg",
        FaviconLight: Assets + "plagevo-favicon-hell.svg",
        FaviconDark: Assets + "plagevo-favicon-dunkel.svg",
        FaviconType: "image/svg+xml");

    /// <summary>
    /// Die Marke der App nach dem gewählten Server: der gehostete Dienst heißt Plagevo und zeigt das Logo, jeder eigene
    /// Server Duofy ohne Logo. Die App fragt einmal beim Start; die Wahl eines anderen Servers gilt für die Marke beim
    /// nächsten Start.
    /// </summary>
    public static AppBrand ForServer(global::Duofy.ApiClient.IServerStore servers)
    {
        ArgumentNullException.ThrowIfNull(servers);
        return servers.IsHosted ? Plagevo : Duofy;
    }

    /// <summary>
    /// Wählt die Marke aus den Einstellungen des Hosts. Ein ausdrücklich gesetzter Name gewinnt: "Plagevo" und "Duofy"
    /// (egal wie geschrieben) wählen ihre Marke, jeder andere Name gilt ohne Logo. Ohne Namen gilt die gehostete Instanz
    /// (<c>Duofy:Hosted</c>, derselbe Wert wie bei der API) als Plagevo, jede andere als Duofy.
    /// </summary>
    public static AppBrand FromSettings(string? name, bool hosted)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return hosted ? Plagevo : Duofy;
        }
        if (string.Equals(trimmed, Plagevo.Name, StringComparison.OrdinalIgnoreCase))
        {
            return Plagevo;
        }
        return string.Equals(trimmed, Duofy.Name, StringComparison.OrdinalIgnoreCase) ? Duofy : new AppBrand(trimmed);
    }
}
