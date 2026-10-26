using Microsoft.JSInterop;

namespace Duofy.Shared.Services;

/// <summary>
/// Die Standard-Ablage für den Browser: Die Wahl liegt im localStorage des Browsers.
/// Die App ersetzt diese Klasse durch eine eigene, die Preferences benutzt.
/// </summary>
public sealed class LocalStorageThemePreferenceStore(IJSRuntime js) : IThemePreferenceStore
{
    private const string Key = "duofy-theme-dark";

    public async Task<bool?> LoadIsDarkAsync()
    {
        try
        {
            var value = await js.InvokeAsync<string?>("localStorage.getItem", Key);
            return value is null ? null : value == "true";
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
            // Beim Vorrendern auf dem Server gibt es noch keinen Browser. Dann gilt "keine Wahl".
            return null;
        }
    }

    public async Task SaveIsDarkAsync(bool isDark)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", Key, isDark ? "true" : "false");
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
            // Der Browser darf den Speicher verweigern (zum Beispiel im privaten Fenster).
            // Die Wahl gilt dann nur, solange die Seite offen ist.
        }
    }
}
