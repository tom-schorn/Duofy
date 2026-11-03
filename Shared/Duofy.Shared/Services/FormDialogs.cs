using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Duofy.Shared.Services;

/// <summary>
/// Der eine Ort, der entscheidet, wie Dialoge aufgehen, gebaut auf dem DialogService von Blueprint. Seiten rufen
/// <see cref="OpenAsync{TDialog,TResult}"/> und <see cref="ConfirmAsync"/> und kennen die Optionen der Bibliothek nicht.
/// Am Handy füllt ein Dialog die Breite (duofy.css), im Browser wie in der App: Es ist dieselbe responsive Oberfläche.
///
/// Den Fokus führt dieser Dienst selbst (dialog-focus.js): beim Öffnen ins erste Feld, bei einer Rückfrage auf den sicheren
/// Knopf, und nach dem Schließen zurück zum Auslöser.
/// </summary>
public sealed class FormDialogs(DialogService dialogs, IJSRuntime js, ILogger<FormDialogs> log)
{
    private const string FocusModule = "./_content/Duofy.Shared/dialog-focus.js";

    /// <summary>Ein Fenster mit Titel und X. Esc schließt. Der Dialog schließt sich mit CloseAsync(ergebnis); ohne Ergebnis heißt es "nichts geändert".</summary>
    public Task<DialogOutcome<TResult>> OpenAsync<TDialog, TResult>(string title, FormParameters<TDialog>? parameters = null)
        where TDialog : ComponentBase =>
        OpenWithAsync<TDialog, TResult>(parameters, new DialogOpenOptions { Title = title, ShowClose = true });

    /// <summary>
    /// Für Dialoge in Schritten (StepDialog): Der Dialog zeichnet Titel und Abbrechen selbst. Schließen geht nur über sein
    /// Abbrechen, damit die Rückfrage bei Eingaben nicht umgangen wird.
    /// </summary>
    public Task<DialogOutcome<TResult>> OpenStepAsync<TDialog, TResult>(string title, FormParameters<TDialog>? parameters = null)
        where TDialog : ComponentBase =>
        OpenWithAsync<TDialog, TResult>(parameters, new DialogOpenOptions { Title = title, ShowClose = false, PreventClose = true });

    private async Task<DialogOutcome<TResult>> OpenWithAsync<TDialog, TResult>(FormParameters<TDialog>? parameters, DialogOpenOptions options)
        where TDialog : ComponentBase
    {
        await CallFocusAsync("rememberTrigger");
        var closed = dialogs.OpenAsync<TDialog>((parameters ?? new FormParameters<TDialog>()).ToDictionary(), options);
        _ = CallFocusAsync("focusFirstField");
        var result = await closed;
        await CallFocusAsync("restoreTrigger");
        return !result.Cancelled && result.GetData<object>() is TResult value
            ? new DialogOutcome<TResult>(true, value)
            : DialogOutcome<TResult>.Unchanged;
    }

    /// <summary>
    /// Fragt nach, bevor etwas geschieht, das sich nicht zurücknehmen lässt. Die Texte kommen vom Aufrufer aus den Ressourcen.
    /// Der Fokus beginnt auf dem sicheren Knopf (Nein), nicht auf Ja: Enter oder Leertaste bestätigen sonst gleich beim Öffnen.
    /// </summary>
    /// <returns>true bei Ja, false bei Nein oder Schließen.</returns>
    public async Task<bool> ConfirmAsync(string message, string title, string yesText, string noText)
    {
        await CallFocusAsync("rememberTrigger");
        var closed = dialogs.ConfirmAsync(title, message, new ConfirmDialogOptions { ConfirmText = yesText, CancelText = noText });
        _ = CallFocusAsync("focusSafeButton");
        var result = await closed;
        await CallFocusAsync("restoreTrigger");
        return result.Confirmed;
    }

    // Der Fokus ist eine Hilfe; scheitert der Aufruf, geht der Dialog trotzdem auf. Der Grund steht im Protokoll.
    private async Task CallFocusAsync(string function)
    {
        try
        {
            await using var module = await js.InvokeAsync<IJSObjectReference>("import", FocusModule);
            if (module is not null)
            {
                await module.InvokeVoidAsync(function);
            }
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException or InvalidOperationException)
        {
            log.LogWarning(exception, "Der Fokus des Dialogs ließ sich nicht setzen ({Function}).", function);
        }
    }
}
