using Duofy.Shared.Services;
using Duofy.Shared.Theme;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace Duofy.Shared.Layout;

/// <summary>
/// Der gemeinsame Teil aller Layouts: Er holt die gemerkte Wahl der Person (Hell, Dunkel oder
/// "wie das Gerät"), leitet daraus den dunklen Modus ab und speichert eine neue Wahl.
/// Bei "wie das Gerät" folgt die Anzeige der Einstellung des Geräts, auch wenn sie sich ändert.
/// </summary>
public abstract class ThemedLayoutBase : LayoutComponentBase
{
    [Inject] private IThemePreferenceStore ThemeStore { get; set; } = default!;

    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>Wird im Layout per @ref an den MudThemeProvider gehängt.</summary>
    protected MudThemeProvider? ThemeProvider { get; set; }

    protected bool IsDarkMode { get; set; }

    /// <summary>Die aktive Wahl; ohne gemerkte Wahl gilt das Gerät.</summary>
    protected ThemeChoice Choice { get; private set; } = ThemeChoice.Device;

    private bool systemIsDark;

    // Issue 5: Kennt der Speicher die Wahl schon jetzt (in der App: Preferences), gilt sie vor dem ersten Zeichnen und
    // nicht erst danach. Im Browser gibt es null; dort setzt das Skript im Kopf der Seite (ThemeBoot) die Farbe früh.
    protected override void OnInitialized()
    {
        if (ThemeStore.LoadImmediately() is not { } start) return;

        Choice = start.Choice;
        systemIsDark = start.SystemIsDark;
        ApplyChoice();
    }

    // Erst nach dem ersten Zeichnen gibt es einen Browser, den man fragen kann.
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || ThemeProvider is null) return;

        Choice = await ThemeStore.LoadAsync();
        systemIsDark = await ThemeProvider.GetSystemDarkModeAsync();
        ApplyChoice();
        StateHasChanged();
        await DropBootClassAsync();

        // Der Provider selbst beobachtet das Gerät nicht (ObserveSystemDarkModeChange="false" im Layout),
        // sonst würde ein Wechsel am Gerät auch eine gewählte Hell- oder Dunkel-Wahl überschreiben.
        await ThemeProvider.WatchSystemDarkModeAsync(OnSystemDarkModeChangedAsync);
    }

    // Ab jetzt zeichnet das Layout selbst in der richtigen Farbe; die frühe Klasse der Seite würde eine spätere Wahl
    // (etwa von Dunkel auf Hell) überstimmen und muss weg. In der App und beim Vorrendern gibt es sie nicht.
    private async Task DropBootClassAsync()
    {
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

    private Task OnSystemDarkModeChangedAsync(bool isDark)
    {
        systemIsDark = isDark;

        // Auch bei Hell oder Dunkel neu setzen: Der Provider übernimmt den Wert des Geräts selbst in IsDarkMode
        // (über @bind), und das muss die Wahl der Person wieder zurücknehmen.
        ApplyChoice();
        return InvokeAsync(StateHasChanged);
    }

    private void ApplyChoice() => IsDarkMode = Choice switch
    {
        ThemeChoice.Dark => true,
        ThemeChoice.Light => false,
        _ => systemIsDark,
    };
}
