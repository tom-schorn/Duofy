using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Radzen;

namespace Microsoft.Extensions.DependencyInjection;

public static class DuofyUiExtensions
{
    /// <summary>
    /// Meldet alles an, was die gemeinsame Oberfläche braucht: Radzen, die Texte aus
    /// der Ressourcendatei und die Ablage für den hellen/dunklen Modus. Web und App rufen
    /// diese eine Zeile auf. Die App ersetzt danach <see cref="IThemePreferenceStore"/>.
    /// Den Anmeldezustand für die Seiten bringt AddDuofyApiClient mit.
    /// </summary>
    public static IServiceCollection AddDuofyUi(this IServiceCollection services)
    {
        services.AddRadzenComponents();
        services.AddLocalization();
        services.AddScoped<IThemePreferenceStore, LocalStorageThemePreferenceStore>();
        services.AddScoped<IConsentStore, LocalStorageConsentStore>();

        // Die Buchungsseite braucht die heutige Uhrzeit (Tests geben eine feste vor) und die Konten
        // aus dem Konten-Client. Beides wird nur angemeldet, wenn der Host nicht schon etwas anderes angemeldet hat.
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IAccountChoices, ApiAccountChoices>();

        // Das App-Layout: Titel und Plus-Knopf der Seite, die Entscheidung über Vollbild-Formulare
        // und der gemeinsame Aufruf des Buchungsdialogs. Im Browser ist IAppHost "kein App"; die App
        // meldet in MauiProgram danach AppHost an, das gewinnt.
        services.TryAddSingleton<IAppHost, BrowserHost>();

        // Name und Logo: ohne Angabe des Hosts Duofy. Der Web-Host meldet seine Marke vor dieser Zeile an (Duofy:AppName).
        services.TryAddSingleton(AppBrand.Duofy);
        services.AddScoped<AppChrome>();
        services.AddScoped<FormDialogs>();
        services.AddScoped<BookingDialogLauncher>();

        // Wessen Zahlen die Seiten zeigen (Spec 68): eine Wahl je Sitzung, beim Abmelden wieder "Ich".
        services.AddScoped<ActiveMember>();
        return services;
    }
}
