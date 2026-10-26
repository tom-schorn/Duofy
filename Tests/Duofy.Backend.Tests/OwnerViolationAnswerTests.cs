using System.Text.Json;
using Duofy.Schemas;
using Duofy.Data;
using Duofy.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duofy.Api.Tests;

/// <summary>
/// Ein abgelehntes Speichern für einen fremden Besitzer (Spec 56, R3) wird zu 404 wie für jede Person außerhalb des
/// Haushalts und steht als Fehler im Protokoll, mit Art und Kennungen, ohne Feldwerte. Über die API lässt es sich
/// nicht auslösen, solange jeder Endpunkt seine Prüfung macht; deshalb ruft der Test den Filter direkt auf.
/// </summary>
public class OwnerViolationAnswerTests
{
    private sealed class NobodySignedIn : IAccess
    {
        public Guid? CallerId => null;

        public Task<AccessLevel> LevelAsync(Guid ownerId, Area area, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AccessCheck> RequireAsync(Guid ownerId, Area area, AccessLevel needed, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AccessCheck> RequireAnyViewAsync(Guid ownerId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public AccessCheck RequireSelf(Guid ownerId) => throw new NotSupportedException();
    }

    /// <summary>Merkt sich jede Zeile des Protokolls mit ihrer Stufe.</summary>
    private sealed class Recorder : ILoggerProvider, ILogger
    {
        public List<(LogLevel Level, string Message)> Lines { get; } = [];

        public ILogger CreateLogger(string categoryName) => this;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add((logLevel, formatter(state, exception)));

        public void Dispose()
        {
        }
    }

    // A4 (R3): die Antwort der API
    [Fact]
    public async Task A_refused_save_answers_404_owner_not_found_and_logs_type_and_ids()
    {
        var recorder = new Recorder();
        var services = new ServiceCollection()
            .AddLogging(logging => logging.AddProvider(recorder))
            .AddSingleton<IAccess, NobodySignedIn>()
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        var owner = Guid.NewGuid();
        var caller = Guid.NewGuid();

        await OwnerFilter.RunAsync(context, _ => throw new OwnerViolationException("Booking", "b-1", owner, caller));

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var answer = await JsonSerializer.DeserializeAsync<ErrorResponse>(context.Response.Body, JsonSerializerOptions.Web);
        Assert.Equal(ErrorCodes.OwnerNotFound, answer!.Code);
        var (_, message) = Assert.Single(recorder.Lines, line => line.Level >= LogLevel.Warning);
        Assert.Equal(LogLevel.Error, recorder.Lines.Max(line => line.Level));
        Assert.Contains("Booking", message);
        Assert.Contains("b-1", message);
        Assert.Contains(owner.ToString(), message);
        Assert.Contains(caller.ToString(), message);
    }
}
