using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Gemeinsamer Unterbau der Komponententests: bUnit mit Blueprint, den Texten
/// und einem Anmeldezustand.
/// </summary>
public abstract class UiTestContext : BunitContext, IAsyncLifetime
{
    protected UiTestContext()
    {
        // Blueprint ruft viel JavaScript auf; im Test genügt es, wenn die Aufrufe ins Leere gehen.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddDuofyUi();

        // Jedes Layout zeigt die Rechtstexte (Spec 109); ohne eigenen Fake bietet der gespielte Server keine an.
        Services.AddSingleton<Duofy.ApiClient.ILegalApi>(new FakeLegalApi());
    }

    // Blueprint meldet Dienste an, die sich nur asynchron aufräumen lassen.
    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    Task IAsyncLifetime.DisposeAsync() => base.DisposeAsync().AsTask();

    /// <summary>
    /// Stellt die Hülle bereit, in der Dialoge und Menüs aufgehen (im echten Layout steht sie im Rahmen),
    /// und gibt sie zurück: Ihr Inhalt steht dort, nicht in der Seite.
    /// </summary>
    protected IRenderedComponent<IComponent> RenderDialogHost() => Render<TestOverlayHost>();

    /// <summary>Ob die Seite im dunklen Modus gerendert wird (die Variablen des Themes wissen es).</summary>
    protected static bool IsDarkMode(IRenderedComponent<IComponent> cut) =>
        cut.FindComponent<Duofy.Shared.Layout.ThemeVariables>().Instance.Dark;

    /// <summary>Der Dialog selbst (nicht sein Inhalt), zum Beispiel <c>DialogSelector + ".step-dialog"</c>.</summary>
    protected const string DialogSelector = "[role='dialog']";

    /// <summary>Eine Meldung oben im Seiteninhalt (Erfolg, Hinweis, Fehler).</summary>
    protected const string AlertSelector = "[data-testid='notice'] [role='alert'], [data-testid='dialog-error'] [role='alert'], .duofy-alert";
}

/// <summary>Die Hülle der Tests: dieselben Bausteine, die im echten Layout für Dialoge und Menüs stehen.</summary>
internal sealed class TestOverlayHost : ComponentBase
{
    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        builder.OpenComponent<BlazorBlueprint.Components.BbDialogProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<BlazorBlueprint.Primitives.BbPortalHost>(1);
        builder.CloseComponent();
    }
}
