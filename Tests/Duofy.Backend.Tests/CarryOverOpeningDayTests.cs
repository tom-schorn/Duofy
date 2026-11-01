using System.Net;
using System.Net.Http.Json;
using Duofy.Domain;
using Duofy.Schemas;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Api.Tests;

/// <summary>
/// Das automatisch angelegte erste Konto startet einen Tag vor der Registrierung (Entscheidung vom 1.11.2026), damit man
/// am ersten Tag buchen und in den Folgemonat übertragen kann. Für alle anderen Konten gilt weiter: Am Starttag lässt sich
/// nichts buchen (Spec 59, R6), und der Übertrag in den November bucht beide Buchungen auf den 31. Oktober (Spec 66, R9).
/// </summary>
[Collection(ApiCollection.Name)]
public class CarryOverOpeningDayTests(ApiFixture fixture)
{
    private WebApplicationFactory<Program> ApiAt(DateTimeOffset now)
    {
        var today = new MovableTime(now);
        return fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<TimeProvider>(today)));
    }

    private static async Task<AccountResponse> AccountAsync(HttpClient client, Person person, string name, DateOnly openedOn)
    {
        var created = await client.SendAsync(person, HttpMethod.Post, "/api/v1/accounts",
            new CreateAccountRequest(name, AccountTypes.Checking, 0m, openedOn));
        return (await created.Content.ReadFromJsonAsync<AccountResponse>())!;
    }

    private static SaveBookingRequest GroceriesOn(Guid accountId, DateOnly date) => new(
        BookingKinds.Expense, accountId, null, date, 12.00m, null,
        [new SaveBookingLine(12.00m, PotKeys.Needs, CategoryCatalog.IdFor("household.groceries"))]);

    [Theory]
    [InlineData(30)]
    [InlineData(31)]
    public async Task The_carry_over_into_november_works_with_a_first_account_of_october_30_or_31(int registeredOn)
    {
        await using var api = ApiAt(new DateTimeOffset(2026, 10, registeredOn, 10, 0, 0, TimeSpan.Zero));
        var client = api.CreateClient();
        var person = await client.PersonAsync("Anna");

        var answer = await client.SendAsync(person, HttpMethod.Post, "/api/v1/plans/2026/11/carry-over", new CreateCarryOverRequest(50.00m));

        Assert.Equal(HttpStatusCode.Created, answer.StatusCode);
        var dates = await fixture.Factory.WithDbAsync(db => db.Bookings.Where(b => b.OwnerId == person.Id).Select(b => b.PaymentDate).ToListAsync());
        Assert.Equal([new DateOnly(2026, 10, 31), new DateOnly(2026, 10, 31)], dates);
        var listed = await client.SendAsync(person, HttpMethod.Get, "/api/v1/accounts");
        Assert.Equal(0m, Assert.Single((await listed.Content.ReadFromJsonAsync<List<AccountResponse>>())!).Balance);
    }

    // Die verständliche Meldung vom 31.10. bleibt für jedes Konto, das später startet.
    [Fact]
    public async Task The_carry_over_answers_date_before_opening_for_an_account_that_starts_too_late()
    {
        await using var api = ApiAt(new DateTimeOffset(2026, 10, 31, 10, 0, 0, TimeSpan.Zero));
        var client = api.CreateClient();
        var person = await client.PersonAsync("Anna");
        var late = await AccountAsync(client, person, "Spät", new DateOnly(2026, 10, 31));

        var answer = await client.SendAsync(person, HttpMethod.Post, "/api/v1/plans/2026/11/carry-over",
            new CreateCarryOverRequest(50.00m, [new RealBalance(late.Id, 10.00m)]));

        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.DateBeforeOpening), (answer.StatusCode, await answer.ReadCodeAsync()));
    }

    // Halb eins in Berlin ist in UTC noch der Vortag: Das Datum richtet sich nach Europe/Berlin.
    [Fact]
    public async Task The_first_account_starts_the_day_before_registration_in_berlin_and_takes_a_booking_on_the_first_day()
    {
        await using var api = ApiAt(new DateTimeOffset(2026, 10, 30, 23, 30, 0, TimeSpan.Zero));
        var client = api.CreateClient();
        var person = await client.PersonAsync("Anna");

        var listed = await client.SendAsync(person, HttpMethod.Get, "/api/v1/accounts");
        var first = Assert.Single((await listed.Content.ReadFromJsonAsync<List<AccountResponse>>())!);
        var booking = await client.SendAsync(person, HttpMethod.Post, "/api/v1/bookings", GroceriesOn(first.Id, new DateOnly(2026, 10, 31)));

        Assert.Equal(new DateOnly(2026, 10, 30), first.OpeningDate);
        Assert.True(booking.StatusCode == HttpStatusCode.Created, await booking.Content.ReadAsStringAsync());
    }

    // Spec 59, R6 gilt für jedes andere Konto unverändert.
    [Fact]
    public async Task Another_account_takes_no_booking_on_its_opening_date()
    {
        await using var api = ApiAt(new DateTimeOffset(2026, 10, 31, 10, 0, 0, TimeSpan.Zero));
        var client = api.CreateClient();
        var person = await client.PersonAsync("Anna");
        var other = await AccountAsync(client, person, "Neu", new DateOnly(2026, 10, 31));

        var booking = await client.SendAsync(person, HttpMethod.Post, "/api/v1/bookings", GroceriesOn(other.Id, new DateOnly(2026, 10, 31)));

        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.DateBeforeOpening), (booking.StatusCode, await booking.ReadCodeAsync()));
    }
}
