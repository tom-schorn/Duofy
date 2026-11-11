using System.Net;
using System.Net.Http.Json;
using Duofy.Domain;
using Duofy.Schemas;
using Microsoft.EntityFrameworkCore;
using static Duofy.Api.Tests.HouseholdDriver;

namespace Duofy.Api.Tests;

/// <summary>Geräte, Einstellung und Erinnerungen über die API (Spec 84, A4 bis A6, A9).</summary>
[Collection(ApiCollection.Name)]
public class ReminderEndpointTests(ApiFixture fixture)
{
    private const string Devices = "/api/v1/me/devices/";
    private const string SettingsPath = "/api/v1/me/reminder-settings";

    private static string ReminderPath(Guid id) => $"/api/v1/reminders/{id}";

    private static async Task<Reminder> ReminderOfAsync(ReminderServer server, Guid positionId) =>
        Assert.Single(await server.RemindersOfAsync(positionId));

    // Eine Windows-Kanal-Adresse kommt vom Gerät; der Server sendet später dorthin und nimmt darum nur WNS-Adressen an.
    [Fact]
    public async Task A_windows_channel_must_be_a_wns_address()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.PersonAsync("Anna");

        var foreign = await server.Client.SendAsync(anna, HttpMethod.Put, Devices + "win", new RegisterDeviceRequest(DevicePlatforms.Windows, "https://evil.example/?token=x"));
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        Assert.Equal(ReminderErrorCodes.DeviceTokenInvalid, await foreign.ReadCodeAsync());

