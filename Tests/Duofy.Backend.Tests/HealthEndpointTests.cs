using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Duofy.Api.Tests;

/// <summary>
/// Die Gesundheit der API hängt an der Datenbank (Spec 107, R6, A1): /api/v1/health antwortet nur 200, wenn die
/// Datenbank antwortet, sonst 503 - ohne Verbindungsdaten im Text und in den Protokollen.
/// </summary>
public sealed class HealthEndpointTests
{
    /// <summary>Die Prüfung wartet höchstens 3 Sekunden; die Antwort muss deutlich vor dieser Grenze plus Reserve kommen.</summary>
    private static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task A1_health_is_healthy_with_the_database()
    {
        await using var database = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await database.StartAsync();
        await using var api = new DuofyApiFactory(database.GetConnectionString(), authPermitLimit: 10_000);
        var client = api.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A1_health_is_unhealthy_without_the_database()
    {
        await using var database = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await database.StartAsync();
        var connectionString = database.GetConnectionString();
        var logs = new CapturedLogs();
        await using var api = new DuofyApiFactory(connectionString, authPermitLimit: 10_000).WithCapturedLogs(logs);
        var client = api.CreateClient();
        // Die API ist gestartet (die Migrationen sind durch); jetzt verschwindet die Datenbank.
        await database.StopAsync();
        logs.Clear();

        var response = await client.GetAsync("/api/v1/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        AssertNoConnectionValues(connectionString, body, logs);
    }

    [Fact]
    public async Task A1_health_is_unhealthy_within_the_time_limit_when_the_database_hangs()
    {
        await using var database = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await database.StartAsync();
        // Die API spricht über einen Zwischenstopp mit der Datenbank; der kann sie später "hängen" lassen:
        // Er nimmt weiter Verbindungen an, antwortet aber nie - das ist keine abgelehnte, sondern eine hängende Verbindung.
        var real = new NpgsqlConnectionStringBuilder(database.GetConnectionString());
        await using var proxy = new HangingProxy(real.Host!, real.Port);
        var viaProxy = new NpgsqlConnectionStringBuilder(real.ConnectionString) { Host = "127.0.0.1", Port = proxy.Port }.ConnectionString;
        var logs = new CapturedLogs();
        await using var api = new DuofyApiFactory(viaProxy, authPermitLimit: 10_000).WithCapturedLogs(logs);
        var client = api.CreateClient();
        // Hier sind die Migrationen durch; ab jetzt bleibt jede Verbindung ohne Antwort.
        proxy.Hang();
        logs.Clear();

        var watch = Stopwatch.StartNew();
        var response = await client.GetAsync("/api/v1/health");
        watch.Stop();
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.ServiceUnavailable, $"{response.StatusCode} {body}\n{logs.Text}");
        Assert.True(watch.Elapsed < TimeLimit, $"Die Antwort kam erst nach {watch.Elapsed.TotalSeconds:F1} s.\n{logs.Text}");
        AssertNoConnectionValues(viaProxy, body, logs);
        // Die Prüfung räumt ihre Verbindung auf: Der Zwischenstopp sieht die Verbindung geschlossen.
        await proxy.WaitUntilNoHangingConnectionsAsync(TimeLimit);
        Assert.Equal(0, proxy.OpenHangingConnections);
        Assert.True(proxy.HangingConnectionsSeen > 0, "Die Prüfung hat keine Verbindung aufgebaut.");
    }

    [Fact]
    public async Task A1_health_releases_its_connection_when_the_caller_cancels()
    {
        await using var database = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await database.StartAsync();
        var real = new NpgsqlConnectionStringBuilder(database.GetConnectionString());
        await using var proxy = new HangingProxy(real.Host!, real.Port);
        var viaProxy = new NpgsqlConnectionStringBuilder(real.ConnectionString) { Host = "127.0.0.1", Port = proxy.Port }.ConnectionString;
        await using var api = new DuofyApiFactory(viaProxy, authPermitLimit: 10_000);
        _ = api.CreateClient();
        proxy.Hang();
        using var scope = api.Services.CreateScope();
        var check = new Duofy.Api.DatabaseHealthCheck(
            scope.ServiceProvider.GetRequiredService<Duofy.Data.DuofyDbContext>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Duofy.Api.DatabaseHealthCheck>.Instance);
        using var cancel = new CancellationTokenSource();

        var running = check.CheckHealthAsync(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext(), cancel.Token);
        await proxy.WaitUntilHangingConnectionAsync(TimeLimit);
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        await proxy.WaitUntilNoHangingConnectionsAsync(TimeLimit);
        Assert.Equal(0, proxy.OpenHangingConnections);
    }

    /// <summary>Weder die Antwort noch ein Protokolleintrag (auch von Framework und Gesundheitsprüfung) nennt echte Verbindungswerte.</summary>
    private static void AssertNoConnectionValues(string connectionString, string body, CapturedLogs logs)
    {
        var parts = new NpgsqlConnectionStringBuilder(connectionString);
        var everything = body + "\n" + logs.Text;
        Assert.NotEmpty(logs.Text); // Sonst prüft der Test an leeren Protokollen vorbei.

        foreach (var (name, value) in new[]
        {
            ("Host", parts.Host), ("Database", parts.Database), ("Username", parts.Username), ("Password", parts.Password),
        })
        {
            Assert.False(string.IsNullOrEmpty(value), $"{name} fehlt in der Testverbindung.");
            Assert.DoesNotContain(value!, everything, StringComparison.OrdinalIgnoreCase);
        }
        // Der Anschluss ist eine Zahl: nur als ganze Zahl suchen, nicht als Teil einer anderen.
        Assert.DoesNotMatch(new Regex($@"(?<!\d){parts.Port}(?!\d)"), everything);
        Assert.DoesNotContain("Host=", everything, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", everything, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Sammelt alle Protokolleinträge der API, jeder Kategorie und Stufe, samt Ausnahmen.</summary>
    private sealed class CapturedLogs : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> lines = new();

        public string Text => string.Join("\n", lines);

        /// <summary>Verwirft den Start der API (Migrationen heißen z. B. "...PasswordReset..."); geprüft wird, was die Anfrage selbst schreibt.</summary>
        public void Clear() => lines.Clear();

        public ILogger CreateLogger(string categoryName) => new Capture(categoryName, lines);

        public void Dispose()
        {
        }

        private sealed class Capture(string category, ConcurrentQueue<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                lines.Enqueue($"{category} [{logLevel}] {formatter(state, exception)} {exception}");
        }
    }

    /// <summary>Leitet Verbindungen an die Datenbank weiter, bis <see cref="Hang"/> gerufen wird; danach kommt nichts mehr zurück.</summary>
    private sealed class HangingProxy : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new();
        private readonly string host;
        private readonly int port;
        private readonly Task accepting;
        private volatile bool hanging;

        public HangingProxy(string host, int port)
        {
            this.host = host;
            this.port = port;
            listener.Start();
            accepting = AcceptAsync();
        }

        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

        public void Hang() => hanging = true;

        private int hangingOpen;
        private int hangingSeen;

        /// <summary>Verbindungen, die im hängenden Zustand angenommen wurden und die Gegenseite noch nicht geschlossen hat.</summary>
        public int OpenHangingConnections => Volatile.Read(ref hangingOpen);

        public int HangingConnectionsSeen => Volatile.Read(ref hangingSeen);

        public Task WaitUntilHangingConnectionAsync(TimeSpan limit) => WaitForAsync(() => OpenHangingConnections > 0, limit);

        public Task WaitUntilNoHangingConnectionsAsync(TimeSpan limit) => WaitForAsync(() => OpenHangingConnections == 0, limit);

        private static async Task WaitForAsync(Func<bool> condition, TimeSpan limit)
        {
            var end = Stopwatch.StartNew();
            while (!condition() && end.Elapsed < limit)
            {
                await Task.Delay(50);
            }
        }

        private async Task AcceptAsync()
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync(stop.Token);
                    _ = Task.Run(() => ServeAsync(client));
                }
            }
            catch (OperationCanceledException)
            {
                // Test zu Ende.
            }
        }

        private async Task ServeAsync(TcpClient client)
        {
            using var _ = client;
            try
            {
                if (hanging)
                {
                    Interlocked.Increment(ref hangingSeen);
                    Interlocked.Increment(ref hangingOpen);
                    try
                    {
                        // Liest und verwirft alles, bis die Gegenseite die Verbindung schließt.
                        var sink = new byte[1024];
                        while (await client.GetStream().ReadAsync(sink, stop.Token) > 0)
                        {
                        }
                    }
                    finally
                    {
                        Interlocked.Decrement(ref hangingOpen);
                    }
                    return;
                }
                using var upstream = new TcpClient();
                await upstream.ConnectAsync(host, port, stop.Token);
                await Task.WhenAll(
                    PipeAsync(client.GetStream(), upstream.GetStream()),
                    PipeAsync(upstream.GetStream(), client.GetStream()));
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException or ObjectDisposedException)
            {
                // Verbindung zu Ende oder Test zu Ende.
            }
        }

        private async Task PipeAsync(Stream from, Stream to)
        {
            var buffer = new byte[8192];
            int read;
            while ((read = await from.ReadAsync(buffer, stop.Token)) > 0)
            {
                if (hanging)
                {
                    // Gelesenes wird verschluckt und nicht weitergegeben: die Gegenseite wartet vergeblich.
                    continue;
                }
                await to.WriteAsync(buffer.AsMemory(0, read), stop.Token);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await stop.CancelAsync();
            listener.Stop();
            await accepting;
            stop.Dispose();
        }
    }
}

internal static class HealthTestFactoryExtensions
{
    /// <summary>Schaltet in der API alle Protokolle (jede Stufe) zusätzlich auf den Sammler.</summary>
    internal static WebApplicationFactory<Program> WithCapturedLogs(this DuofyApiFactory factory, ILoggerProvider logs) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Trace);
            logging.AddProvider(logs);
        }));
}
