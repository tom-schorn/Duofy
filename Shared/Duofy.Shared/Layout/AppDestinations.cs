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
    /// <summary>Die Ziele in der Reihenfolge der Leiste: Ziel, Adresse, Textschlüssel, Symbol.</summary>
    public static IReadOnlyList<(AppDestination Destination, string Href, string LabelKey, string Icon)> All { get; } =
    [
        (AppDestination.Start, "/", "Nav_Start", MudBlazor.Icons.Material.Filled.Home),
        (AppDestination.Plan, "/plan", "Nav_Plan", MudBlazor.Icons.Material.Filled.CalendarMonth),
        (AppDestination.Bookings, "/buchungen", "Nav_Bookings", MudBlazor.Icons.Material.Filled.Receipt),
        (AppDestination.Savings, "/sparen", "Nav_Savings", MudBlazor.Icons.Material.Filled.Savings),
        (AppDestination.More, "/mehr", "Nav_More", MudBlazor.Icons.Material.Filled.MoreHoriz),
    ];

    /// <summary>
    /// Die festen Einträge der Seitenleiste (Spec 10, Änderung vom 01.11.2026): Was unten hinter "Mehr" liegt, steht
    /// hier direkt; "Mehr" selbst nicht. Admin, Rechtstexte und Abmelden hängen vom Server ab und stehen in SideAccountEntries.
    /// </summary>
    public static IReadOnlyList<(AppDestination Destination, string Href, string LabelKey, string Icon)> Side { get; } =
    [
        (AppDestination.Start, "/", "Nav_Start", MudBlazor.Icons.Material.Filled.Home),
        (AppDestination.Plan, "/plan", "Nav_Plan", MudBlazor.Icons.Material.Filled.CalendarMonth),
        (AppDestination.Bookings, "/buchungen", "Nav_Bookings", MudBlazor.Icons.Material.Filled.Receipt),
        (AppDestination.Savings, "/sparen", "Nav_Savings", MudBlazor.Icons.Material.Filled.Savings),
        (AppDestination.Accounts, "/konten", "Nav_Accounts", MudBlazor.Icons.Material.Filled.AccountBalance),
        (AppDestination.Contracts, "/vertraege", "More_Contracts", MudBlazor.Icons.Material.Filled.Description),
        (AppDestination.Household, "/haushalt", "More_Household", MudBlazor.Icons.Material.Filled.Groups),
        (AppDestination.UserAccount, "/konto", "More_UserAccount", MudBlazor.Icons.Material.Filled.Person),
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
