using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components;
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

    /// <summary>Wird im Layout per @ref an den MudThemeProvider gehängt.</summary>
    protected MudThemeProvider? ThemeProvider { get; set; }

    protected bool IsDarkMode { get; set; }

    /// <summary>Die aktive Wahl; ohne gemerkte Wahl gilt das Gerät.</summary>
    protected ThemeChoice Choice { get; private set; } = ThemeChoice.Device;

    private bool systemIsDark;

    // Erst nach dem ersten Zeichnen gibt es einen Browser, den man fragen kann.
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || ThemeProvider is null) return;

        Choice = await ThemeStore.LoadAsync();
        systemIsDark = await ThemeProvider.GetSystemDarkModeAsync();
        ApplyChoice();
        StateHasChanged();

        // Der Provider selbst beobachtet das Gerät nicht (ObserveSystemDarkModeChange="false" im Layout),
        // sonst würde ein Wechsel am Gerät auch eine gewählte Hell- oder Dunkel-Wahl überschreiben.
        await ThemeProvider.WatchSystemDarkModeAsync(OnSystemDarkModeChangedAsync);
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
