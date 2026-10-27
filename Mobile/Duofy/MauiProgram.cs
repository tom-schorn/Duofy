using System.Reflection;
using Duofy.ApiClient;
using Duofy.Services;
using Duofy.Shared.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Duofy
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            AddAppSettings(builder.Configuration);

            // Add device-specific services used by the Duofy.Shared project
            builder.Services.AddSingleton<IFormFactor, FormFactor>();

            builder.Services.AddMauiBlazorWebView();

            // MudBlazor, Texte und die Ablage für hell/dunkel aus Duofy.Shared. Die App
            // ersetzt danach die Ablage durch eine, die die Preferences des Telefons benutzt.
            builder.Services.AddDuofyUi();
            builder.Services.AddScoped<IThemePreferenceStore, PreferencesThemePreferenceStore>();

            // Nur die App meldet das an: Die Seiten zeigen dann das App-Layout mit Vollbild-Formularen.
            builder.Services.AddSingleton<IAppHost, AppHost>();

            // Teilen öffnet in der App den Teilen-Dialog von Android, zum Beispiel für eine Einladung.
            builder.Services.AddSingleton<IShareService, MauiShareService>();

            // Die Verbindung zur API. Die Adresse steht in appsettings.json (Release: der gehostete
            // Dienst Plagevo, https://api.plagevo.de/) oder appsettings.Debug.json (Debug: die API auf dem PC).
            var apiAddress = builder.Configuration["Api:BaseAddress"]
                ?? throw new InvalidOperationException("Api:BaseAddress fehlt in appsettings.json.");
            var hostedAddress = new Uri(apiAddress);
            builder.Services.AddDuofyApiClient(hostedAddress);

            // Die Person kann auf der Anmeldeseite einen eigenen Server wählen. Die Wahl steht in den
            // Preferences und gilt beim nächsten Start; ohne Wahl gilt der gehostete Dienst von oben.
            // http ist nur in Debug-Builds erlaubt, damit man gegen einen PC im Heimnetz testen kann.
            var allowHttp = false;
#if DEBUG
            allowHttp = true;
#endif
            builder.Services.AddDuofyServerChoice(
                new ServerStore(hostedAddress, new PreferencesServerPreference()),
                new ServerSelectorOptions { AllowHttp = allowHttp });

            // Die Tokens liegen im SecureStorage des Telefons. Dadurch bleibt die App nach
            // dem Schließen angemeldet.
            builder.Services.AddSingleton<ITokenStore, SecureStorageTokenStore>();

            // Die Seiten fragen über diesen Dienst, wer angemeldet ist.
            builder.Services.AddCascadingAuthenticationState();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }

        // Liest die eingebetteten Einstellungsdateien. Spätere Dateien überschreiben frühere:
        // appsettings.json, dann appsettings.Debug.json, dann appsettings.local.json.
        private static void AddAppSettings(IConfigurationBuilder configuration)
        {
            var assembly = Assembly.GetExecutingAssembly();
            foreach (var name in new[] { "appsettings.json", "appsettings.Debug.json", "appsettings.local.json" })
            {
                var resource = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(name, StringComparison.Ordinal));
                if (resource is null) continue;

                using var stream = assembly.GetManifestResourceStream(resource)!;
                configuration.AddJsonStream(stream);
            }
        }
    }
}
