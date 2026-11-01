using Microsoft.JSInterop;

namespace Duofy.Shared.Services;

/// <summary>
/// Die Standard-Ablage für den Browser: Die Wahl liegt im localStorage ("granted" oder "denied").
/// Jeder andere Wert und jeder fehlende Eintrag heißt "noch nicht gewählt".
/// </summary>
public sealed class LocalStorageConsentStore(IJSRuntime js) : IConsentStore
{
    private static string KeyOf(ConsentCategory category) => $"duofy-consent-{category.ToString().ToLowerInvariant()}";

    public async Task<bool?> LoadAsync(ConsentCategory category)
    {
        try
        {
            var value = await js.InvokeAsync<string?>("localStorage.getItem", KeyOf(category));
            return value switch
            {
                "granted" => true,
                "denied" => false,
                _ => null,
            };
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
            // Ohne Browser (Vorrendern) oder bei verweigertem Speicher gibt es keine Wahl, also keine Zustimmung.
            return null;
        }
    }

    public async Task SaveAsync(ConsentCategory category, bool granted)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", KeyOf(category), granted ? "granted" : "denied");
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
            // Der Browser darf den Speicher verweigern (zum Beispiel im privaten Fenster).
            // Die Wahl gilt dann nur, solange die Seite offen ist.
        }
    }
}
