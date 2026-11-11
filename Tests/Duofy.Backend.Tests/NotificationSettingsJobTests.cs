using System.Net;
using System.Net.Http.Json;
using Duofy.Domain;
using Duofy.Schemas;
using Microsoft.EntityFrameworkCore;
using static Duofy.Api.Tests.HouseholdDriver;

namespace Duofy.Api.Tests;

/// <summary>Welche Benachrichtigungen der Job am Morgen und am Abend verschickt (Spec 118, A4 bis A9).</summary>
[Collection(ApiCollection.Name)]
public class NotificationSettingsJobTests(ApiFixture fixture)
{
    // 10. November 2026: 09:00 und 19:00 in Berlin (Winterzeit); Morning ist 07:30.
    private static readonly DateTimeOffset NineOClock = ReminderServer.Morning.AddMinutes(90);
    private static readonly DateTimeOffset Evening = ReminderServer.Morning.AddHours(11).AddMinutes(30);

    private static string ReminderPath(Guid id) => $"/api/v1/reminders/{id}";

    private static async Task<List<Reminder>> AllAsync(ReminderServer server) =>
        await server.Factory.WithDbAsync(db => db.Reminders.OrderBy(r => r.Day).ToListAsync());

    // A4
    [Fact]
    public async Task A_position_due_today_notifies_at_the_morning_and_again_in_the_evening()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var token = await server.DeviceAsync(anna);
        var position = await server.PositionAsync(anna, "Miete", dueDay: 10);

        await server.RunAtAsync(NineOClock);
        Assert.Single(server.Push.To(token));

        await server.RunAtAsync(Evening);
        Assert.Equal(2, server.Push.To(token).Count);
        Assert.Equal([ReminderKind.Due, ReminderKind.Evening], (await server.RemindersOfAsync(position)).Select(r => r.Kind).Order());

