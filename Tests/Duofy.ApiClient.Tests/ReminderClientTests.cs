using System.Net;
using Duofy.Schemas;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.ApiClient.Tests;

/// <summary>Der Client für die Erinnerungen (Spec 84): richtige Adressen und Inhalte, 404 als NotFound.</summary>
public class ReminderClientTests
{
    private static (FakeApi Api, ServiceProvider Provider, IServiceScope Scope) Start(Func<SeenRequest, HttpResponseMessage> respond)
    {
        var api = new FakeApi { Respond = respond };
        var provider = TestHost.Create(api, new InMemoryTokenStore { Tokens = new AuthTokens("access", "refresh") });
        return (api, provider, provider.CreateScope());
    }

    [Fact]
    public async Task Registering_a_device_puts_platform_and_token_under_the_installation_id()
    {
        var (api, provider, scope) = Start(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IReminderApi>()
            .RegisterDeviceAsync("install-1", DevicePlatforms.Android, "token-1");

        Assert.True(result.Succeeded);
        var request = Assert.Single(api.Requests);
        Assert.Equal((HttpMethod.Put, "/api/v1/me/devices/install-1", "Bearer access"), (request.Method, request.Path, request.Authorization));
        Assert.Contains("\"platform\":\"android\"", request.Body);
        Assert.Contains("\"pushToken\":\"token-1\"", request.Body);
    }

    [Fact]
    public async Task Removing_a_device_deletes_it_by_installation_id()
    {
        var (api, provider, scope) = Start(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        using var _p = provider;
        using var _s = scope;

        await scope.ServiceProvider.GetRequiredService<IReminderApi>().RemoveDeviceAsync("abc");

        var request = Assert.Single(api.Requests);
        Assert.Equal((HttpMethod.Delete, "/api/v1/me/devices/abc"), (request.Method, request.Path));
    }

    [Fact]
    public async Task Settings_are_read_and_changed_and_a_404_means_there_are_no_reminders()
    {
        var (api, provider, scope) = Start(request => request.Method == HttpMethod.Get
            ? FakeApi.Json(HttpStatusCode.OK, new ReminderSettingsResponse(true, true, true, true, "09:00", true, "19:00", 2))
            : FakeApi.Json(HttpStatusCode.OK, new ReminderSettingsResponse(false, true, true, false, "07:30", false, "20:15", 5)));
        using var _p = provider;
        using var _s = scope;
        var reminders = scope.ServiceProvider.GetRequiredService<IReminderApi>();

        var read = await reminders.GetSettingsAsync();
        var changed = await reminders.UpdateSettingsAsync(new UpdateReminderSettingsRequest(DueToday: false, Household: false, MorningTime: "07:30", EveningEnabled: false, EveningTime: "20:15", WarningDays: 5));

        Assert.Equal(new ReminderSettingsResponse(true, true, true, true, "09:00", true, "19:00", 2), read.Value);
        Assert.Equal(new ReminderSettingsResponse(false, true, true, false, "07:30", false, "20:15", 5), changed.Value);
        Assert.Equal([(HttpMethod.Get, "/api/v1/me/reminder-settings"), (HttpMethod.Put, "/api/v1/me/reminder-settings")],
            api.Requests.Select(r => (r.Method, r.Path)));

        api.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound);
        Assert.Equal(ClientErrorCodes.NotFound, (await reminders.GetSettingsAsync()).ErrorCode);
    }

    [Fact]
    public async Task A_reminder_is_fetched_and_postponed_by_its_id()
    {
        var id = Guid.NewGuid();
        var body = new ReminderResponse(id, Guid.NewGuid(), Guid.NewGuid(), "GEZ", 55.08m, 2026, 11, 4, "sent");
        var (api, provider, scope) = Start(request => request.Method == HttpMethod.Get
            ? FakeApi.Json(HttpStatusCode.OK, body)
            : new HttpResponseMessage(HttpStatusCode.NoContent));
        using var _p = provider;
        using var _s = scope;
        var reminders = scope.ServiceProvider.GetRequiredService<IReminderApi>();

        var fetched = await reminders.GetAsync(id);
        var later = await reminders.LaterAsync(id);

        Assert.Equal("GEZ", fetched.Value!.Label);
        Assert.True(later.Succeeded);
        Assert.Equal([(HttpMethod.Get, $"/api/v1/reminders/{id}"), (HttpMethod.Post, $"/api/v1/reminders/{id}/later")],
            api.Requests.Select(r => (r.Method, r.Path)));
    }
}
