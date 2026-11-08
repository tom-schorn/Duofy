namespace Duofy.Shared.Services;

/// <summary>Was die App mit einer Adresse tut, die ihre Ansicht laden will.</summary>
public enum LinkDecision
{
    /// <summary>Die Adresse gehört zur Oberfläche der App selbst; die Ansicht lädt sie.</summary>
    StayInApp,

    /// <summary>Eine andere Seite im Netz (https); der Browser des Telefons öffnet sie.</summary>
    OpenSystemBrowser,

    /// <summary>Nichts davon (http, javascript:, data:, intent:, file: und so weiter); es lädt nirgends.</summary>
    Cancel,
}

/// <summary>
/// Die Entscheidung der App über eine Adresse (Spec 109, A6) als reine Funktion: Adresse hinein, Entscheidung heraus.
/// MainPage ruft sie für jede Adresse auf, die die Ansicht laden will; so ist sie mit echten Adressen prüfbar.
/// </summary>
public static class ExternalLinks
{
    /// <summary>Der Rechnername, unter dem die Ansicht der App ihre Oberfläche auf Android ausliefert (MAUI: https://0.0.0.0/).</summary>
    public const string AppHost = "0.0.0.0";

    /// <summary>
    /// Der Rechnername der Ansicht auf Windows (MAUI HostAddressHelper: https://0.0.0.1/; der Schalter AppHostAddressAlways0000
    /// stellt 0.0.0.0 ein). Beide Namen gehören der App (Issue 111).
    /// </summary>
    public const string AppHostWindows = "0.0.0.1";

    public static LinkDecision Decide(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return LinkDecision.Cancel;
        }

        // Die eigene Oberfläche: https (Android, Windows) oder app (iOS) am Rechnernamen der App, ohne Benutzerteil und ohne eigenen Port. Ein
        // Rechnername, der nur mit "0.0.0.0" oder "0.0.0.1" beginnt, oder "0.0.0.1@fremd.example" ist nicht die App.
        var ownScheme = uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == "app";
        if (ownScheme && IsAppAuthority(url!) && uri.UserInfo.Length == 0 && uri.IsDefaultPort)
        {
            return LinkDecision.StayInApp;
        }

        return LegalPages.IsExternal(url) ? LinkDecision.OpenSystemBrowser : LinkDecision.Cancel;
    }

    // Uri.Host schreibt Zahlen-Aliase um (https://1/ und https://0x0.0.0.1/ werden zu 0.0.0.1). Darum zählt der Text der Adresse
    // selbst: zwischen "://" und dem nächsten / ? # steht genau ein App-Rechnername, ohne Port und ohne Benutzerteil.
    private static bool IsAppAuthority(string url)
    {
        var start = url.IndexOf("://", StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        var rest = url.AsSpan(start + 3);
        var end = rest.IndexOfAny('/', '?', '#');
        var authority = end < 0 ? rest : rest[..end];
        return authority.SequenceEqual(AppHost) || authority.SequenceEqual(AppHostWindows);
    }
}
