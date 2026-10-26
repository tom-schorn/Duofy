namespace Duofy.Shared.Layout;

/// <summary>Die vier Ziele der unteren Leiste der App.</summary>
public enum AppDestination
{
    Start,
    Plan,
    Bookings,
    More,
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
        (AppDestination.More, "/mehr", "Nav_More", MudBlazor.Icons.Material.Filled.MoreHoriz),
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
            "mehr" or "konten" or "vertraege" or "konto" or "haushalt" or "admin" => AppDestination.More,
            _ => null,
        };
    }
}
