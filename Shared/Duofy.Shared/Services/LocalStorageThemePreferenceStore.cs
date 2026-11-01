using Microsoft.JSInterop;

namespace Duofy.Shared.Services;

/// <summary>
/// Die Standard-Ablage für den Browser: Die Wahl liegt im localStorage des Browsers.
/// Die App ersetzt diese Klasse durch eine eigene, die Preferences benutzt.
/// </summary>
/// <remarks>
/// Der Schlüssel und die Werte "true" (dunkel) und "false" (hell) stammen noch aus der Zeit mit nur
/// zwei Zuständen, damit gespeicherte Wahlen gültig bleiben. "Wie das Gerät" ist kein Eintrag.
/// </remarks>
public sealed class LocalStorageThemePreferenceStore(IJSRuntime js) : IThemePreferenceStore
{
    /// <summary>Der Name des Eintrags; auch das Skript im Kopf der Seite (<see cref="Duofy.Shared.Theme.ThemeBoot"/>) liest ihn.</summary>
    public const string StorageKey = "duofy-theme-dark";

    private const string Key = StorageKey;

    public async Task<ThemeChoice> LoadAsync()
    {
        try
        {
            var value = await js.InvokeAsync<string?>("localStorage.getItem", Key);
            return value switch
            {
                "true" => ThemeChoice.Dark,
                "false" => ThemeChoice.Light,
                _ => ThemeChoice.Device,
            };
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
            // Beim Vorrendern auf dem Server gibt es noch keinen Browser. Dann gilt "wie das Gerät".
            return ThemeChoice.Device;
        }
    }

    public async Task SaveAsync(ThemeChoice choice)
    {
        try
        {
            if (choice == ThemeChoice.Device)
            {
                await js.InvokeVoidAsync("localStorage.removeItem", Key);
            }
            else
            {
                await js.InvokeVoidAsync("localStorage.setItem", Key, choice == ThemeChoice.Dark ? "true" : "false");
            }
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
            // Der Browser darf den Speicher verweigern (zum Beispiel im privaten Fenster).
            // Die Wahl gilt dann nur, solange die Seite offen ist.
        }
    }
}
