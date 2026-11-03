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

    /// <summary>Schließt mit Ergebnis; der Aufrufer bekommt es als <c>DialogOutcome</c>.</summary>
    protected Task CloseAsync(object result) => DialogRef?.CloseAsync(DialogResult.Ok(result)) ?? Task.CompletedTask;

    /// <summary>Schließt ohne Ergebnis: "nichts geändert".</summary>
    protected Task CancelAsync() => DialogRef?.CancelAsync() ?? Task.CompletedTask;
}
