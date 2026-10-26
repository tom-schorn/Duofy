using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Gemeinsamer Unterbau der Komponententests: bUnit mit MudBlazor, den Texten
/// und einem Anmeldezustand.
/// </summary>
public abstract class UiTestContext : BunitContext
{
    protected UiTestContext()
    {
        // MudBlazor ruft viel JavaScript auf; im Test genügt es, wenn die Aufrufe ins Leere gehen.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddDuofyUi();
    }

    // MudBlazor hat Dienste, die nur asynchron aufgeräumt werden können. xUnit ruft aber
    // Dispose() auf, deshalb warten wir hier auf DisposeAsync().
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
