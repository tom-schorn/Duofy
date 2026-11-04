using Duofy.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duofy.Api.Tests;

/// <summary>Ein Fehler eines Anbieters oder eines Durchlaufs hält den Server und die übrigen Geräte nicht auf (Spec 84, M1).</summary>
[Collection(ApiCollection.Name)]
public class ReminderResilienceTests(ApiFixture fixture)
{
    private static readonly DateTimeOffset Morning = ReminderServer.Morning;

    private sealed class ExplodingFcm(string badToken, RecordingPush inner) : IFcmClient
    {
        public Task SendAsync(string token, PushPayload payload, CancellationToken cancellationToken) =>
            token == badToken ? throw new NullReferenceException("unerwartet") : inner.SendAsync(token, payload, cancellationToken);
    }

    private sealed class ThrowingScopes(Exception exception) : IServiceScopeFactory
    {
        public int Calls { get; private set; }

        public IServiceScope CreateScope()
        {
            Calls++;
            throw exception;
        }
    }

    [Fact]
    public async Task A_device_failing_with_an_unexpected_exception_does_not_stop_the_other_devices_or_the_run()
    {
        var push = new RecordingPush();
        string? bad = null;
        await using var server = ReminderServer.Start(fixture, configureServices: services =>
        {
            services.AddSingleton<IFcmClient>(_ => new ExplodingFcm(bad ?? "", push));
            services.AddSingleton<IWnsClient>(push);
        });
        var anna = await server.FounderAsync("Anna");
        bad = await server.DeviceAsync(anna);
        var good = await server.DeviceAsync(anna, DevicePlatform.Windows);
        await server.PositionAsync(anna);

        await server.RunAtAsync(Morning.AddMinutes(90));

        Assert.Single(push.To(good));
    }

    [Theory]
    [InlineData(typeof(NullReferenceException))]
    [InlineData(typeof(ArgumentException))]
    public async Task The_job_keeps_running_after_any_unexpected_exception_in_a_run(Type type)
    {
        await using var server = ReminderServer.Start(fixture);
        var scopes = new ThrowingScopes((Exception)Activator.CreateInstance(type)!);
        var job = new ReminderJob(scopes, server.Factory.Services.GetRequiredService<ReminderSwitch>(), new ManualTime(Morning), NullLoggerFactory.Instance);

        var keepsRunning = await job.RunOnceAsync(CancellationToken.None);

        Assert.True(keepsRunning);
        Assert.Equal(1, scopes.Calls);
    }

    [Fact]
    public async Task The_job_stops_quietly_when_it_is_cancelled()
    {
        await using var server = ReminderServer.Start(fixture);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var scopes = new ThrowingScopes(new OperationCanceledException(cts.Token));
        var job = new ReminderJob(scopes, server.Factory.Services.GetRequiredService<ReminderSwitch>(), new ManualTime(Morning), NullLoggerFactory.Instance);

        Assert.False(await job.RunOnceAsync(cts.Token));
    }
}
