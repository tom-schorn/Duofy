using Duofy.ApiClient;
using Duofy.Push;
using Duofy.Schemas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duofy.Push.Tests;

/// <summary>Spec 84, A5, A6, A10: Was die App mit dem Signal und den Knöpfen tut (mit gespielten Signalen und Antworten).</summary>
public class ReminderPushTests
{
    // A10 (R1, R4): Ein Signal "due" holt den Text und zeigt die Benachrichtigung mit den Knöpfen.
    [Fact]
    public async Task A_due_signal_shows_the_fetched_text_with_the_buttons()
    {
        var api = new FakeReminderApi();
        var reminder = Samples.Reminder(label: "GEZ");
        api.Reminders[reminder.Id] = reminder;
        var notifier = new FakeNotifier();

        await Samples.Signals(api, notifier).HandleAsync(new PushSignal(reminder.Id, PushSignal.Due));

        var shown = Assert.Single(notifier.Shown);
        Assert.Equal("GEZ ist heute fällig", shown.Title);
        Assert.True(shown.WithActions);
        Assert.Equal(reminder.Id, shown.ReminderId);
        Assert.Equal($"/plan/2026/11?haken={reminder.PositionId}&besitzer={reminder.OwnerId}", shown.Path);
        Assert.Equal(("Bezahlt", "Später"), (ReminderTexts.PaidAction, ReminderTexts.LaterAction));
    }

    // R4: Scheitert das Holen (offline), erscheint der Text ohne Namen und ohne Knöpfe.
    [Fact]
    public async Task A_failed_fetch_shows_the_text_without_a_name()
    {
        var api = new FakeReminderApi { GetFailure = ClientErrorCodes.ServerUnreachable };
        var notifier = new FakeNotifier();
        var id = Guid.NewGuid();

        await Samples.Signals(api, notifier).HandleAsync(new PushSignal(id, PushSignal.Due));

        var shown = Assert.Single(notifier.Shown);
        Assert.Equal("Ein Posten ist heute fällig", shown.Title);
        Assert.False(shown.WithActions);
        Assert.Equal(ReminderPaths.CurrentMonth, shown.Path);
        Assert.Equal(id, shown.ReminderId);
    }

    // Gehört die Erinnerung der Person nicht (mehr), zeigt die App nichts.
    [Fact]
    public async Task A_reminder_that_is_not_for_this_person_shows_nothing()
    {
        var notifier = new FakeNotifier();

        await Samples.Signals(new FakeReminderApi(), notifier).HandleAsync(new PushSignal(Guid.NewGuid(), PushSignal.Due));

        Assert.Empty(notifier.Shown);
    }

    // R7: "withdraw" nimmt genau diese Benachrichtigung weg und fragt den Server nicht.
    // S4: Ist niemand angemeldet, zeigt ein spätes Signal nichts (auch keinen Hinweis ohne Namen).
    [Fact]
    public async Task A_due_signal_without_a_sign_in_shows_nothing()
    {
        var notifier = new FakeNotifier();

        await Samples.Signals(new FakeReminderApi { GetFailure = ClientErrorCodes.NotSignedIn }, notifier).HandleAsync(new PushSignal(Guid.NewGuid(), PushSignal.Due));

        Assert.Empty(notifier.Shown);
    }

    [Fact]
    public async Task A_withdraw_signal_removes_the_notification()
    {
        var notifier = new FakeNotifier();
        var id = Guid.NewGuid();

        await Samples.Signals(new FakeReminderApi { GetFailure = ClientErrorCodes.ServerUnreachable }, notifier).HandleAsync(new PushSignal(id, PushSignal.Withdraw));

        Assert.Equal([id], notifier.Cancelled);
        Assert.Empty(notifier.Shown);
    }

    // A5: "Bezahlt" hakt mit dem geplanten Betrag ab, für den Besitzer des Postens, und die Benachrichtigung verschwindet.
    [Fact]
    public async Task Paid_ticks_with_the_planned_amount_and_removes_the_notification()
    {
        var api = new FakeReminderApi();
        var reminder = Samples.Reminder(amount: 55.08m);
        api.Reminders[reminder.Id] = reminder;
        var ticker = new FakeTicker();
        var notifier = new FakeNotifier();

        var outcome = await Samples.Actions(api, ticker, notifier).PaidAsync(reminder.Id);

        Assert.Equal(ReminderActionOutcome.Done, outcome);
        Assert.Equal((2026, 11, reminder.PositionId, reminder.OwnerId, 55.08m), Assert.Single(ticker.Ticked));
        Assert.Equal([reminder.Id], notifier.Cancelled);
        Assert.Empty(notifier.Messages);
    }

