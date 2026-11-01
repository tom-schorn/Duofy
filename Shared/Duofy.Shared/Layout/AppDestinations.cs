namespace Duofy.Shared.Layout;

/// <summary>Die Ziele der Navigation: fünf in der unteren Leiste, dazu die, die nur die Seitenleiste direkt zeigt.</summary>
public enum AppDestination
{
    Start,
    Plan,
    Bookings,
    Savings,
    More,
    Accounts,
    Contracts,
    Household,
    UserAccount,
}

/// <summary>
/// Welche Adresse zu welchem Ziel der unteren Leiste gehört. Konten, Verträge, Haushalt, Benutzerkonto und Admin
/// liegen unter "Mehr" und lassen deshalb "Mehr" leuchten.
/// </summary>
public static class AppDestinations
{
    /// <summary>Die Ziele in der Reihenfolge der Leiste: Ziel, Adresse, Textschlüssel, Name des Symbols in Material Symbols.</summary>
    public static IReadOnlyList<(AppDestination Destination, string Href, string LabelKey, string Icon)> All { get; } =
    [
        (AppDestination.Start, "/", "Nav_Start", "home"),
        (AppDestination.Plan, "/plan", "Nav_Plan", "calendar_month"),
        (AppDestination.Bookings, "/buchungen", "Nav_Bookings", "receipt"),
        (AppDestination.Savings, "/sparen", "Nav_Savings", "savings"),
        (AppDestination.More, "/mehr", "Nav_More", "more_horiz"),
    ];

    /// <summary>
    /// Die festen Einträge der Seitenleiste (Spec 10, Änderung vom 01.11.2026): Was unten hinter "Mehr" liegt, steht
    /// hier direkt; "Mehr" selbst nicht. Admin, Rechtstexte und Abmelden hängen vom Server ab und stehen in SideAccountEntries.
    /// </summary>
    public static IReadOnlyList<(AppDestination Destination, string Href, string LabelKey, string Icon)> Side { get; } =
    [
        (AppDestination.Start, "/", "Nav_Start", "home"),
        (AppDestination.Plan, "/plan", "Nav_Plan", "calendar_month"),
        (AppDestination.Bookings, "/buchungen", "Nav_Bookings", "receipt"),
        (AppDestination.Savings, "/sparen", "Nav_Savings", "savings"),
        (AppDestination.Accounts, "/konten", "Nav_Accounts", "account_balance"),
        (AppDestination.Contracts, "/vertraege", "More_Contracts", "description"),
        (AppDestination.Household, "/haushalt", "More_Household", "groups"),
        (AppDestination.UserAccount, "/konto", "More_UserAccount", "person"),
    ];

    /// <param name="path">Der Pfad ohne Rechnername, zum Beispiel "/konten" oder "konten".</param>
    /// <returns>Das Ziel, das leuchten soll; null bei Adressen ohne Ziel (zum Beispiel unbekannte Seiten).</returns>
    public static AppDestination? For(string path)
    {
        var clean = path.Split('?', '#')[0].Trim('/');
        var first = clean.Split('/')[0];
        return first switch
        {
            "" => AppDestination.Start,
            "plan" => AppDestination.Plan,
            "buchungen" => AppDestination.Bookings,
            "sparen" => AppDestination.Savings,
            "mehr" or "konten" or "vertraege" or "konto" or "haushalt" or "admin" => AppDestination.More,
            _ => null,
        };
    }
}
