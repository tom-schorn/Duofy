using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Duofy.Shared.Dialogs;

/// <summary>
/// Der gemeinsame Teil aller Dialoge, die <see cref="Services.FormDialogs"/> öffnet: Sie schließen sich selbst, mit oder
/// ohne Ergebnis. Blueprint reicht dafür jedem Dialog seinen Griff (<see cref="IDialogReference"/>) als Cascading-Wert.
/// Ohne Griff (der Baustein wird im Test allein gezeichnet) passiert beim Schließen nichts.
/// </summary>
public abstract class FormDialogBase : ComponentBase
{
    [CascadingParameter] public IDialogReference? DialogRef { get; set; }

    [Inject] private IServiceProvider Services { get; set; } = default!;

    // Meldet den Griff bei FormDialogs an, damit der Dialog zugeht, wenn die Person die Seite verlässt. Ohne FormDialogs (Test) bleibt es aus.
    public override Task SetParametersAsync(ParameterView parameters)
    {
        var set = base.SetParametersAsync(parameters);
        if (DialogRef is not null && Services.GetService(typeof(Services.FormDialogs)) is Services.FormDialogs forms)
        {
            forms.Track(DialogRef);
        }
        return set;
    }

    /// <summary>Schließt mit Ergebnis; der Aufrufer bekommt es als <c>DialogOutcome</c>.</summary>
    protected Task CloseAsync(object result) => DialogRef?.CloseAsync(DialogResult.Ok(result)) ?? Task.CompletedTask;

    /// <summary>Schließt ohne Ergebnis: "nichts geändert".</summary>
    protected Task CancelAsync() => DialogRef?.CancelAsync() ?? Task.CompletedTask;
}
