using Duofy.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Duofy.Api;

/// <summary>
/// Die API ist nur gesund, wenn auch die Datenbank antwortet (Spec 107, R6). Die Antwort nennt keine
/// Verbindungsdaten, nur gesund oder nicht.
/// </summary>
public sealed class DatabaseHealthCheck(DuofyDbContext db, ILogger<DatabaseHealthCheck> log) : IHealthCheck
{
    /// <summary>So lange wartet die Prüfung auf die Datenbank, bevor sie sie für nicht erreichbar hält.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // Eine eigene, nicht gebündelte Verbindung statt des Datenbankkontexts der Anfrage: Hängt die Datenbank, würde
        // das Aufräumen des Kontexts nach der Prüfung selbst über eine Minute warten (im Test gemessen) und die Antwort
        // so um genau diese Zeit verspäten. Diese Verbindung wird bei einer Zeitüberschreitung im Hintergrund
        // verworfen, ohne dass die Antwort darauf wartet.
        var connectionString = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString())
        {
            Pooling = false,
            Timeout = (int)Timeout.TotalSeconds,
            CommandTimeout = (int)Timeout.TotalSeconds,
        }.ConnectionString;

        // Die Prüfung läuft als eigene Aufgabe mit eigenem Abbruch und gibt ihre Verbindung selbst frei, erst wenn sie
        // zu Ende ist (Npgsql verbietet das Schließen während OpenAsync). So wird die Verbindung in jedem Pfad genau
        // einmal freigegeben: Erfolg, Fehler, Zeitüberschreitung und Abbruch durch den Aufrufer.
        using var probeCancel = new CancellationTokenSource();
        var probe = RunProbeAsync(connectionString, probeCancel.Token);
        try
        {
            if (await probe.WaitAsync(Timeout, cancellationToken))
            {
                return HealthCheckResult.Healthy();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await probeCancel.CancelAsync();
            throw;
        }
        catch (TimeoutException)
        {
            // Antwortet zu spät: gilt wie "antwortet nicht". Die Prüfung wird abgebrochen und räumt im Hintergrund auf,
            // die Antwort wartet nicht darauf.
            await probeCancel.CancelAsync();
        }
        log.LogWarning("Die Datenbank antwortet nicht (Gesundheitsprüfung).");
        return HealthCheckResult.Unhealthy("Die Datenbank antwortet nicht.");
    }

    /// <summary>
    /// Öffnet die Verbindung, fragt <c>SELECT 1</c> und gibt die Verbindung danach in jedem Fall frei. Fehler der
    /// Datenbank liefern false; ihre Ausnahme bleibt aus dem Protokoll, weil sie Verbindungswerte (Rechner, Anschluss) nennen kann.
    /// </summary>
    private async Task<bool> RunProbeAsync(string connectionString, CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            return await command.ExecuteScalarAsync(cancellationToken) is 1;
        }
        catch (Exception ex) when (ex is NpgsqlException or OperationCanceledException or IOException or TimeoutException)
        {
            return false;
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    /// <summary>Gibt die Verbindung frei; schlägt das fehl, steht Phase und Art des Fehlers im Protokoll, nie dessen Text.</summary>
    private async Task DisposeAsync(NpgsqlConnection connection)
    {
        try
        {
            await connection.DisposeAsync();
        }
        catch (Exception ex) when (ex is NpgsqlException or IOException or ObjectDisposedException or InvalidOperationException)
        {
            log.LogWarning("Die Verbindung der Gesundheitsprüfung ließ sich nicht freigeben (Phase {Phase}, Ausnahme {ExceptionType}).",
                "Freigeben", ex.GetType().Name);
        }
    }
}
