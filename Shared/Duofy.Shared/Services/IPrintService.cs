using Microsoft.JSInterop;

namespace Duofy.Shared.Services;

/// <summary>
/// Druckt die Seite, die gerade zu sehen ist (Spec 125, R4): Das Stylesheet blendet beim Drucken alles außer dem Druckbild
/// des Monats aus. Im Browser ist das der Druckdialog des Browsers; die App kann den Dienst durch den Druck ihres Systems
/// ersetzen, denn die eingebettete Ansicht kennt <c>window.print</c> nicht überall.
/// </summary>
public interface IPrintService
{
    Task PrintAsync();
}

/// <summary>Druckt über den Browser: <c>window.print()</c>.</summary>
public sealed class WindowPrintService(IJSRuntime js) : IPrintService
{
    public async Task PrintAsync() => await js.InvokeVoidAsync("print");
}
