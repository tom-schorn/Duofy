using System.Collections.Concurrent;
using Duofy.Data;
using Duofy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Api.Tests;

/// <summary>Eine Uhr, die nur steht, wo der Test sie hinstellt; ihre Zeitgeber feuern nie, so läuft der Job nicht nebenher.</summary>
internal sealed class ManualTime : TimeProvider
{
    private DateTimeOffset now;

    public ManualTime(DateTimeOffset now) => this.now = now.ToUniversalTime();

    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

    public void Set(DateTimeOffset moment) => now = moment.ToUniversalTime();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        System.CreateTimer(callback, state, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
}

/// <summary>Der Ersatz für FCM und WNS (Spec 84): Er schickt nichts, sondern merkt sich jedes Signal.</summary>
internal sealed class RecordingPush : IFcmClient, IWnsClient
{
    private readonly ConcurrentQueue<(string Provider, string Token, PushPayload Payload)> sent = new();

    public IReadOnlyList<(string Provider, string Token, PushPayload Payload)> All => sent.ToList();

    public IReadOnlyList<(string Provider, string Token, PushPayload Payload)> To(string token) =>
        sent.Where(s => s.Token == token).ToList();

    public Task SendAsync(string token, PushPayload payload, CancellationToken cancellationToken)
    {
        sent.Enqueue(("fcm", token, payload));
        return Task.CompletedTask;
    }

    Task IWnsClient.SendAsync(string channel, PushPayload payload, CancellationToken cancellationToken)
    {
        sent.Enqueue(("wns", channel, payload));
        return Task.CompletedTask;
    }
}

/// <summary>Ein Server mit eingeschalteten Erinnerungen, fester Uhr und Push-Ersatz (Spec 84).</summary>
internal sealed class ReminderServer : IAsyncDisposable
{
    /// <summary>10. November 2026, 07:30 in Berlin (06:30 UTC, Winterzeit).</summary>
    public static readonly DateTimeOffset Morning = new(2026, 11, 10, 7, 30, 0, TimeSpan.FromHours(1));

    private ReminderServer(MailServer server, ManualTime time, RecordingPush push)
    {
        Server = server;
        Time = time;
        Push = push;
    }

    public MailServer Server { get; }

    public ManualTime Time { get; }

    public RecordingPush Push { get; }

    public HttpClient Client => Server.Client;

    public DuofyApiFactory Factory => Server.Factory;

    public static ReminderServer Start(ApiFixture fixture, bool enabled = true, bool hosted = true, Action<IServiceCollection>? configureServices = null)
    {
        var settings = hosted ? MailServer.HostedSettings() : new Dictionary<string, string> { ["Duofy:Hosted"] = "false" };
        settings["Duofy:Reminders:Enabled"] = enabled ? "true" : "false";
        var time = new ManualTime(Morning);
        var push = new RecordingPush();
        var server = MailServer.Start(fixture, settings, services =>
        {
            services.AddSingleton<TimeProvider>(time);
            services.AddSingleton<IFcmClient>(push);
            services.AddSingleton<IWnsClient>(push);
            configureServices?.Invoke(services);
        });
        return new ReminderServer(server, time, push);
    }

    /// <summary>Eine bestätigte, angemeldete Person (die gehostete Version verlangt die Bestätigung der Adresse).</summary>
    public Task<Person> PersonAsync(string firstName) => Server.ConfirmedPersonAsync(firstName);

    /// <summary>Eine bestätigte Person, die einen Haushalt gegründet hat.</summary>
    public async Task<Person> FounderAsync(string firstName)
    {
        var person = await PersonAsync(firstName);
        await Client.FoundAsync(person);
        return person;
    }

    /// <summary>Lässt den Job einmal zu diesem Zeitpunkt laufen.</summary>
    public async Task RunAtAsync(DateTimeOffset moment)
    {
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ReminderRunner>().RunAsync(moment, CancellationToken.None);
    }

    /// <summary>Meldet ein Gerät an, an der API vorbei; gibt das Token zurück.</summary>
    public async Task<string> DeviceAsync(Person person, DevicePlatform platform = DevicePlatform.Android)
    {
        var token = $"token-{Guid.NewGuid():N}";
        await Factory.WithDbAsync(async db =>
        {
            db.ReminderDevices.Add(new ReminderDevice
            {
                UserId = person.Id,
                InstallationId = $"install-{Guid.NewGuid():N}",
                Platform = platform,
                PushToken = token,
                LastSeenAt = Morning.ToUniversalTime(),
            });
            await db.SaveChangesAsync();
            return 0;
        });
        return token;
    }

    /// <summary>Legt dem Besitzer einen Posten im Monat von <see cref="Morning"/> an; gibt seine Kennung zurück.</summary>
    public Task<Guid> PositionAsync(
        Person owner, string label = "GEZ", PaymentMethod method = PaymentMethod.Transfer, int dueDay = 10,
        CommitmentType type = CommitmentType.Contract, bool done = false) =>
        Factory.WithDbAsync(async db =>
        {
            var plan = await db.Plans.Include(p => p.Positions)
                .FirstOrDefaultAsync(p => p.OwnerId == owner.Id && p.Year == Morning.Year && p.Month == Morning.Month);
            if (plan is null)
            {
                plan = new Plan { Id = Guid.NewGuid(), OwnerId = owner.Id, Year = Morning.Year, Month = Morning.Month, CreatedAt = Morning.ToUniversalTime() };
                db.Plans.Add(plan);
            }
            var categoryId = await db.Categories.Where(c => c.OwnerId == owner.Id || c.OwnerId == null).Select(c => c.Id).FirstAsync();
            var position = new PlanPosition
            {
                Id = Guid.NewGuid(),
                Label = label,
                AmountPlanned = 18.36m,
                DueDay = dueDay,
                Type = type,
                CategoryId = categoryId,
                PaymentMethod = method,
                DoneAt = done ? Morning.ToUniversalTime() : null,
            };
            plan.Positions.Add(position);
            await db.SaveChangesAsync();
            return position.Id;
        });

    /// <summary>Stellt die Uhrzeit ein, zu der die Person erinnert wird.</summary>
    public Task SettingsAsync(Person person, TimeOnly time, bool enabled = true) =>
        Factory.WithDbAsync(async db =>
        {
            db.ReminderSettings.Add(new ReminderSettings { UserId = person.Id, Time = time, Enabled = enabled });
            await db.SaveChangesAsync();
            return 0;
        });

    /// <summary>Hakt den Posten von Hand ab, an der API vorbei.</summary>
    public Task TickAsync(Guid positionId) =>
        Factory.WithDbAsync(async db =>
        {
            await db.PlanPositions.Where(p => p.Id == positionId).ExecuteUpdateAsync(s => s.SetProperty(p => p.DoneAt, Morning.ToUniversalTime()));
            return 0;
        });

    public Task<List<Reminder>> RemindersOfAsync(Guid positionId) =>
        Factory.WithDbAsync(db => db.Reminders.Where(r => r.PositionId == positionId).ToListAsync());

    public ValueTask DisposeAsync() => Server.DisposeAsync();
}
