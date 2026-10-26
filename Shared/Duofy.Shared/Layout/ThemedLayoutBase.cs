using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Duofy.Shared.Layout;

/// <summary>
/// Der gemeinsame Teil beider Layouts: Er merkt sich, ob der dunkle Modus an ist, holt die
/// gemerkte Wahl der Person und speichert sie beim Umschalten.
/// </summary>
public abstract class ThemedLayoutBase : LayoutComponentBase
{
    [Inject] private IThemePreferenceStore ThemeStore { get; set; } = default!;

    /// <summary>Wird im Layout per @ref an den MudThemeProvider gehängt.</summary>
    protected MudThemeProvider? ThemeProvider { get; set; }

    protected bool IsDarkMode { get; set; }

    // Erst nach dem ersten Zeichnen gibt es einen Browser, den man fragen kann.
    // Reihenfolge: gemerkte Wahl der Person, sonst die Einstellung des Systems.
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || ThemeProvider is null) return;

        var saved = await ThemeStore.LoadIsDarkAsync();
        IsDarkMode = saved ?? await ThemeProvider.GetSystemDarkModeAsync();
        StateHasChanged();
    }

    protected async Task ToggleModeAsync()
    {
        IsDarkMode = !IsDarkMode;
        await ThemeStore.SaveIsDarkAsync(IsDarkMode);
    }
}
