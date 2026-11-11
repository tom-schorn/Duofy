using Duofy.Domain;
using Duofy.Schemas;
using Microsoft.EntityFrameworkCore;
using static Duofy.Api.Tests.HouseholdDriver;

namespace Duofy.Api.Tests;

/// <summary>Welche Erinnerungen der Job verschickt, an wen und wann (Spec 84, A1 bis A3, A5, A7, A8).</summary>
[Collection(ApiCollection.Name)]
public class ReminderJobTests(ApiFixture fixture)
{
    private static readonly DateTimeOffset Morning = ReminderServer.Morning;

    // A1
    [Fact]
    public async Task A_due_position_reaches_the_android_token_through_fcm_and_the_windows_channel_through_wns()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var android = await server.DeviceAsync(anna, DevicePlatform.Android);
        var windows = await server.DeviceAsync(anna, DevicePlatform.Windows);
        await server.PositionAsync(anna);

        await server.RunAtAsync(Morning.AddMinutes(90)); // 09:00, die Vorgabe

        var fcm = Assert.Single(server.Push.To(android));
        Assert.Equal("fcm", fcm.Provider);
        var wns = Assert.Single(server.Push.To(windows));
        Assert.Equal("wns", wns.Provider);
        Assert.Equal(PushPayload.Due, fcm.Payload.Type);
    }

    // A2
    [Fact]
    public async Task At_730_exactly_one_reminder_is_sent_at_the_chosen_minute_and_not_before_or_after()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var token = await server.DeviceAsync(anna);
        await server.SettingsAsync(anna, new TimeOnly(7, 30));
        var position = await server.PositionAsync(anna);

        await server.RunAtAsync(Morning.AddMinutes(-1));
        Assert.Empty(server.Push.To(token));

        await server.RunAtAsync(Morning);
        Assert.Single(server.Push.To(token));

        await server.RunAtAsync(Morning.AddMinutes(1));
        Assert.Single(server.Push.To(token));
        Assert.Single(await server.RemindersOfAsync(position));
    }

    // A2
    [Fact]
    public async Task With_reminders_off_nothing_is_sent()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var token = await server.DeviceAsync(anna);
        await server.SettingsAsync(anna, new TimeOnly(7, 30), enabled: false);
        await server.PositionAsync(anna);

        await server.RunAtAsync(Morning);

        Assert.Empty(server.Push.To(token));
    }

    [Fact]
    public async Task The_time_is_read_in_berlin_time_not_utc()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var token = await server.DeviceAsync(anna);
        await server.SettingsAsync(anna, new TimeOnly(7, 30));
        await server.PositionAsync(anna);

        // 10. November 07:30 Berlin sind 06:30 UTC; 07:30 UTC wären 08:30 in Berlin und damit zu spät, aber nicht zu früh
        await server.RunAtAsync(new DateTimeOffset(2026, 11, 10, 6, 29, 0, TimeSpan.Zero));
        Assert.Empty(server.Push.To(token));
        await server.RunAtAsync(new DateTimeOffset(2026, 11, 10, 6, 30, 0, TimeSpan.Zero));
        Assert.Single(server.Push.To(token));
    }

    // A3
    [Fact]
    public async Task Only_transfer_withdrawal_and_other_produce_a_reminder_not_direct_debit_or_standing_order()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var token = await server.DeviceAsync(anna);
        var ids = new Dictionary<PaymentMethod, Guid>();
        foreach (var method in Enum.GetValues<PaymentMethod>())
        {
            ids[method] = await server.PositionAsync(anna, label: method.ToString(), method: method);
        }

        await server.RunAtAsync(Morning.AddMinutes(90));

        Assert.Equal(3, server.Push.To(token).Count);
        foreach (var (method, id) in ids)
        {
            var expected = method is PaymentMethod.Transfer or PaymentMethod.Withdrawal or PaymentMethod.Other;
            Assert.Equal(expected, (await server.RemindersOfAsync(id)).Count == 1);
        }
    }

    [Fact]
    public async Task Income_ticked_and_not_yet_due_positions_produce_no_reminder()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var token = await server.DeviceAsync(anna);
        await server.PositionAsync(anna, "Gehalt", type: CommitmentType.Income);
        await server.PositionAsync(anna, "Schon bezahlt", done: true);
        await server.PositionAsync(anna, "Morgen", dueDay: 11);

        await server.RunAtAsync(Morning.AddMinutes(90));

        Assert.Empty(server.Push.To(token));
    }

    [Fact]
    public async Task A_person_without_a_device_gets_no_reminder_row()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var position = await server.PositionAsync(anna);

        await server.RunAtAsync(Morning.AddMinutes(90));

        Assert.Empty(await server.RemindersOfAsync(position));
    }

    // A5, zweiter Teil
    [Fact]
    public async Task A_postponed_reminder_comes_back_the_next_day_at_the_persons_time_and_not_today()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var token = await server.DeviceAsync(anna);
        await server.SettingsAsync(anna, new TimeOnly(7, 30));
        var position = await server.PositionAsync(anna);
        await server.RunAtAsync(Morning);
        var reminder = Assert.Single(await server.RemindersOfAsync(position));
        await server.Factory.WithDbAsync(async db =>
        {
            await db.Reminders.Where(r => r.Id == reminder.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.State, ReminderState.Later).SetProperty(r => r.Day, new DateOnly(2026, 11, 11)));
            return 0;
        });

        await server.RunAtAsync(Morning.AddHours(2));
        Assert.Single(server.Push.To(token));

        await server.RunAtAsync(Morning.AddDays(1).AddMinutes(-1));
        Assert.Single(server.Push.To(token));

        await server.RunAtAsync(Morning.AddDays(1));
        var sent = server.Push.To(token);
        Assert.Equal(2, sent.Count);
        Assert.Equal(reminder.Id, sent[1].Payload.ReminderId);
        Assert.Equal(ReminderState.Sent, Assert.Single(await server.RemindersOfAsync(position)).State);
    }

    // A7
    [Fact]
    public async Task Owner_and_member_with_the_reminder_grant_are_reminded_a_member_without_it_is_not_and_removing_it_stops_it()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var ben = await server.PersonAsync("Ben");
        var cleo = await server.PersonAsync("Cleo");
        await server.Client.JoinAsync(anna, ben);
        await server.Client.JoinAsync(anna, cleo);
        var annaToken = await server.DeviceAsync(anna);
        var benToken = await server.DeviceAsync(ben);
        var cleoToken = await server.DeviceAsync(cleo);
        await server.Client.GrantAsync(anna, ben, new UpdateGrantsRequest(Reminders: AccessLevelKeys.View));
        await server.PositionAsync(anna, "Erster", dueDay: 10);
        await server.PositionAsync(anna, "Zweiter", dueDay: 11);

        await server.RunAtAsync(Morning.AddMinutes(90));

        Assert.Single(server.Push.To(annaToken));
        Assert.Single(server.Push.To(benToken));
        Assert.Empty(server.Push.To(cleoToken));

        await server.Client.GrantAsync(anna, ben, new UpdateGrantsRequest(Reminders: AccessLevelKeys.None));
        await server.RunAtAsync(Morning.AddDays(1).AddMinutes(90));

        // Am 11. kommt für Anna "Zweiter" fällig und "Erster" überfällig (Spec 118); Ben bekommt nichts mehr.
        Assert.Equal(3, server.Push.To(annaToken).Count);
        Assert.Single(server.Push.To(benToken));
    }

    // A8
    [Fact]
    public async Task After_a_tick_a_withdraw_signal_goes_to_everyone_and_no_further_reminder_is_sent()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var ben = await server.PersonAsync("Ben");
        await server.Client.JoinAsync(anna, ben);
        var annaToken = await server.DeviceAsync(anna);
        var benToken = await server.DeviceAsync(ben);
        await server.Client.GrantAsync(anna, ben, new UpdateGrantsRequest(Reminders: AccessLevelKeys.View));
        var position = await server.PositionAsync(anna);
        await server.RunAtAsync(Morning.AddMinutes(90));
        await server.TickAsync(position);

        await server.RunAtAsync(Morning.AddMinutes(91));
        await server.RunAtAsync(Morning.AddMinutes(92));

        foreach (var token in new[] { annaToken, benToken })
        {
            var signals = server.Push.To(token);
            Assert.Equal([PushPayload.Due, PushPayload.Withdraw], signals.Select(s => s.Payload.Type));
        }
        Assert.All(await server.RemindersOfAsync(position), r => Assert.Equal(ReminderState.Done, r.State));
    }

    [Fact]
    public async Task The_signal_holds_only_the_reminder_id_and_the_type()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var token = await server.DeviceAsync(anna);
        var position = await server.PositionAsync(anna, label: "Geheimer Posten");
        await server.RunAtAsync(Morning.AddMinutes(90));

        var payload = Assert.Single(server.Push.To(token)).Payload;

        Assert.Equal(["ReminderId", "Type"], typeof(PushPayload).GetProperties().Select(p => p.Name).Order());
        Assert.Equal((await server.RemindersOfAsync(position)).Single().Id, payload.ReminderId);
    }
}
