using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Radzen;

namespace Duofy.Shared.Services;

/// <summary>
/// Der eine Ort, der entscheidet, wie Formular-Dialoge aufgehen: im Browser als Fenster in der
/// Mitte, in der App über den ganzen Bildschirm. In der App zeichnet der Dialog seine Kopfzeile
/// mit Schließen und Speichern selbst (FullScreenFormHeader), deshalb ist die Titelzeile von Radzen dort aus.
///
/// Seiten rufen <see cref="OpenAsync{TDialog,TResult}"/> und <see cref="ConfirmAsync"/> und kennen die
/// Optionen der Bibliothek nicht.
/// </summary>
public sealed class FormDialogs(IAppHost host, DialogService dialogs, IJSRuntime js, ILogger<FormDialogs> log)
{
    /// <summary>
    /// Die Optionen für Radzen. Den Fokus ins erste Feld setzt <see cref="OpenWithAsync{TDialog,TResult}"/> selbst (Radzen
    /// nähme das erste fokussierbare Element, oft einen Knopf der Kopfzeile); beim Schließen setzt Radzen ihn auf das
    /// Element zurück, das vorher den Fokus hatte. Esc schließt im Browser den Dialog.
    /// </summary>
    public DialogOptions RadzenOptions() => host.IsApp
        ? new DialogOptions
        {
            ShowTitle = false,
            ShowClose = false,
            Width = "100%",
            Height = "100%",
            // Ausnahme von "kein eigenes CSS" (Plan, Frage 1): der Dialog füllt das Fenster ohne abgerundete Ecken.
            Style = "max-width: none; border-radius: 0;",
            AutoFocusFirstElement = false,
            CloseDialogOnEsc = true,
            CloseDialogOnOverlayClick = false,
        }
        : WindowOptions();

    /// <summary>Ein Fenster in der Mitte mit Titelzeile und X, im Browser wie in der App (für kleine Dialoge ohne Formular).</summary>
    public DialogOptions WindowOptions() => new()
    {
        ShowTitle = true,
        ShowClose = true,
        Width = "600px",
        Style = "max-width: 100%;",
        AutoFocusFirstElement = false,
        CloseDialogOnEsc = true,
        CloseDialogOnOverlayClick = true,
    };

    /// <summary>
    /// Öffnet den Dialog und wartet, bis er zu ist. Der Dialog schließt sich mit <c>DialogService.Close(ergebnis)</c>;
    /// ohne Ergebnis (Abbrechen, X, Esc) heißt es "nichts geändert".
    /// </summary>
    public Task<DialogOutcome<TResult>> OpenAsync<TDialog, TResult>(string title, FormParameters<TDialog>? parameters = null)
        where TDialog : ComponentBase =>
        OpenWithAsync<TDialog, TResult>(title, parameters, RadzenOptions());

    /// <summary>
    /// Wie <see cref="OpenAsync{TDialog,TResult}"/>, aber auch in der App als Fenster: für kleine Dialoge, die nichts
    /// eingeben lassen, etwa eine Einladung mit Link und QR-Code. Der Dialog bringt seine Knöpfe selbst mit.
    /// </summary>
    public Task<DialogOutcome<TResult>> OpenWindowAsync<TDialog, TResult>(string title, FormParameters<TDialog>? parameters = null)
        where TDialog : ComponentBase =>
        OpenWithAsync<TDialog, TResult>(title, parameters, WindowOptions());

    /// <summary>
    /// Wie <see cref="OpenAsync{TDialog,TResult}"/>, für Dialoge in Schritten (StepDialog): Der Dialog zeichnet Titel und
    /// Abbrechen selbst. Schließen geht nur über sein Abbrechen, damit die Rückfrage bei Eingaben nicht umgangen wird.
    /// </summary>
    public Task<DialogOutcome<TResult>> OpenStepAsync<TDialog, TResult>(string title, FormParameters<TDialog>? parameters = null)
        where TDialog : ComponentBase =>
        OpenWithAsync<TDialog, TResult>(title, parameters, StepOptions());

