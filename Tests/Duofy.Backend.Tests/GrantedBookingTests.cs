using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using Duofy.Domain;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api.Tests;

/// <summary>Buchungen einer anderen Person mit Freigabe (Spec 68, R4, R5, R9).</summary>
[Collection(ApiCollection.Name)]
public class GrantedBookingTests
{
    private const string Bookings = "/api/v1/bookings";

    private static readonly DateOnly February2 = new(2026, 2, 2);

    private static readonly Guid Groceries = CategoryCatalog.IdFor("household.groceries");

    private readonly ApiFixture fixture;
    private readonly HttpClient client;

    public GrantedBookingTests(ApiFixture fixture)
    {
        this.fixture = fixture;
        client = fixture.Factory.CreateClient();
    }

    private static SaveBookingRequest Expense(Guid account, decimal amount, Guid? ownerId = null) =>
        new(BookingKinds.Expense, account, null, February2, amount, null, [new SaveBookingLine(amount, PotKeys.Needs, Groceries)], OwnerId: ownerId);

    private async Task<Guid> GiroOfAsync(Person owner)
    {
        var response = await client.SendAsync(owner, HttpMethod.Post, "/api/v1/accounts",
            new CreateAccountRequest("Giro", AccountTypes.Checking, 100m, new DateOnly(2026, 1, 1)));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountResponse>())!.Id;
    }

    private async Task<BookingResponse> BookAsync(Person owner, Guid account, decimal amount)
    {
        var response = await client.SendAsync(owner, HttpMethod.Post, Bookings, Expense(account, amount));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BookingResponse>())!;
    }

    private Task<decimal?> AmountOfAsync(Guid bookingId) =>
        fixture.Factory.WithDbAsync(db => db.Bookings.Where(b => b.Id == bookingId).Select(b => (decimal?)b.Amount).SingleOrDefaultAsync());

    // A5
    [Fact]
    public async Task With_edit_ben_changes_annas_booking_but_cannot_delete_it()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var giro = await GiroOfAsync(anna);
        var booking = await BookAsync(anna, giro, 40.00m);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Book: AccessLevelKeys.Edit, Accounts: AccessLevelKeys.View));

        var change = await client.SendAsync(ben, HttpMethod.Put, $"{Bookings}/{booking.Id}", Expense(giro, 45.50m));
        var delete = await client.SendAsync(ben, HttpMethod.Delete, $"{Bookings}/{booking.Id}");

        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        Assert.Equal(45.50m, await AmountOfAsync(booking.Id));
        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NoDeleteGranted), (delete.StatusCode, await delete.ReadCodeAsync()));
        Assert.Equal(anna.Id, await fixture.Factory.WithDbAsync(db => db.Bookings.Where(b => b.Id == booking.Id).Select(b => b.OwnerId).SingleAsync()));
    }

    // A6
    [Fact]
    public async Task Without_seeing_annas_accounts_ben_cannot_book_on_them()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var giro = await GiroOfAsync(anna);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Book: AccessLevelKeys.Create));

        var create = await client.SendAsync(ben, HttpMethod.Post, Bookings, Expense(giro, 12.00m, anna.Id));

        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
        Assert.False(await fixture.Factory.WithDbAsync(db => db.Bookings.AnyAsync(b => b.OwnerId == anna.Id)));
    }

    // R4: Mit Anlegen und Konten Ansehen bucht Ben für Anna; die Buchung gehört ihr und steht in ihrem Monat.
    [Fact]
    public async Task With_create_and_accounts_view_ben_books_for_anna_and_she_and_he_see_it()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var giro = await GiroOfAsync(anna);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Book: AccessLevelKeys.Create, Accounts: AccessLevelKeys.View));

        var create = await client.SendAsync(ben, HttpMethod.Post, Bookings, Expense(giro, 12.00m, anna.Id));
        var bensMonth = await client.SendAsync(ben, HttpMethod.Get, $"{Bookings}?year=2026&month=2&owner={anna.Id}");
        var bensOwn = await client.SendAsync(ben, HttpMethod.Get, $"{Bookings}?year=2026&month=2");
        var annasMonth = await client.SendAsync(anna, HttpMethod.Get, $"{Bookings}?year=2026&month=2");

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = (await create.Content.ReadFromJsonAsync<BookingResponse>())!;
        Assert.Equal(created.Id, Assert.Single((await bensMonth.Content.ReadFromJsonAsync<BookingMonthResponse>())!.Bookings).Id);
        Assert.Empty((await bensOwn.Content.ReadFromJsonAsync<BookingMonthResponse>())!.Bookings);
        Assert.Equal(created.Id, Assert.Single((await annasMonth.Content.ReadFromJsonAsync<BookingMonthResponse>())!.Bookings).Id);
    }

    // R4: Anlegen ohne die Stufe antwortet mit dem fehlenden Schritt; Ansehen fehlt ganz: 404.
    [Fact]
    public async Task Booking_for_anna_with_view_only_is_refused_and_without_any_grant_not_found()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var giro = await GiroOfAsync(anna);

        var without = await client.SendAsync(ben, HttpMethod.Post, Bookings, Expense(giro, 12.00m, anna.Id));
        var listWithout = await client.SendAsync(ben, HttpMethod.Get, $"{Bookings}?year=2026&month=2&owner={anna.Id}");
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Book: AccessLevelKeys.View, Accounts: AccessLevelKeys.View));
        var withView = await client.SendAsync(ben, HttpMethod.Post, Bookings, Expense(giro, 12.00m, anna.Id));

        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.OwnerNotFound), (without.StatusCode, await without.ReadCodeAsync()));
        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.OwnerNotFound), (listWithout.StatusCode, await listWithout.ReadCodeAsync()));
        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NoCreateGranted), (withView.StatusCode, await withView.ReadCodeAsync()));
        Assert.False(await fixture.Factory.WithDbAsync(db => db.Bookings.AnyAsync(b => b.OwnerId == anna.Id)));
    }

    // A9
    [Fact]
    public async Task Claras_booking_is_not_found_for_ben_and_stays_as_it_is()
    {
        var (_, ben) = await client.AnnaAndBenAsync();
        var clara = await client.FounderAsync("Clara");
        var giro = await GiroOfAsync(clara);
        var booking = await BookAsync(clara, giro, 40.00m);

        var change = await client.SendAsync(ben, HttpMethod.Put, $"{Bookings}/{booking.Id}", Expense(giro, 1.00m));
        var delete = await client.SendAsync(ben, HttpMethod.Delete, $"{Bookings}/{booking.Id}");

        Assert.Equal(HttpStatusCode.NotFound, change.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal(40.00m, await AmountOfAsync(booking.Id));
    }
}
