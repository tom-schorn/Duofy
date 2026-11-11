namespace Duofy.Shared.Layout;

/// <summary>Die Ziele der Navigation: fünf in der unteren Leiste (Tom, 03.11.2026), dazu die, die nur die Seitenleiste direkt zeigt.</summary>
public enum AppDestination
{
    Start,
    Plan,
    Savings,
    More,
    Accounts,
    Contracts,
    Household,
}

/// <summary>
/// Welche Adresse zu welchem Ziel der unteren Leiste gehört. Konten, Verträge, Benutzerkonto und Admin liegen unter "Mehr"
/// und lassen deshalb "Mehr" leuchten. "Haushalt" meint den ganzen Bereich des Haushalts: Haushaltsplan, Mitglieder,
/// Freigaben und Richtwerte (Spec 82, R11); ohne Haushalt führt er zu /haushalt, deshalb leuchtet er auch dort.
/// </summary>
public static class AppDestinations
{
    /// <summary>Die Ziele in der Reihenfolge der unteren Leiste: Ziel, Adresse, Textschlüssel, Name des Symbols (Lucide).</summary>
    public static IReadOnlyList<(AppDestination Destination, string Href, string LabelKey, string Icon)> All { get; } =
    [
        (AppDestination.Start, "/", "Nav_Start", "layout-dashboard"),
        (AppDestination.Plan, "/plan", "Nav_Plan", "calendar-days"),
        (AppDestination.Household, "/haushaltsplan", "More_Household", "house"),
        (AppDestination.Savings, "/sparen", "Nav_Savings", "piggy-bank"),
        (AppDestination.More, "/mehr", "Nav_More", "ellipsis"),
    ];

    /// <summary>
    /// Die festen Einträge der Seitenleiste am PC und am Tablet (Spec 82, R11): Was unten hinter "Mehr" liegt, steht hier
    /// direkt; "Mehr" selbst nicht. Der Haushalt trägt in der Leiste seinen Namen. Benutzerkonto, Admin, Rechtstexte,
    /// die Auswahl der Person und Abmelden stehen im Profil-Menü unten (UserMenu).
    /// </summary>
    public static IReadOnlyList<(AppDestination Destination, string Href, string LabelKey, string Icon)> Side { get; } =
    [
        (AppDestination.Start, "/", "Nav_Start", "layout-dashboard"),
        (AppDestination.Plan, "/plan", "Nav_MyPlan", "calendar-days"),
        (AppDestination.Household, "/haushaltsplan", "Nav_OurHousehold", "house"),
        (AppDestination.Contracts, "/vertraege", "More_Contracts", "file-text"),
        (AppDestination.Accounts, "/konten", "Nav_Accounts", "wallet"),
        (AppDestination.Savings, "/sparen", "Nav_Savings", "piggy-bank"),
    ];

    /// <summary>Einträge, deren Text von der Person abhängt: Schlüssel für die eigene Ansicht, Schlüssel mit Vornamen für die einer anderen (Spec 126).</summary>
    private static readonly Dictionary<string, string> OfPerson = new() { ["Nav_MyPlan"] = "Nav_PlanOf" };

    /// <param name="labelKey">Der Textschlüssel des Eintrags.</param>
    /// <param name="firstName">Der Vorname der gewählten Person; null in der eigenen Ansicht.</param>
    /// <returns>Der Schlüssel, der gilt: "Meine ..." wird in der Ansicht einer anderen Person zu "... von Vorname"; der Text bekommt den Vornamen als {0}.</returns>
    public static string LabelKeyFor(string labelKey, string? firstName) =>
        firstName is not null && OfPerson.TryGetValue(labelKey, out var of) ? of : labelKey;

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
            "haushaltsplan" or "haushalt" => AppDestination.Household,
            "sparen" => AppDestination.Savings,
            "mehr" or "konten" or "vertraege" or "konto" or "admin" => AppDestination.More,
            _ => null,
        };
    }
}