    /// <summary>
    /// Die Optionen für Schritt-Dialoge. Browser: höchstens 640 px breit (auf dem schmalen Handy die ganze Breite).
    /// App: Vollbild wie die anderen Formulare.
    /// </summary>
    public DialogOptions StepOptions() => new()
    {
        ShowTitle = false,
        ShowClose = false,
        Width = host.IsApp ? "100%" : "min(640px, 100%)",
        Height = host.IsApp ? "100%" : null,
        Style = host.IsApp ? "max-width: none; border-radius: 0;" : "max-width: 100%;",
        AutoFocusFirstElement = false,
        CloseDialogOnEsc = false,
        CloseDialogOnOverlayClick = false,
    };

    private async Task<DialogOutcome<TResult>> OpenWithAsync<TDialog, TResult>(string title, FormParameters<TDialog>? parameters, DialogOptions options)
        where TDialog : ComponentBase
    {
        // Ohne Titelzeile (Schritt-Dialoge, Vollbild in der App) nennt Radzen das Fenster nur "Dialog"; der Titel gibt ihm seinen Namen.
        options.AriaLabel = title;
        var before = await CountOpenDialogsAsync();
        var closed = dialogs.OpenAsync<TDialog>(title, (parameters ?? new FormParameters<TDialog>()).ToDictionary(), options);
        _ = FocusFirstFieldAsync(before);
        object? result = await closed;
        return result is TResult value ? new DialogOutcome<TResult>(true, value) : DialogOutcome<TResult>.Unchanged;
    }

    /// <summary>
    /// Fragt nach, bevor etwas geschieht, das sich nicht zurücknehmen lässt. Die Texte kommen vom Aufrufer aus den Ressourcen.
    /// </summary>
    /// <returns>true bei Ja, false bei Nein oder Schließen.</returns>
    public async Task<bool> ConfirmAsync(string message, string title, string yesText, string noText)
    {
        // Der Fokus beginnt auf dem sicheren Knopf (Nein), nicht auf Ja: Enter oder Leertaste bestätigen sonst gleich beim Öffnen.
        var options = new ConfirmOptions { OkButtonText = yesText, CancelButtonText = noText, AutoFocusFirstElement = false };
        var before = await CountOpenDialogsAsync();
        var closed = dialogs.Confirm(message, title, options);
        _ = FocusSafeButtonAsync(before);
        return await closed ?? false;
    }

    // Der Fokus ist eine Hilfe; scheitert der Aufruf, geht der Dialog trotzdem auf. Der Grund steht im Protokoll.
    private async Task<int> CountOpenDialogsAsync()
    {
        try
        {
            await using var module = await js.InvokeAsync<IJSObjectReference>("import", FocusModule);
            return await module.InvokeAsync<int>("countDialogs");
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException or InvalidOperationException)
        {
            log.LogDebug(exception, "Die offenen Dialoge ließen sich nicht zählen.");
            return 0;
        }
    }

    private async Task FocusFirstFieldAsync(int before)
    {
        try
        {
            await using var module = await js.InvokeAsync<IJSObjectReference>("import", FocusModule);
            await module.InvokeVoidAsync("focusFirstField", before);
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException or InvalidOperationException)
        {
            log.LogWarning(exception, "Der Fokus ließ sich nicht in das erste Feld des Dialogs setzen.");
        }
    }

    private async Task FocusSafeButtonAsync(int before)
    {
        try
        {
            await using var module = await js.InvokeAsync<IJSObjectReference>("import", FocusModule);
            await module.InvokeVoidAsync("focusSafeButton", before);
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException or InvalidOperationException)
        {
            log.LogWarning(exception, "Der Fokus ließ sich nicht auf den sicheren Knopf der Rückfrage setzen.");
        }
    }

    private const string FocusModule = "./_content/Duofy.Shared/dialog-focus.js";
}
