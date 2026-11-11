using Duofy.Shared.Resources;
using Duofy.Shared.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Duofy.Services;

/// <summary>Zeichnet das Druckbild der Seite, die gerade zu sehen ist, als PDF-Datei (Spec 125, R6).</summary>
public interface IPrintViewRenderer
{
    /// <summary>Legt das PDF unter <paramref name="fileName"/> im Cache der App ab und gibt den Pfad der Datei zurück.</summary>
    Task<string> RenderPdfAsync(string fileName, CancellationToken cancellationToken = default);

    /// <summary>Löscht die Datei wieder aus dem Cache der App; das PDF gehört nach dem Teilen dem, an den es ging.</summary>
    void Discard(string path);
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
public sealed class PdfPrintService(
    IPrintViewRenderer renderer,
    IPdfShare share,
    IStringLocalizer<SharedResources> l,
    ILogger<PdfPrintService> log,
    Func<DateTime>? clock = null) : IPrintService
{
    private readonly Func<DateTime> clock = clock ?? (() => DateTime.Now);

    public async Task PrintAsync()
    {
        var path = await renderer.RenderPdfAsync($"plan-{clock():yyyyMMdd-HHmmss}.pdf");
        try
        {
            await share.ShareFileAsync(l["Print_ShareTitle"], path, "application/pdf");
        }
        finally
        {
            // Der Teilen-Dialog hat seine eigene Kopie; die Datei im Cache wächst sonst mit jedem Druck.
            try
            {
                renderer.Discard(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                log.LogWarning(exception, "Das PDF {Path} ließ sich nicht aus dem Cache löschen.", path);
            }
        }
    }
}