    // S3: Ohne Betrag (kein Recht auf den Plan) wird nichts abgehakt, sondern die kurze Meldung gezeigt.
    [Fact]
    public async Task Paid_without_a_visible_amount_ticks_nothing_and_shows_the_refusal_message()
    {
        var api = new FakeReminderApi();
        var reminder = Samples.Reminder() with { AmountPlanned = null };
        api.Reminders[reminder.Id] = reminder;
        var ticker = new FakeTicker();
        var notifier = new FakeNotifier();

        var outcome = await Samples.Actions(api, ticker, notifier).PaidAsync(reminder.Id);

        Assert.Equal(ReminderActionOutcome.Refused, outcome);
        Assert.Empty(ticker.Ticked);
        Assert.Single(notifier.Messages);
    }

    // A6: Ohne das Recht zum Abhaken lehnt der Server mit seinem Code ab; eine kurze Meldung ersetzt die Benachrichtigung.
    [Fact]
    public async Task Paid_without_the_right_is_refused_and_shows_a_short_message()
    {
        var api = new FakeReminderApi();
        var reminder = Samples.Reminder();
        api.Reminders[reminder.Id] = reminder;
        var ticker = new FakeTicker { Refusal = ErrorCodes.NoEditGranted };
        var notifier = new FakeNotifier();

        var outcome = await Samples.Actions(api, ticker, notifier).PaidAsync(reminder.Id);

        Assert.Equal(ReminderActionOutcome.Refused, outcome);
        Assert.Empty(ticker.Ticked);
        Assert.Empty(notifier.Cancelled);
        var message = Assert.Single(notifier.Messages);
        Assert.Equal(reminder.Id, message.Id);
        Assert.Equal("Du darfst diesen Posten nicht abhaken. Öffne die App, um mehr zu sehen.", message.Message);
        Assert.Contains("haken=", message.Path);
    }

    [Fact]
    public async Task Paid_for_a_reminder_that_is_gone_shows_a_message_too()
    {
        var notifier = new FakeNotifier();
        var id = Guid.NewGuid();

        var outcome = await Samples.Actions(new FakeReminderApi(), new FakeTicker(), notifier).PaidAsync(id);

        Assert.Equal(ReminderActionOutcome.Refused, outcome);
        Assert.Equal(id, Assert.Single(notifier.Messages).Id);
    }

    // A5: "Später" verschiebt die Erinnerung beim Server und nimmt die Benachrichtigung weg; abgehakt wird nichts.
    [Fact]
    public async Task Later_postpones_the_reminder_and_removes_the_notification()
    {
        var api = new FakeReminderApi();
        var reminder = Samples.Reminder();
        api.Reminders[reminder.Id] = reminder;
        var ticker = new FakeTicker();
        var notifier = new FakeNotifier();

        var outcome = await Samples.Actions(api, ticker, notifier).LaterAsync(reminder.Id);

        Assert.Equal(ReminderActionOutcome.Done, outcome);
        Assert.Equal([reminder.Id], api.Postponed);
        Assert.Equal([reminder.Id], notifier.Cancelled);
        Assert.Empty(ticker.Ticked);
    }

    // R4: Das Signal hat nur die Kennung und die Art; alles andere lehnt die App ab.
    [Fact]
    public void The_signal_parser_accepts_only_the_id_and_a_known_type()
    {
        var id = Guid.NewGuid();

        Assert.True(PushSignal.TryParse(new Dictionary<string, string> { ["reminderId"] = id.ToString(), ["type"] = "due" }, out var due));
        Assert.Equal(new PushSignal(id, "due"), due);
        Assert.True(PushSignal.TryParseJson($"{{\"reminderId\":\"{id}\",\"type\":\"withdraw\"}}", out var withdraw));
        Assert.Equal(new PushSignal(id, "withdraw"), withdraw);

        Assert.False(PushSignal.TryParse(new Dictionary<string, string> { ["reminderId"] = "kein-guid", ["type"] = "due" }, out _));
        Assert.False(PushSignal.TryParse(new Dictionary<string, string> { ["reminderId"] = id.ToString(), ["type"] = "boom" }, out _));
        Assert.False(PushSignal.TryParse(new Dictionary<string, string>(), out _));
        Assert.False(PushSignal.TryParseJson("das ist kein json", out _));
        Assert.False(PushSignal.TryParseJson("[1,2]", out _));
        Assert.False(PushSignal.TryParseJson(null, out _));
    }

