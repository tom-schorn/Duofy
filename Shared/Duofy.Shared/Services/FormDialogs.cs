using MudBlazor;

namespace Duofy.Shared.Services;

/// <summary>
/// Der eine Ort, der entscheidet, wie Formular-Dialoge aufgehen: im Browser als Fenster in der
/// Mitte, in der App über den ganzen Bildschirm. In der App zeichnet der Dialog seine Kopfzeile
/// mit Schließen und Speichern selbst, deshalb ist die Standard-Kopfzeile von MudBlazor dort aus.
/// </summary>
public sealed class FormDialogs(IAppHost host)
{
    public DialogOptions Options() => host.IsApp
        ? new DialogOptions { FullScreen = true, NoHeader = true, CloseButton = false }
        : new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true };
}