        (await server.Client.SendAsync(anna, HttpMethod.Put, Devices + "win", new RegisterDeviceRequest(DevicePlatforms.Windows, "https://db5p.notify.windows.com/?token=abc")))
            .EnsureSuccessStatusCode();
    }

    // S4: Meldet sich auf demselben Gerät eine andere Person an, gehört es ihr; bei der vorigen Person kommt nichts mehr an.
    [Fact]
    public async Task A_device_registered_by_another_person_moves_over_and_the_previous_person_loses_it()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.PersonAsync("Anna");
        var ben = await server.PersonAsync("Ben");
        (await server.Client.SendAsync(anna, HttpMethod.Put, Devices + "phone", new RegisterDeviceRequest(DevicePlatforms.Android, "token-a"))).EnsureSuccessStatusCode();
        (await server.Client.SendAsync(anna, HttpMethod.Put, Devices + "tablet", new RegisterDeviceRequest(DevicePlatforms.Android, "token-t"))).EnsureSuccessStatusCode();
        (await server.Client.SendAsync(anna, HttpMethod.Put, Devices + "other", new RegisterDeviceRequest(DevicePlatforms.Android, "token-o"))).EnsureSuccessStatusCode();

        // Dieselbe Installation unter Ben, und dasselbe Token unter einer neuen Installation.
        (await server.Client.SendAsync(ben, HttpMethod.Put, Devices + "phone", new RegisterDeviceRequest(DevicePlatforms.Android, "token-b"))).EnsureSuccessStatusCode();
        (await server.Client.SendAsync(ben, HttpMethod.Put, Devices + "new-install", new RegisterDeviceRequest(DevicePlatforms.Android, "token-t"))).EnsureSuccessStatusCode();

        var annas = await server.Factory.WithDbAsync(db => db.ReminderDevices.Where(d => d.UserId == anna.Id).Select(d => d.InstallationId).ToListAsync());
        var bens = await server.Factory.WithDbAsync(db => db.ReminderDevices.Where(d => d.UserId == ben.Id).Select(d => d.InstallationId).OrderBy(i => i).ToListAsync());
        Assert.Equal(["other"], annas);
        Assert.Equal(["new-install", "phone"], bens);
    }

    [Fact]
    public async Task A_device_is_registered_again_in_place_and_removed_only_by_its_own_person()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.PersonAsync("Anna");
        var ben = await server.PersonAsync("Ben");
        var installation = $"install-{Guid.NewGuid():N}";

        (await server.Client.SendAsync(anna, HttpMethod.Put, Devices + installation, new RegisterDeviceRequest(DevicePlatforms.Android, "token-1")))
            .EnsureSuccessStatusCode();
        (await server.Client.SendAsync(anna, HttpMethod.Put, Devices + installation, new RegisterDeviceRequest(DevicePlatforms.Android, "token-2")))
            .EnsureSuccessStatusCode();
        var rows = await server.Factory.WithDbAsync(db => db.ReminderDevices.Where(d => d.UserId == anna.Id).ToListAsync());
        Assert.Equal("token-2", Assert.Single(rows).PushToken);

        // Bens Abmeldung mit demselben Namen trifft Annas Gerät nicht.
        (await server.Client.SendAsync(ben, HttpMethod.Delete, Devices + installation)).EnsureSuccessStatusCode();
        Assert.Equal(1, await server.Factory.WithDbAsync(db => db.ReminderDevices.CountAsync(d => d.UserId == anna.Id)));

        (await server.Client.SendAsync(anna, HttpMethod.Delete, Devices + installation)).EnsureSuccessStatusCode();
        Assert.Equal(0, await server.Factory.WithDbAsync(db => db.ReminderDevices.CountAsync(d => d.UserId == anna.Id)));
    }

    [Fact]
    public async Task A_device_with_an_unknown_platform_an_empty_token_or_beyond_the_limit_is_refused()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.PersonAsync("Anna");

        var platform = await server.Client.SendAsync(anna, HttpMethod.Put, Devices + "a", new RegisterDeviceRequest("ios", "token"));
        Assert.Equal(HttpStatusCode.BadRequest, platform.StatusCode);
        Assert.Equal(ReminderErrorCodes.DevicePlatformInvalid, await platform.ReadCodeAsync());

        var token = await server.Client.SendAsync(anna, HttpMethod.Put, Devices + "a", new RegisterDeviceRequest(DevicePlatforms.Android, " "));
        Assert.Equal(ReminderErrorCodes.DeviceTokenInvalid, await token.ReadCodeAsync());

        for (var i = 0; i < ReminderLimits.MaxDevices; i++)
        {
            (await server.Client.SendAsync(anna, HttpMethod.Put, Devices + $"d{i}", new RegisterDeviceRequest(DevicePlatforms.Android, $"t{i}")))
                .EnsureSuccessStatusCode();
        }
        var tooMany = await server.Client.SendAsync(anna, HttpMethod.Put, Devices + "one-more", new RegisterDeviceRequest(DevicePlatforms.Android, "t"));
        Assert.Equal(ReminderErrorCodes.TooManyDevices, await tooMany.ReadCodeAsync());
    }

    [Fact]
    public async Task The_settings_start_with_reminders_on_at_nine_and_keep_what_the_person_sets()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.PersonAsync("Anna");

        var first = await server.Client.SendAsync(anna, HttpMethod.Get, SettingsPath);
        Assert.Equal(new ReminderSettingsResponse(true, "09:00"), await first.Content.ReadFromJsonAsync<ReminderSettingsResponse>());

        var put = await server.Client.SendAsync(anna, HttpMethod.Put, SettingsPath, new UpdateReminderSettingsRequest(Enabled: false, Time: "07:30"));
        Assert.Equal(new ReminderSettingsResponse(false, "07:30"), await put.Content.ReadFromJsonAsync<ReminderSettingsResponse>());

        var onlyTime = await server.Client.SendAsync(anna, HttpMethod.Put, SettingsPath, new UpdateReminderSettingsRequest(Time: "08:15"));
        Assert.Equal(new ReminderSettingsResponse(false, "08:15"), await onlyTime.Content.ReadFromJsonAsync<ReminderSettingsResponse>());

        var invalid = await server.Client.SendAsync(anna, HttpMethod.Put, SettingsPath, new UpdateReminderSettingsRequest(Time: "25:00"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(ReminderErrorCodes.ReminderTimeInvalid, await invalid.ReadCodeAsync());
    }

    // #117 / #118 A2: Der Android-Dialog liefert die Zeit nicht immer als "HH:mm"; auch "HH:mm:ss" gilt, die Sekunden fallen weg.
    [Theory]
    [InlineData("04:40", "04:40")]
    [InlineData("04:40:00", "04:40")]
    [InlineData("04:40:59", "04:40")]
    [InlineData("4:40", "04:40")]
    public async Task A_time_with_or_without_seconds_is_stored_without_seconds(string sent, string stored)
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.PersonAsync("Anna");

        var put = await server.Client.SendAsync(anna, HttpMethod.Put, SettingsPath, new UpdateReminderSettingsRequest(Time: sent));

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var settings = await server.Client.SendAsync(anna, HttpMethod.Get, SettingsPath);
        Assert.Equal(stored, (await settings.Content.ReadFromJsonAsync<ReminderSettingsResponse>())!.Time);
    }

    [Theory]
    [InlineData("")]
    [InlineData("25:00")]
    [InlineData("12:60")]
    [InlineData("abc")]
    public async Task An_empty_or_impossible_time_is_refused_and_the_stored_time_stays(string sent)
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.PersonAsync("Anna");
        await server.Client.SendAsync(anna, HttpMethod.Put, SettingsPath, new UpdateReminderSettingsRequest(Time: "07:30"));

        var put = await server.Client.SendAsync(anna, HttpMethod.Put, SettingsPath, new UpdateReminderSettingsRequest(Time: sent));

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
        Assert.Equal(ReminderErrorCodes.ReminderTimeInvalid, await put.ReadCodeAsync());
        var settings = await server.Client.SendAsync(anna, HttpMethod.Get, SettingsPath);
        Assert.Equal("07:30", (await settings.Content.ReadFromJsonAsync<ReminderSettingsResponse>())!.Time);
    }

    // A4
    [Fact]
    public async Task The_text_of_a_reminder_is_served_only_to_its_recipient()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var ben = await server.PersonAsync("Ben");
        await server.Client.JoinAsync(anna, ben);
        await server.Client.GrantAsync(anna, ben, new UpdateGrantsRequest(Reminders: AccessLevelKeys.View));
        var cleo = await server.PersonAsync("Cleo");
        await server.DeviceAsync(anna);
        await server.DeviceAsync(ben);
        var position = await server.PositionAsync(anna, "GEZ");
        await server.RunAtAsync(ReminderServer.Morning.AddMinutes(90));
        var forAnna = Assert.Single(await server.RemindersOfAsync(position), r => r.RecipientId == anna.Id);
        var forBen = Assert.Single(await server.RemindersOfAsync(position), r => r.RecipientId == ben.Id);

        var own = await server.Client.SendAsync(anna, HttpMethod.Get, ReminderPath(forAnna.Id));
        var text = await own.Content.ReadFromJsonAsync<ReminderResponse>();
        Assert.Equal(("GEZ", 18.36m, anna.Id, 2026, 11, 10), (text!.Label, text.AmountPlanned, text.OwnerId, text.Year, text.Month, text.DueDay));

        // Ben liest den Namen über seine eigene Erinnerung, nie über die von Anna.
        var bens = await server.Client.SendAsync(ben, HttpMethod.Get, ReminderPath(forBen.Id));
        Assert.Equal("GEZ", (await bens.Content.ReadFromJsonAsync<ReminderResponse>())!.Label);
        Assert.Equal(HttpStatusCode.NotFound, (await server.Client.SendAsync(ben, HttpMethod.Get, ReminderPath(forAnna.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await server.Client.SendAsync(cleo, HttpMethod.Get, ReminderPath(forAnna.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await server.Client.SendAsync(anna, HttpMethod.Get, ReminderPath(Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await server.Client.GetAsync(ReminderPath(forAnna.Id))).StatusCode);
    }

    // S3: Den Betrag sieht nur, wer den Plan des Besitzers ansehen darf.
    [Fact]
    public async Task The_planned_amount_is_served_only_to_someone_who_may_view_the_owners_plan()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var ben = await server.PersonAsync("Ben");
        await server.Client.JoinAsync(anna, ben);
        await server.Client.GrantAsync(anna, ben, new UpdateGrantsRequest(Reminders: AccessLevelKeys.View));
        await server.DeviceAsync(ben);
        var position = await server.PositionAsync(anna, "GEZ");
        await server.RunAtAsync(ReminderServer.Morning.AddMinutes(90));
        var forBen = Assert.Single(await server.RemindersOfAsync(position), r => r.RecipientId == ben.Id);

        var withoutPlan = await (await server.Client.SendAsync(ben, HttpMethod.Get, ReminderPath(forBen.Id))).Content.ReadFromJsonAsync<ReminderResponse>();
        Assert.Equal("GEZ", withoutPlan!.Label);
        Assert.Null(withoutPlan.AmountPlanned);

        await server.Client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.View, Reminders: AccessLevelKeys.View));
        var withPlan = await (await server.Client.SendAsync(ben, HttpMethod.Get, ReminderPath(forBen.Id))).Content.ReadFromJsonAsync<ReminderResponse>();
        Assert.Equal(18.36m, withPlan!.AmountPlanned);
    }

    [Fact]
    public async Task A_member_who_lost_the_grant_no_longer_gets_the_text()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var ben = await server.PersonAsync("Ben");
        await server.Client.JoinAsync(anna, ben);
        await server.Client.GrantAsync(anna, ben, new UpdateGrantsRequest(Reminders: AccessLevelKeys.View));
        await server.DeviceAsync(ben);
        var position = await server.PositionAsync(anna);
        await server.RunAtAsync(ReminderServer.Morning.AddMinutes(90));
        var reminder = await ReminderOfAsync(server, position);
        await server.Client.GrantAsync(anna, ben, new UpdateGrantsRequest(Reminders: AccessLevelKeys.None));

        Assert.Equal(HttpStatusCode.NotFound, (await server.Client.SendAsync(ben, HttpMethod.Get, ReminderPath(reminder.Id))).StatusCode);
    }

    // A5
    [Fact]
    public async Task Later_moves_the_reminder_to_tomorrow_and_only_for_its_recipient()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var cleo = await server.PersonAsync("Cleo");
        await server.DeviceAsync(anna);
        var position = await server.PositionAsync(anna);
        await server.RunAtAsync(ReminderServer.Morning.AddMinutes(90));
        var reminder = await ReminderOfAsync(server, position);

        Assert.Equal(HttpStatusCode.NotFound, (await server.Client.SendAsync(cleo, HttpMethod.Post, ReminderPath(reminder.Id) + "/later")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await server.Client.SendAsync(anna, HttpMethod.Post, ReminderPath(reminder.Id) + "/later")).StatusCode);

        var moved = await ReminderOfAsync(server, position);
        Assert.Equal((ReminderState.Later, new DateOnly(2026, 11, 11)), (moved.State, moved.Day));
    }

    // A6
    [Fact]
    public async Task A_recipient_without_the_right_to_tick_is_refused_with_the_existing_code()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var ben = await server.PersonAsync("Ben");
        await server.Client.JoinAsync(anna, ben);
        await server.Client.GrantAsync(anna, ben, new UpdateGrantsRequest(
            Plan: AccessLevelKeys.View, Book: AccessLevelKeys.View, Accounts: AccessLevelKeys.View, Reminders: AccessLevelKeys.View));
        var position = await server.PositionAsync(anna);

        var tick = await server.Client.SendAsync(ben, HttpMethod.Post, $"/api/v1/plans/2026/11/positions/{position}/tick?owner={anna.Id}", new { });

        Assert.Equal(HttpStatusCode.Forbidden, tick.StatusCode);
        Assert.Equal(ErrorCodes.NoEditGranted, await tick.ReadCodeAsync());
    }

    [Fact]
    public async Task The_reminder_grant_allows_only_none_and_view()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var ben = await server.PersonAsync("Ben");
        await server.Client.JoinAsync(anna, ben);
        var household = await server.Client.MineAsync(anna);

        var create = await server.Client.SendAsync(anna, HttpMethod.Put, Grants(household.Id, ben.Id), new UpdateGrantsRequest(Reminders: AccessLevelKeys.Create));

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
    }

    [Fact]
    public async Task The_reminder_grant_opens_no_data_of_the_owner()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var ben = await server.PersonAsync("Ben");
        await server.Client.JoinAsync(anna, ben);
        await server.Client.GrantAsync(anna, ben, new UpdateGrantsRequest(Reminders: AccessLevelKeys.View));
        await server.PositionAsync(anna);

        var plan = await server.Client.SendAsync(ben, HttpMethod.Get, $"/api/v1/plans/2026/11?owner={anna.Id}");

        Assert.Equal(HttpStatusCode.NotFound, plan.StatusCode);
    }

    // A9
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Without_the_switch_or_on_a_self_hosted_server_the_endpoints_answer_404_and_the_job_sends_nothing(bool hosted, bool enabled)
    {
        await using var server = ReminderServer.Start(fixture, enabled: enabled, hosted: hosted);
        var anna = hosted ? await server.FounderAsync("Anna") : await server.Client.PersonAsync("Anna");
        var token = await server.DeviceAsync(anna);
        await server.PositionAsync(anna);
        var anyId = Guid.NewGuid();

        foreach (var (method, path, body) in new (HttpMethod, string, object?)[]
        {
            (HttpMethod.Put, Devices + "x", new RegisterDeviceRequest(DevicePlatforms.Android, "t")),
            (HttpMethod.Delete, Devices + "x", null),
            (HttpMethod.Get, SettingsPath, null),
            (HttpMethod.Put, SettingsPath, new UpdateReminderSettingsRequest(Enabled: true)),
            (HttpMethod.Get, ReminderPath(anyId), null),
            (HttpMethod.Post, ReminderPath(anyId) + "/later", null),
        })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await server.Client.SendAsync(anna, method, path, body)).StatusCode);
        }

        await server.RunAtAsync(ReminderServer.Morning.AddMinutes(90));
        Assert.Empty(server.Push.To(token));
    }
}