        await server.RunAtAsync(Evening.AddMinutes(1));
        Assert.Equal(2, server.Push.To(token).Count);
    }

    // A4
    [Fact]
    public async Task A_position_ticked_before_the_evening_gets_no_evening_notification()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var token = await server.DeviceAsync(anna);
        var position = await server.PositionAsync(anna, "Miete", dueDay: 10);
        await server.RunAtAsync(NineOClock);
        await server.TickAsync(position);

        await server.RunAtAsync(Evening);

        var evening = (await server.RemindersOfAsync(position)).Where(r => r.Kind == ReminderKind.Evening);
        Assert.Empty(evening);
        Assert.DoesNotContain(server.Push.To(token).Skip(1), s => s.Payload.Type == PushPayload.Due);
    }

    // A4
    [Fact]
    public async Task With_the_evening_switched_off_nothing_comes_in_the_evening()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var token = await server.DeviceAsync(anna);
        await server.SettingsAsync(anna, new TimeOnly(9, 0), change: s => s.EveningEnabled = false);
        await server.PositionAsync(anna, "Miete", dueDay: 10);

        await server.RunAtAsync(NineOClock);
        await server.RunAtAsync(Evening);

        Assert.Single(server.Push.To(token));
    }

    // A4
    [Fact]
    public async Task The_evening_time_is_the_persons_own()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var token = await server.DeviceAsync(anna);
        await server.SettingsAsync(anna, new TimeOnly(9, 0), change: s => s.EveningTime = new TimeOnly(20, 30));
        await server.PositionAsync(anna, "Miete", dueDay: 10);
        await server.RunAtAsync(NineOClock);

        await server.RunAtAsync(Evening);
        Assert.Single(server.Push.To(token));

        await server.RunAtAsync(Evening.AddMinutes(90));
        Assert.Equal(2, server.Push.To(token).Count);
    }

    // A5
    [Fact]
    public async Task The_advance_warning_comes_in_the_morning_the_chosen_number_of_days_before()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var token = await server.DeviceAsync(anna);
        var inTwo = await server.PositionAsync(anna, "Auto", dueDay: 12);
        var inThree = await server.PositionAsync(anna, "Strom", dueDay: 13);

        await server.RunAtAsync(NineOClock);

        Assert.Single(server.Push.To(token));
        Assert.Equal(ReminderKind.Advance, Assert.Single(await server.RemindersOfAsync(inTwo)).Kind);
        Assert.Empty(await server.RemindersOfAsync(inThree));

        await server.RunAtAsync(NineOClock.AddMinutes(1));
        Assert.Single(server.Push.To(token));
    }

    // A5
    [Fact]
    public async Task With_the_advance_switch_off_or_other_warning_days_the_warning_changes()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var ben = await server.PersonAsync("Ben");
        var annaToken = await server.DeviceAsync(anna);
        var benToken = await server.DeviceAsync(ben);
        await server.SettingsAsync(anna, new TimeOnly(9, 0), change: s => s.AdvanceWarning = false);
        await server.SettingsAsync(ben, new TimeOnly(9, 0), change: s => s.WarningDays = 3);
        await server.Client.FoundAsync(ben);
        await server.PositionAsync(anna, "Auto", dueDay: 12);
        await server.PositionAsync(ben, "Strom", dueDay: 13);

        await server.RunAtAsync(NineOClock);

        Assert.Empty(server.Push.To(annaToken));
        Assert.Single(server.Push.To(benToken));
    }

    // A7
    [Fact]
    public async Task An_unticked_position_past_its_day_is_overdue_every_morning_until_it_is_ticked()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var token = await server.DeviceAsync(anna);
        var position = await server.PositionAsync(anna, "Miete", dueDay: 8);

        await server.RunAtAsync(NineOClock);
        await server.RunAtAsync(NineOClock.AddMinutes(5));
        Assert.Single(server.Push.To(token));
        Assert.Equal(ReminderKind.Overdue, Assert.Single(await server.RemindersOfAsync(position)).Kind);

        await server.RunAtAsync(NineOClock.AddDays(1));
        Assert.Equal(2, server.Push.To(token).Count);
        await server.RunAtAsync(NineOClock.AddDays(2));
        Assert.Equal(3, server.Push.To(token).Count);

        await server.TickAsync(position);
        await server.RunAtAsync(NineOClock.AddDays(3));
        Assert.DoesNotContain(server.Push.To(token).Skip(3), s => s.Payload.Type == PushPayload.Due);
    }

    // A4, A7 (Entscheidung 1): offen heißt weniger gebucht als geplant und nicht durchlaufend, wie im Plan.
    [Fact]
    public async Task A_fully_booked_position_gets_no_evening_notification_but_a_partly_booked_one_does()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var token = await server.DeviceAsync(anna);
        var full = await server.PositionAsync(anna, "Miete", dueDay: 10);
        var part = await server.PositionAsync(anna, "Strom", dueDay: 10);
        var through = await server.PositionAsync(anna, "Durchlauf", dueDay: 10, passThrough: true);
        await server.RunAtAsync(NineOClock);
        await server.BookAsync(anna, full, 18.36m);
        await server.BookAsync(anna, part, 10m);

        await server.RunAtAsync(Evening);

        Assert.Empty((await server.RemindersOfAsync(full)).Where(r => r.Kind == ReminderKind.Evening));
        Assert.Single((await server.RemindersOfAsync(part)).Where(r => r.Kind == ReminderKind.Evening));
        Assert.Empty((await server.RemindersOfAsync(through)).Where(r => r.Kind == ReminderKind.Evening));
        Assert.Equal(4, server.Push.To(token).Count); // drei am Morgen, eine am Abend
    }

    // A7 (Entscheidung 1)
    [Fact]
    public async Task A_fully_booked_or_pass_through_position_past_its_day_is_not_overdue()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        await server.DeviceAsync(anna);
        var full = await server.PositionAsync(anna, "Miete", dueDay: 8);
        var part = await server.PositionAsync(anna, "Strom", dueDay: 8);
        var through = await server.PositionAsync(anna, "Durchlauf", dueDay: 8, passThrough: true);
        await server.BookAsync(anna, full, 18.36m);
        await server.BookAsync(anna, part, 10m);

        await server.RunAtAsync(NineOClock);

        Assert.Empty(await server.RemindersOfAsync(full));
        Assert.Equal(ReminderKind.Overdue, Assert.Single(await server.RemindersOfAsync(part)).Kind);
        Assert.Empty(await server.RemindersOfAsync(through));
    }

    // A7
    [Fact]
    public async Task With_the_overdue_switch_off_nothing_overdue_comes()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var token = await server.DeviceAsync(anna);
        await server.SettingsAsync(anna, new TimeOnly(9, 0), change: s => s.Overdue = false);
        await server.PositionAsync(anna, "Miete", dueDay: 8);

        await server.RunAtAsync(NineOClock);

        Assert.Empty(server.Push.To(token));
    }

    // A6
    [Fact]
    public async Task After_three_postpones_the_fetch_offers_no_later_and_a_fourth_is_refused()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var token = await server.DeviceAsync(anna);
        var position = await server.PositionAsync(anna, "Miete", dueDay: 10);
        await server.RunAtAsync(NineOClock);
        var reminder = Assert.Single(await server.RemindersOfAsync(position));

        var first = await FetchAsync(server, anna, reminder.Id);
        Assert.Equal((0, true, ReminderKinds.Due), (first.PostponeCount, first.LaterAllowed, first.Kind));

        for (var day = 1; day <= 3; day++)
        {
            Assert.Equal(HttpStatusCode.NoContent, (await server.Client.SendAsync(anna, HttpMethod.Post, ReminderPath(reminder.Id) + "/later")).StatusCode);
            await server.RunAtAsync(NineOClock.AddDays(day));
        }

        var third = await FetchAsync(server, anna, reminder.Id);
        Assert.Equal(3, third.PostponeCount);
        Assert.False(third.LaterAllowed);
        Assert.Equal(4, server.Push.To(token).Count);

        var fourth = await server.Client.SendAsync(anna, HttpMethod.Post, ReminderPath(reminder.Id) + "/later");
        Assert.Equal(HttpStatusCode.BadRequest, fourth.StatusCode);
        Assert.Equal(ReminderErrorCodes.ReminderLaterLimitReached, await fourth.ReadCodeAsync());
        Assert.Equal(3, (await FetchAsync(server, anna, reminder.Id)).PostponeCount);
    }

    // A6 (Fund 2): "Später" bei der Vorwarnung zählt nicht mit, und die Vorwarnung, die am Fälligkeitstag zurückkehrt, ersetzt "heute fällig".
    [Fact]
    public async Task Postponing_an_advance_warning_costs_no_postpone_and_gives_no_second_due_notification()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var token = await server.DeviceAsync(anna);
        var position = await server.PositionAsync(anna, "Miete", dueDay: 15);
        var thirteenth = NineOClock.AddDays(3);

        await server.RunAtAsync(thirteenth);
        var reminder = Assert.Single(await server.RemindersOfAsync(position));
        Assert.Equal(ReminderKind.Advance, reminder.Kind);
        (await server.Client.SendAsync(anna, HttpMethod.Post, ReminderPath(reminder.Id) + "/later")).EnsureSuccessStatusCode();

        await server.RunAtAsync(thirteenth.AddDays(1));
        (await server.Client.SendAsync(anna, HttpMethod.Post, ReminderPath(reminder.Id) + "/later")).EnsureSuccessStatusCode();

        await server.RunAtAsync(thirteenth.AddDays(2));

        Assert.Equal(3, server.Push.To(token).Count);
        Assert.Single(await server.RemindersOfAsync(position));
        var fetched = await FetchAsync(server, anna, reminder.Id);
        Assert.Equal((0, true), (fetched.PostponeCount, fetched.LaterAllowed));
        Assert.Empty(await server.Factory.WithDbAsync(db => db.PostponeCounts.Where(c => c.PositionId == position).ToListAsync()));
    }

    // A6: Der Plan nennt, wie oft ein Posten verschoben wurde.
    [Fact]
    public async Task The_plan_returns_how_often_a_position_was_postponed()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        await server.DeviceAsync(anna);
        var position = await server.PositionAsync(anna, "Miete", dueDay: 10);
        await server.PositionAsync(anna, "Strom", dueDay: 10);
        await server.RunAtAsync(NineOClock);
        var reminder = Assert.Single(await server.RemindersOfAsync(position));
        (await server.Client.SendAsync(anna, HttpMethod.Post, ReminderPath(reminder.Id) + "/later")).EnsureSuccessStatusCode();

        var plan = await (await server.Client.SendAsync(anna, HttpMethod.Get, "/api/v1/plans/2026/11")).Content.ReadFromJsonAsync<PlanResponse>();

        Assert.Equal(1, plan!.Positions.Single(p => p.Id == position).PostponeCount);
        Assert.Equal(0, plan.Positions.Single(p => p.Id != position).PostponeCount);
    }

    // Hinweis 10: Eine verschobene Erinnerung kommt nicht zurück, wenn ihr Schalter inzwischen aus ist; sie endet dann.
    [Fact]
    public async Task A_postponed_reminder_does_not_come_back_when_its_switch_is_off_meanwhile()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var token = await server.DeviceAsync(anna);
        var position = await server.PositionAsync(anna, "Auto", dueDay: 12);
        await server.RunAtAsync(NineOClock);
        var reminder = Assert.Single(await server.RemindersOfAsync(position));
        Assert.Equal(ReminderKind.Advance, reminder.Kind);
        (await server.Client.SendAsync(anna, HttpMethod.Post, ReminderPath(reminder.Id) + "/later")).EnsureSuccessStatusCode();
        await server.SettingsAsync(anna, new TimeOnly(9, 0), change: s => s.AdvanceWarning = false);

        await server.RunAtAsync(NineOClock.AddDays(1));

        Assert.Single(server.Push.To(token));
        Assert.Equal(ReminderState.Done, Assert.Single(await server.RemindersOfAsync(position)).State);
    }

    // A6 (Fund 6d): Auch der Haushaltsplan nennt, wie oft ein Posten verschoben wurde.
    [Fact]
    public async Task The_household_plan_returns_how_often_a_position_was_postponed()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        await server.DeviceAsync(anna);
        var position = await server.PositionAsync(anna, "Miete", dueDay: 10);
        var other = await server.PositionAsync(anna, "Strom", dueDay: 10);
        await server.RunAtAsync(NineOClock);
        var reminder = Assert.Single(await server.RemindersOfAsync(position));
        (await server.Client.SendAsync(anna, HttpMethod.Post, ReminderPath(reminder.Id) + "/later")).EnsureSuccessStatusCode();
        var household = await server.HouseholdOfAsync(anna);

        var plan = await (await server.Client.SendAsync(anna, HttpMethod.Get, $"/api/v1/households/{household}/plans/2026/11")).Content.ReadFromJsonAsync<HouseholdPlanResponse>();

        Assert.Equal(1, plan!.Positions.Single(p => p.Id == position).PostponeCount);
        Assert.Equal(0, plan.Positions.Single(p => p.Id == other).PostponeCount);
    }

    // A6, A7: Der Text sagt, ob der Posten heute fällig, bald fällig oder überfällig ist.
    [Fact]
    public async Task The_fetch_says_whether_the_position_is_due_soon_due_or_overdue()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        await server.DeviceAsync(anna);
        var soon = await server.PositionAsync(anna, "Auto", dueDay: 12);
        var overdue = await server.PositionAsync(anna, "Strom", dueDay: 8);
        await server.RunAtAsync(NineOClock);

        var advance = await FetchAsync(server, anna, Assert.Single(await server.RemindersOfAsync(soon)).Id);
        Assert.Equal((ReminderKinds.Advance, 2), (advance.Kind, advance.DaysUntilDue));
        var late = await FetchAsync(server, anna, Assert.Single(await server.RemindersOfAsync(overdue)).Id);
        Assert.Equal((ReminderKinds.Overdue, -2), (late.Kind, late.DaysUntilDue));
    }

    private static async Task<ReminderResponse> FetchAsync(ReminderServer server, Person person, Guid id)
    {
        var response = await server.Client.SendAsync(person, HttpMethod.Get, ReminderPath(id));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ReminderResponse>())!;
    }

    // A9
    [Fact]
    public async Task The_fetch_names_the_position_and_the_amount_while_the_signal_holds_only_id_and_type()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var token = await server.DeviceAsync(anna);
        var position = await server.PositionAsync(anna, "Miete", dueDay: 10);

        await server.RunAtAsync(NineOClock);

        var signal = Assert.Single(server.Push.To(token)).Payload;
        Assert.Equal([nameof(PushPayload.ReminderId), nameof(PushPayload.Type)], typeof(PushPayload).GetProperties().Select(p => p.Name).Order());
        var reminder = Assert.Single(await server.RemindersOfAsync(position));
        Assert.Equal(reminder.Id, signal.ReminderId);
        var text = await FetchAsync(server, anna, reminder.Id);
        Assert.Equal("Miete", text.Label);
        Assert.Equal(18.36m, text.AmountPlanned);
    }

    // A8
    [Fact]
    public async Task The_evening_summary_names_the_events_the_recipient_may_see_in_one_notification()
    {
        await using var server = ReminderServer.Start(fixture);
        var tom = await server.FounderAsync("Tom");
        var jasmin = await server.PersonAsync("Jasmin");
        var fremd = await server.PersonAsync("Fremd");
        await server.Client.JoinAsync(tom, jasmin);
        await server.Client.JoinAsync(tom, fremd);
        var token = await server.DeviceAsync(tom);
        await server.Client.GrantAsync(jasmin, tom, new UpdateGrantsRequest(Plan: AccessLevelKeys.View, Book: AccessLevelKeys.View));
        var household = await server.HouseholdOfAsync(tom);
        await server.ClearEventsAsync();
        var before = Evening.AddHours(-5);
        await server.EventAsync(household, HouseholdEventKind.Booked, jasmin, owner: jasmin, label: "REWE", amount: 23.40m, at: before);
        await server.EventAsync(household, HouseholdEventKind.Booked, jasmin, owner: jasmin, label: "Tankstelle", amount: 50m, at: before.AddMinutes(30));
        await server.EventAsync(household, HouseholdEventKind.Ticked, jasmin, owner: jasmin, label: "Miete", amount: 850m, at: before.AddHours(1));
        await server.EventAsync(household, HouseholdEventKind.Booked, fremd, owner: fremd, label: "Geheim", amount: 99m, at: before.AddHours(2));

        await server.RunAtAsync(Evening);

        var signal = Assert.Single(server.Push.To(token));
        Assert.Equal(PushPayload.Household, signal.Payload.Type);
        var text = await FetchAsync(server, tom, signal.Payload.ReminderId);
        Assert.Equal(ReminderKinds.Household, text.Kind);
        Assert.Equal(["REWE", "Tankstelle", "Miete"], text.Events!.Select(e => e.Label));
        Assert.All(text.Events!, e => Assert.Equal("Jasmin", e.ActorName));
        Assert.DoesNotContain(text.Events!, e => e.Label == "Geheim");

        await server.RunAtAsync(Evening.AddMinutes(1));
        Assert.Single(server.Push.To(token));
    }

    // A8
    [Fact]
    public async Task Without_events_or_with_the_household_switch_off_or_for_the_actor_there_is_no_summary()
    {
        await using var server = ReminderServer.Start(fixture);
        var tom = await server.FounderAsync("Tom");
        var jasmin = await server.PersonAsync("Jasmin");
        await server.Client.JoinAsync(tom, jasmin);
        var tomToken = await server.DeviceAsync(tom);
        var jasminToken = await server.DeviceAsync(jasmin);
        await server.SettingsAsync(tom, new TimeOnly(9, 0), change: s => s.Household = false);
        var household = await server.HouseholdOfAsync(tom);
        await server.Client.GrantAsync(jasmin, tom, new UpdateGrantsRequest(Plan: AccessLevelKeys.View));
        await server.ClearEventsAsync();

        await server.RunAtAsync(Evening);
        Assert.Empty(server.Push.To(tomToken));
        Assert.Empty(server.Push.To(jasminToken));

        await server.EventAsync(household, HouseholdEventKind.Ticked, jasmin, owner: jasmin, label: "Miete", at: Evening.AddHours(-1));
        await server.RunAtAsync(Evening.AddMinutes(1));

        Assert.Empty(server.Push.To(tomToken)); // Schalter aus
        Assert.Empty(server.Push.To(jasminToken)); // ihr eigenes Tun
    }

    // A8
    [Fact]
    public async Task A_grant_change_reaches_only_the_person_it_concerns_and_the_next_summary_starts_after_the_last()
    {
        await using var server = ReminderServer.Start(fixture);
        var tom = await server.FounderAsync("Tom");
        var jasmin = await server.PersonAsync("Jasmin");
        await server.Client.JoinAsync(tom, jasmin);
        var tomToken = await server.DeviceAsync(tom);
        var jasminToken = await server.DeviceAsync(jasmin);
        var household = await server.HouseholdOfAsync(tom);
        await server.ClearEventsAsync();
        await server.EventAsync(household, HouseholdEventKind.GrantChanged, tom, subject: jasmin, at: Evening.AddHours(-2));

        await server.RunAtAsync(Evening);

        Assert.Empty(server.Push.To(tomToken));
        var signal = Assert.Single(server.Push.To(jasminToken));
        var text = await FetchAsync(server, jasmin, signal.Payload.ReminderId);
        Assert.Equal("grantchanged", Assert.Single(text.Events!).Kind);

        // Am nächsten Abend ohne neues Ereignis kommt nichts; das alte steht nicht noch einmal da.
        await server.RunAtAsync(Evening.AddDays(1));
        Assert.Single(server.Push.To(jasminToken));
    }

    // A8 (Entscheidung 2): Die Zusammenfassung kommt nur zur Abendzeit (mit kurzem Nachholfenster); was danach geschieht, steht in der nächsten.
    [Fact]
    public async Task An_event_after_the_evening_time_goes_into_the_next_evenings_summary_not_into_a_late_one()
    {
        await using var server = ReminderServer.Start(fixture);
        var tom = await server.FounderAsync("Tom");
        var jasmin = await server.PersonAsync("Jasmin");
        await server.Client.JoinAsync(tom, jasmin);
        var token = await server.DeviceAsync(tom);
        await server.Client.GrantAsync(jasmin, tom, new UpdateGrantsRequest(Plan: AccessLevelKeys.View, Book: AccessLevelKeys.View));
        var household = await server.HouseholdOfAsync(tom);
        await server.ClearEventsAsync();
        var lateEvent = Evening.AddHours(4).AddMinutes(30); // 23:30
        await server.EventAsync(household, HouseholdEventKind.Booked, jasmin, owner: jasmin, label: "Spätkauf", amount: 5m, at: lateEvent);

        await server.RunAtAsync(lateEvent.AddMinutes(1)); // 23:31: nicht mehr die Zeit der Zusammenfassung
        Assert.Empty(server.Push.To(token));

        await server.RunAtAsync(Evening.AddDays(1));
        var signal = Assert.Single(server.Push.To(token));
        var text = await FetchAsync(server, tom, signal.Payload.ReminderId);
        Assert.Equal(["Spätkauf"], text.Events!.Select(e => e.Label));
    }

    // A8 (Entscheidung 2): Nach einem Ausfall holt der Job die Zusammenfassung noch kurz nach, deckt aber nur bis zur Abendzeit ab.
    [Fact]
    public async Task After_downtime_the_summary_is_caught_up_shortly_and_covers_only_until_the_evening_time()
    {
        await using var server = ReminderServer.Start(fixture);
        var tom = await server.FounderAsync("Tom");
        var jasmin = await server.PersonAsync("Jasmin");
        await server.Client.JoinAsync(tom, jasmin);
        var token = await server.DeviceAsync(tom);
        await server.Client.GrantAsync(jasmin, tom, new UpdateGrantsRequest(Plan: AccessLevelKeys.View, Book: AccessLevelKeys.View));
        var household = await server.HouseholdOfAsync(tom);
        await server.ClearEventsAsync();
        await server.EventAsync(household, HouseholdEventKind.Booked, jasmin, owner: jasmin, label: "Vorher", amount: 5m, at: Evening.AddMinutes(-30));
        await server.EventAsync(household, HouseholdEventKind.Booked, jasmin, owner: jasmin, label: "Nachher", amount: 6m, at: Evening.AddMinutes(10));

        await server.RunAtAsync(Evening.AddMinutes(20)); // der Server stand um 19:00

        var signal = Assert.Single(server.Push.To(token));
        Assert.Equal(["Vorher"], (await FetchAsync(server, tom, signal.Payload.ReminderId)).Events!.Select(e => e.Label));

        await server.RunAtAsync(Evening.AddDays(1));
        var next = Assert.Single(server.Push.To(token).Skip(1));
        Assert.Equal(["Nachher"], (await FetchAsync(server, tom, next.Payload.ReminderId)).Events!.Select(e => e.Label));
    }

    // A8 (Fund 6a): Ereignisse eines anderen Haushalts kommen nie in die Zusammenfassung.
    [Fact]
    public async Task Events_of_another_household_never_appear_in_the_summary()
    {
        await using var server = ReminderServer.Start(fixture);
        var tom = await server.FounderAsync("Tom");
        var jasmin = await server.PersonAsync("Jasmin");
        await server.Client.JoinAsync(tom, jasmin);
        var stranger = await server.FounderAsync("Fremd");
        var strangerMate = await server.PersonAsync("Mitbewohner");
        await server.Client.JoinAsync(stranger, strangerMate);
        var token = await server.DeviceAsync(tom);
        await server.Client.GrantAsync(jasmin, tom, new UpdateGrantsRequest(Book: AccessLevelKeys.View));
        var own = await server.HouseholdOfAsync(tom);
        var foreign = await server.HouseholdOfAsync(stranger);
        await server.ClearEventsAsync();
        var before = Evening.AddHours(-2);
        await server.EventAsync(foreign, HouseholdEventKind.Invited, strangerMate, subject: strangerMate, label: "Fremdes", at: before);
        await server.EventAsync(foreign, HouseholdEventKind.Booked, strangerMate, owner: strangerMate, label: "Fremdkauf", amount: 7m, at: before);

        await server.RunAtAsync(Evening);
        Assert.Empty(server.Push.To(token));

        await server.EventAsync(own, HouseholdEventKind.Booked, jasmin, owner: jasmin, label: "Eigenkauf", amount: 3m, at: before.AddMinutes(5));
        await server.RunAtAsync(Evening.AddMinutes(1));
        var signal = Assert.Single(server.Push.To(token));
        Assert.Equal(["Eigenkauf"], (await FetchAsync(server, tom, signal.Payload.ReminderId)).Events!.Select(e => e.Label));
    }

    // A8 (Fund 6b): "Abgehakt" liest nur, wer den Plan des Besitzers ansehen darf; die Buchung daneben darf er sehen.
    [Fact]
    public async Task A_tick_without_the_right_to_view_the_plan_does_not_appear()
    {
        await using var server = ReminderServer.Start(fixture);
        var tom = await server.FounderAsync("Tom");
        var jasmin = await server.PersonAsync("Jasmin");
        await server.Client.JoinAsync(tom, jasmin);
        var token = await server.DeviceAsync(tom);
        await server.Client.GrantAsync(jasmin, tom, new UpdateGrantsRequest(Book: AccessLevelKeys.View));
        var household = await server.HouseholdOfAsync(tom);
        await server.ClearEventsAsync();
        var before = Evening.AddHours(-2);
        await server.EventAsync(household, HouseholdEventKind.Ticked, jasmin, owner: jasmin, label: "Miete", amount: 850m, at: before);
        await server.EventAsync(household, HouseholdEventKind.Booked, jasmin, owner: jasmin, label: "REWE", amount: 23.40m, at: before.AddMinutes(5));

        await server.RunAtAsync(Evening);

        var signal = Assert.Single(server.Push.To(token));
        Assert.Equal(["REWE"], (await FetchAsync(server, tom, signal.Payload.ReminderId)).Events!.Select(e => e.Label));
    }

    // A8 (Fund 6c): Wird die Freigabe zwischen Ereignis und Abruf zurückgenommen, zeigt der Abruf das Ereignis nicht mehr.
    [Fact]
    public async Task A_grant_revoked_between_the_event_and_the_fetch_hides_the_event()
    {
        await using var server = ReminderServer.Start(fixture);
        var tom = await server.FounderAsync("Tom");
        var jasmin = await server.PersonAsync("Jasmin");
        await server.Client.JoinAsync(tom, jasmin);
        var token = await server.DeviceAsync(tom);
        await server.Client.GrantAsync(jasmin, tom, new UpdateGrantsRequest(Plan: AccessLevelKeys.View));
        var household = await server.HouseholdOfAsync(tom);
        await server.ClearEventsAsync();
        await server.EventAsync(household, HouseholdEventKind.Ticked, jasmin, owner: jasmin, label: "Miete", amount: 850m, at: Evening.AddHours(-2));
        await server.RunAtAsync(Evening);
        var signal = Assert.Single(server.Push.To(token));
        Assert.Equal(["Miete"], (await FetchAsync(server, tom, signal.Payload.ReminderId)).Events!.Select(e => e.Label));

        await server.Client.GrantAsync(jasmin, tom, new UpdateGrantsRequest(Plan: AccessLevelKeys.None));

        // Die Rücknahme selbst vermerkt noch ein Ereignis (Freigabe geändert); der Posten ist nicht mehr zu sehen.
        Assert.DoesNotContain((await FetchAsync(server, tom, signal.Payload.ReminderId)).Events!, e => e.Kind == "ticked");
    }
}