    // Die Hauptaktivität ist für andere Apps offen: Nur die Adressen, die die Benachrichtigung selbst baut, öffnen die App.
    [Theory]
    [InlineData("/plan", true)]
    [InlineData("/plan/2026/11", true)]
    [InlineData("/plan/2026/11?haken=0f7c6f0e-5c1b-4b5e-9d63-3c8b2f2a7a11&besitzer=0f7c6f0e-5c1b-4b5e-9d63-3c8b2f2a7a12", true)]
    [InlineData("/konto/loeschen", false)]
    [InlineData("/admin", false)]
    [InlineData("//evil.example/plan", false)]
    [InlineData("https://evil.example/plan", false)]
    [InlineData("/plan/2026/13", false)]
    [InlineData("/plan/2026/11?x=1", false)]
    [InlineData("/plan/../admin", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_the_paths_a_notification_builds_may_open_the_app(string? path, bool allowed)
    {
        Assert.Equal(allowed, ReminderPaths.IsAllowed(path));
    }

    [Fact]
    public void The_path_of_a_notification_is_always_allowed()
    {
        Assert.True(ReminderPaths.IsAllowed(ReminderPaths.Month(2026, 1, Guid.NewGuid(), Guid.NewGuid())));
        Assert.True(ReminderPaths.IsAllowed(ReminderPaths.CurrentMonth));
    }

    // R1: Die Anmeldung des Geräts schickt Plattform, Token und die Kennung der Installation.
    [Fact]
    public async Task Registering_sends_platform_token_and_installation()
    {
        var api = new FakeReminderApi();
        var registrar = new DeviceRegistrar(api, new FakeTokenSource("fcm-token"), new FixedInstallation("inst"), NullLogger<DeviceRegistrar>.Instance);

        Assert.True(await registrar.RegisterAsync());

        Assert.Equal(("inst", "android", "fcm-token"), Assert.Single(api.Registered));
    }

    [Fact]
    public async Task Without_a_token_or_on_a_server_without_reminders_nothing_is_registered_and_nothing_throws()
    {
        var noToken = new FakeReminderApi();
        Assert.False(await new DeviceRegistrar(noToken, new FakeTokenSource(null), new FixedInstallation(), NullLogger<DeviceRegistrar>.Instance).RegisterAsync());
        Assert.Empty(noToken.Registered);

        var selfHosted = new FakeReminderApi { RegisterFailure = ClientErrorCodes.NotFound };
        Assert.False(await new DeviceRegistrar(selfHosted, new FakeTokenSource("t"), new FixedInstallation(), NullLogger<DeviceRegistrar>.Instance).RegisterAsync());
    }

    private sealed class MemoryTokenStore : ITokenStore
    {
        public AuthTokens? Tokens { get; set; }
        public Task<AuthTokens?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Tokens);
        public Task SaveAsync(AuthTokens tokens, CancellationToken cancellationToken = default) { Tokens = tokens; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default) { Tokens = null; return Task.CompletedTask; }
    }

    private static (DeviceRegisteringTokenStore Store, MemoryTokenStore Inner, FakeReminderApi Api) Decorated(FakeReminderApi? api = null)
    {
        api ??= new FakeReminderApi();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IReminderApi>(api);
        services.AddSingleton<IPushTokenSource>(new FakeTokenSource("fcm-token"));
        services.AddSingleton<IInstallationIdStore>(new FixedInstallation("inst"));
        services.AddScoped<DeviceRegistrar>();
        var inner = new MemoryTokenStore();
        return (new DeviceRegisteringTokenStore(inner, services.BuildServiceProvider(), NullLogger<DeviceRegisteringTokenStore>.Instance), inner, api);
    }

    // Nach der Anmeldung wird das Gerät angemeldet, beim Erneuern der Tokens nicht noch einmal.
    [Fact]
    public async Task Signing_in_registers_the_device_once_and_a_token_refresh_does_not()
    {
        var (store, inner, api) = Decorated();

        await store.SaveAsync(new AuthTokens("a1", "r1"));
        await store.SaveAsync(new AuthTokens("a2", "r2"));

        Assert.Equal("a2", inner.Tokens!.AccessToken);
        Assert.Single(api.Registered);
    }

    // Beim Abmelden wird das Gerät abgemeldet, solange die Anmeldung noch da ist; danach sind die Tokens weg.
    [Fact]
    public async Task Signing_out_removes_the_device_before_clearing_the_tokens()
    {
        var (store, inner, api) = Decorated();
        await store.SaveAsync(new AuthTokens("a1", "r1"));

        await store.ClearAsync();

        Assert.Equal(["inst"], api.Removed);
        Assert.Null(inner.Tokens);
    }

    [Fact]
    public async Task Clearing_without_anybody_signed_in_does_not_call_the_server()
    {
        var (store, _, api) = Decorated();

        await store.ClearAsync();

        Assert.Empty(api.Removed);
    }
}
