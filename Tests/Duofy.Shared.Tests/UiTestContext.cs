using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Gemeinsamer Unterbau der Komponententests: bUnit mit Radzen, den Texten
/// und einem Anmeldezustand.
/// </summary>
public abstract class UiTestContext : BunitContext
{
    protected UiTestContext()
    {
        // Radzen ruft viel JavaScript auf; im Test genügt es, wenn die Aufrufe ins Leere gehen.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddDuofyUi();
    }

    /// <summary>
    /// Stellt die Hülle bereit, in der Radzen-Dialoge aufgehen (im echten Layout steht RadzenComponents in jedem Layout),
    /// und gibt sie zurück: Dialoginhalt steht dort, nicht in der Seite.
    /// </summary>
    protected IRenderedComponent<IComponent> RenderRadzenHost() => Render<Radzen.Blazor.RadzenComponents>();

    /// <summary>Ob die Seite im dunklen Modus gerendert wird (die Variablen des Themes wissen es).</summary>
    protected static bool IsDarkMode(IRenderedComponent<IComponent> cut) =>
        cut.FindComponent<Duofy.Shared.Layout.ThemeVariables>().Instance.Dark;

    /// <summary>Der Dialog selbst (nicht sein Inhalt), zum Beispiel <c>DialogSelector + ".step-dialog"</c>.</summary>
    protected const string DialogSelector = "[role='dialog']";

    /// <summary>Eine Meldung oben im Seiteninhalt (Erfolg, Hinweis, Fehler).</summary>
    protected const string AlertSelector = ".rz-alert";
}
