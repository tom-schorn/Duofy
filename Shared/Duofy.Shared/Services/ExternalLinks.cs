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
    /// <summary>Der Rechnername, unter dem die Ansicht der App ihre eigene Oberfläche ausliefert (MAUI: https://0.0.0.0/).</summary>
    public const string AppHost = "0.0.0.0";

    public static LinkDecision Decide(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return LinkDecision.Cancel;
        }

        // Die eigene Oberfläche: https (Android, Windows) oder app (iOS) am Rechnernamen der App, ohne Benutzerteil. Ein
        // Rechnername, der nur mit "0.0.0.0" beginnt, oder "0.0.0.0@fremd.example" ist nicht die App.
        var ownScheme = uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == "app";
        if (ownScheme && uri.Host == AppHost && uri.UserInfo.Length == 0)
        {
            return LinkDecision.StayInApp;
        }

        return LegalPages.IsExternal(url) ? LinkDecision.OpenSystemBrowser : LinkDecision.Cancel;
    }
}
