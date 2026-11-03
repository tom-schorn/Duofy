using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components;

namespace Duofy.Shared.Layout;

/// <summary>
/// Der gemeinsame Teil aller Layouts: Er holt die gemerkte Wahl der Person (Hell, Dunkel oder
/// "wie das Gerät"), leitet daraus den dunklen Modus ab und speichert eine neue Wahl.
/// Bei "wie das Gerät" folgt die Anzeige der Einstellung des Geräts, auch wenn sie sich ändert.
/// Den Modus setzt <see cref="ThemeVariables"/> als Klasse an html.
/// </summary>
public abstract class ThemedLayoutBase : LayoutComponentBase
{
    [Inject] private IThemePreferenceStore ThemeStore { get; set; } = default!;

    protected bool IsDarkMode { get; set; }

    /// <summary>Die aktive Wahl; ohne gemerkte Wahl gilt das Gerät.</summary>
    protected ThemeChoice Choice { get; private set; } = ThemeChoice.Device;

    private bool systemIsDark;
    private bool choiceLoaded;
    private bool deviceAnswered;

    /// <summary>True, sobald feststeht, was gilt; vorher bleibt die Farbe des frühen Skripts im Kopf der Seite (ThemeBoot).</summary>
    protected bool ThemeSettled => choiceLoaded && (Choice != ThemeChoice.Device || deviceAnswered);

    // Kennt der Speicher die Wahl schon jetzt (in der App: Preferences), gilt sie vor dem ersten Zeichnen und nicht erst danach.
    protected override void OnInitialized()
    {
        if (ThemeStore.LoadImmediately() is not { } start) return;

        Choice = start.Choice;
        systemIsDark = start.SystemIsDark;
        deviceAnswered = true;
        choiceLoaded = true;
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
    }

    /// <summary>Die Antwort des Geräts aus <see cref="ThemeVariables"/>: beim Start und bei jeder Änderung.</summary>
    protected Task OnDeviceDarkChangedAsync(bool isDark)
    {
        systemIsDark = isDark;
        deviceAnswered = true;
        ApplyChoice();
        return Task.CompletedTask;
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
    }
}
