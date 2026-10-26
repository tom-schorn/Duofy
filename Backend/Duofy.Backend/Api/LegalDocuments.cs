using System.Text;
using Duofy.Schemas;

namespace Duofy.Api;

/// <summary>
/// Die Rechtstexte dieses Servers (Spec 70, R7): Impressum, Datenschutzerklärung und Nutzungsbedingungen,
/// je eine Textdatei aus den Einstellungen Duofy:ImprintFile, Duofy:PrivacyFile und Duofy:TermsFile
/// (Umgebungsvariablen Duofy__ImprintFile und so weiter).
///
/// Ein Text, der nicht eingestellt, nicht lesbar, leer oder größer als 200 KB ist, gibt es nicht. Der Pfad
/// kommt immer aus der Einstellung und nie aus einer Anfrage; im Log steht nur der Name des Textes, nie sein
/// Pfad. Die Dateien werden bei jeder Anfrage frisch gelesen, damit eine geänderte Datei ohne Neustart gilt.
/// </summary>
internal sealed class LegalDocuments(IConfiguration configuration, ILoggerFactory loggers)
{
    /// <summary>Die größte erlaubte Datei in Bytes (200 KB).</summary>
    public const int MaxBytes = 200 * 1024;

    private readonly ILogger logger = loggers.CreateLogger("Duofy.Api.Legal");

    /// <summary>Der Name in der Adresse und der Schlüssel der Einstellung dazu.</summary>
    private static readonly IReadOnlyDictionary<string, string> Settings = new Dictionary<string, string>
    {
        [LegalDocumentNames.Imprint] = "Duofy:ImprintFile",
        [LegalDocumentNames.Privacy] = "Duofy:PrivacyFile",
        [LegalDocumentNames.Terms] = "Duofy:TermsFile",
    };

    /// <summary>Die Namen der Texte, die es gibt, in der festen Reihenfolge von <see cref="LegalDocumentNames.All"/>.</summary>
    public async Task<IReadOnlyList<string>> AvailableAsync(CancellationToken cancellationToken)
    {
        var available = new List<string>();
        foreach (var name in LegalDocumentNames.All)
        {
            if (await ReadAsync(name, cancellationToken) is not null)
            {
                available.Add(name);
            }
        }
        return available;
    }

    /// <summary>Der Text; null bei einem unbekannten Namen oder einem Text, den es nicht gibt.</summary>
    public async Task<string?> ReadAsync(string name, CancellationToken cancellationToken)
    {
        // Nur genau diese drei Namen, in genau dieser Schreibweise; alles andere ist kein Text.
        if (!Settings.TryGetValue(name, out var setting))
        {
            return null;
        }

        var path = configuration[setting]?.Trim();
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        string text;
        try
        {
            text = await ReadLimitedAsync(path, name, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // Die Meldung der Ausnahme enthält den Pfad, darum loggen wir nur ihre Art.
            logger.LogWarning("Rechtstext {Document} nicht lesbar ({Reason}); er gilt als nicht vorhanden.",
                name, ex.GetType().Name);
            return null;
        }
        catch (TooLargeException)
        {
            logger.LogWarning("Rechtstext {Document} ist größer als {Limit} KB; er gilt als nicht vorhanden.",
                name, MaxBytes / 1024);
            return null;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            logger.LogWarning("Rechtstext {Document} ist leer; er gilt als nicht vorhanden.", name);
            return null;
        }
        return text;
    }

    /// <summary>Liest höchstens <see cref="MaxBytes"/> plus ein Byte, damit auch eine Datei, die währenddessen wächst, nicht den Speicher füllt.</summary>
    private static async Task<string> ReadLimitedAsync(string path, string name, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 4096, useAsync: true);
        var buffer = new byte[MaxBytes + 1];
        var filled = 0;
        while (filled < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(filled), cancellationToken);
            if (read == 0)
            {
                break;
            }
            filled += read;
        }
        if (filled > MaxBytes)
        {
            throw new TooLargeException(name);
        }
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false).GetString(buffer, 0, filled);
    }

    private sealed class TooLargeException(string name) : Exception($"Rechtstext {name} ist zu groß.");
}
