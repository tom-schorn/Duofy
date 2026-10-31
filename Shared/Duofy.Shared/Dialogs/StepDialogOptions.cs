using MudBlazor;

namespace Duofy.Shared.Dialogs;

/// <summary>
/// Wie ein Schritt-Dialog aufgeht (Spec 11). Die Größe kommt aus app.css (.step-dialog): bis 599 px Vollbild,
/// darüber höchstens 640 px breit. Schließen geht nur über Abbrechen des Dialogs, damit die Rückfrage
/// bei Eingaben nicht umgangen wird; deshalb kein Klick daneben, kein Escape und kein Kreuz von MudBlazor.
/// </summary>
public static class StepDialogOptions
{
    public static DialogOptions Create() => new()
    {
        BackdropClick = false,
        CloseOnEscapeKey = false,
        CloseButton = false,
    };
}
