using Duofy.Shared.Services;
using Duofy.Shared.Theme;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace Duofy.Shared.Layout;

/// <summary>
/// Der gemeinsame Teil aller Layouts: Er holt die gemerkte Wahl der Person (Hell, Dunkel oder
/// "wie das Gerät"), leitet daraus den dunklen Modus ab und speichert eine neue Wahl.
/// Bei "wie das Gerät" folgt die Anzeige der Einstellung des Geräts, auch wenn sie sich ändert.
/// Der Modus schaltet die Theme-Datei von Radzen über den ThemeService und die Variablen über
/// <see cref="ThemeVariables"/>.
/// </summary>
public abstract class ThemedLayoutBase : LayoutComponentBase
{
    [Inject] private IThemePreferenceStore ThemeStore { get; set; } = default!;

    [Inject] private IJSRuntime Js { get; set; } = default!;

    [Inject] private ThemeService RadzenTheme { get; set; } = default!;

    protected bool IsDarkMode { get; set; }

    /// <summary>Die aktive Wahl; ohne gemerkte Wahl gilt das Gerät.</summary>
    protected ThemeChoice Choice { get; private set; } = ThemeChoice.Device;

    private bool systemIsDark;
    private bool choiceLoaded;
    private bool deviceAnswered;
    private bool bootClassDropped;

    // Issue 5: Kennt der Speicher die Wahl schon jetzt (in der App: Preferences), gilt sie vor dem ersten Zeichnen und
    // nicht erst danach. Im Browser gibt es null; dort setzt das Skript im Kopf der Seite (ThemeBoot) die Farbe früh.
    protected override void OnInitialized()
    {
        if (ThemeStore.LoadImmediately() is not { } start) return;

        Choice = start.Choice;
        systemIsDark = start.SystemIsDark;
        deviceAnswered = true;
        ApplyChoice();
    }

    // Erst nach dem ersten Zeichnen gibt es einen Browser, den man fragen kann.
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        Choice = await ThemeStore.LoadAsync();
        choiceLoaded = true;
        ApplyChoice();
        StateHasChanged();
        await DropBootClassWhenSettledAsync();
    }

    /// <summary>Die Antwort des Geräts aus <see cref="ThemeVariables"/>: beim Start und bei jeder Änderung.</summary>
    protected async Task OnDeviceDarkChangedAsync(bool isDark)
    {
        systemIsDark = isDark;
        deviceAnswered = true;
        ApplyChoice();
        await DropBootClassWhenSettledAsync();
    }

    // Ab jetzt zeichnet das Layout selbst in der richtigen Farbe; die frühe Klasse der Seite würde eine spätere Wahl
    // (etwa von Dunkel auf Hell) überstimmen und muss weg. Bei "wie das Gerät" erst, wenn das Gerät geantwortet hat,
    // sonst blitzt es hell auf. In der App und beim Vorrendern gibt es die Klasse nicht.
    private async Task DropBootClassWhenSettledAsync()
    {
        if (bootClassDropped || !choiceLoaded || (Choice == ThemeChoice.Device && !deviceAnswered)) return;

        bootClassDropped = true;
        try
        {
            await Js.InvokeVoidAsync(ThemeBoot.RemoveFunction, ThemeBoot.DarkClass);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
            // Ohne Browser (Test, Vorrendern) ist nichts zu entfernen.
        }
    }

    protected async Task SelectChoiceAsync(ThemeChoice choice)
    {
        Choice = choice;
        ApplyChoice();
        await ThemeStore.SaveAsync(choice);
    }

    private void ApplyChoice()
    {
        IsDarkMode = Choice switch
        {
            ThemeChoice.Dark => true,
            ThemeChoice.Light => false,
            _ => systemIsDark,
        };

        // Lädt nur dann eine andere Datei, wenn sich der Modus wirklich geändert hat.
        RadzenTheme.SetTheme(IsDarkMode ? RadzenVariables.DarkTheme : RadzenVariables.LightTheme, true);
    }
}
