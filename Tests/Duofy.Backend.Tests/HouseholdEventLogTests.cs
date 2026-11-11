using System.Net.Http.Json;
using Duofy.Domain;
using Duofy.Schemas;
using Microsoft.EntityFrameworkCore;
using static Duofy.Api.Tests.HouseholdDriver;

namespace Duofy.Api.Tests;

/// <summary>Wo das Protokoll der Haushaltsereignisse gefüllt wird (Spec 118, R8): beim Einladen, Beitreten, Freigeben, Buchen und Abhaken.</summary>
[Collection(ApiCollection.Name)]
public class HouseholdEventLogTests(ApiFixture fixture)
{
    private static readonly Guid Groceries = CategoryCatalog.IdFor("household.groceries");

    // Die Tests teilen sich die Datenbank; jeder liest nur die Ereignisse des eigenen Haushalts.
    private static async Task<List<HouseholdEvent>> EventsAsync(ReminderServer server, Person person)
    {
        var household = await server.HouseholdOfAsync(person);
        return await server.Factory.WithDbAsync(db => db.HouseholdEvents.Where(e => e.HouseholdId == household).OrderBy(e => e.OccurredAt).ToListAsync());
    }

    [Fact]
    public async Task Inviting_joining_and_a_grant_change_are_logged()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var ben = await server.PersonAsync("Ben");

        await server.Client.InviteAsync(anna);
        await server.Client.JoinAsync(anna, ben);
        await server.Client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.View));

        var events = await EventsAsync(server, anna);
        Assert.Contains(events, e => e.Kind == HouseholdEventKind.Invited && e.ActorId == anna.Id);
        Assert.Contains(events, e => e.Kind == HouseholdEventKind.Joined && e.ActorId == ben.Id && e.SubjectId == ben.Id);
        Assert.Contains(events, e => e.Kind == HouseholdEventKind.GrantChanged && e.ActorId == anna.Id && e.SubjectId == ben.Id);
    }

    [Fact]
    public async Task A_booking_by_a_member_and_a_tick_are_logged_with_name_and_amount()
    {
        await using var server = ReminderServer.Start(fixture);
        var anna = await server.FounderAsync("Anna");
        var ben = await server.PersonAsync("Ben");
        await server.Client.JoinAsync(anna, ben);
        var account = (await (await server.Client.SendAsync(anna, HttpMethod.Post, "/api/v1/accounts",
            new CreateAccountRequest("Giro", AccountTypes.Checking, 500m, new DateOnly(2026, 1, 1)))).Content.ReadFromJsonAsync<AccountResponse>())!;
        await server.Client.GrantAsync(anna, ben, new UpdateGrantsRequest(
            Plan: AccessLevelKeys.View, Book: AccessLevelKeys.Edit, Accounts: AccessLevelKeys.View));

        var booking = await server.Client.SendAsync(ben, HttpMethod.Post, "/api/v1/bookings",
            new SaveBookingRequest(BookingKinds.Expense, account.Id, null, new DateOnly(2026, 11, 3), 12.50m, "REWE", [new SaveBookingLine(12.50m, PotKeys.Needs, Groceries)], OwnerId: anna.Id));
        Assert.True(booking.IsSuccessStatusCode, await booking.Content.ReadAsStringAsync());
        var created = await server.Client.SendAsync(anna, HttpMethod.Post, "/api/v1/plans/2026/11/positions", new CreatePositionRequest("Miete", 850m, 10, Groceries, PotKeys.Needs));
        var position = (await created.Content.ReadFromJsonAsync<PlanPositionResponse>())!.Id;
        var tick = await server.Client.SendAsync(ben, HttpMethod.Post, $"/api/v1/plans/2026/11/positions/{position}/tick?owner={anna.Id}", new TickPositionRequest(AccountId: account.Id));
        Assert.True(tick.IsSuccessStatusCode, await tick.Content.ReadAsStringAsync());

        var events = await EventsAsync(server, anna);
        var booked = Assert.Single(events, e => e.Kind == HouseholdEventKind.Booked);
        Assert.Equal((ben.Id, anna.Id, 12.50m), (booked.ActorId, booked.OwnerId, booked.Amount));
        var ticked = Assert.Single(events, e => e.Kind == HouseholdEventKind.Ticked);
        Assert.Equal((ben.Id, anna.Id, "Miete", 850m), (ticked.ActorId, ticked.OwnerId, ticked.Label, ticked.Amount));
        // Das Abhaken legt selbst eine Buchung an, ist aber nur ein Ereignis.
        Assert.Equal(2, events.Count(e => e.Kind is HouseholdEventKind.Booked or HouseholdEventKind.Ticked));
    }
}
