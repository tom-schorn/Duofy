using Duofy.Shared.Services;

namespace Duofy.Services;

/// <summary>Zeichnet das Druckbild der Seite, die gerade zu sehen ist, als PDF-Datei (Spec 125, R6).</summary>
public interface IPrintViewRenderer
{
    /// <summary>Legt das PDF unter <paramref name="fileName"/> im Cache der App ab und gibt den Pfad der Datei zurück.</summary>
    Task<string> RenderPdfAsync(string fileName, CancellationToken cancellationToken = default);
}

/// <summary>Gibt eine Datei über den Teilen-Dialog des Systems weiter.</summary>
public interface IPdfShare
{
    Task ShareFileAsync(string title, string path, string contentType);
}

/// <summary>
/// Drucken in der App (Spec 125, R6): Statt zu drucken, erzeugt die App aus dem Druckbild des gewählten Monats ein PDF und
/// öffnet damit den Teilen-Dialog des Systems; von dort geht es an einen Drucker, eine Ablage oder einen Messenger.
/// </summary>
public sealed class PdfPrintService(IPrintViewRenderer renderer, IPdfShare share, Func<DateTime>? clock = null) : IPrintService
{
    private readonly Func<DateTime> clock = clock ?? (() => DateTime.Now);

    public async Task PrintAsync()
    {
        var path = await renderer.RenderPdfAsync($"plan-{clock():yyyyMMdd-HHmmss}.pdf");
        await share.ShareFileAsync("Plan", path, "application/pdf");
    }
}
