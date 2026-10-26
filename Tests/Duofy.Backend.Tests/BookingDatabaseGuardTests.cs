using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>
/// Die Datenbank schützt die Buchungen auch an der API vorbei: Ein Konto oder eine Kategorie
/// unter einer Buchung lässt sich nicht löschen, eine Person mit allem, was ihr gehört, schon.
/// </summary>
[Collection(ApiCollection.Name)]
public class BookingDatabaseGuardTests(ApiFixture fixture)
{
    private readonly HttpClient client = fixture.Factory.CreateClient();

    /// <summary>Eine Person mit Konto, eigener Kategorie und einer Buchung darauf.</summary>
    private async Task<(Guid Account, Guid Category)> PersonWithBookingAsync()
    {
        var (_, tokens) = await client.SignUpAsync();
        var account = await (await client.SendAsync(Authorized(HttpMethod.Post, "/api/v1/accounts", tokens.AccessToken,
            new CreateAccountRequest("Giro", AccountTypes.Checking, 100m, new DateOnly(2026, 10, 1))))).Content.ReadFromJsonAsync<AccountResponse>();
        var category = await (await client.SendAsync(Authorized(HttpMethod.Post, "/api/v1/categories", tokens.AccessToken,
            new SaveCategoryRequest("Eigen", "leisure", PotKeys.Wants)))).Content.ReadFromJsonAsync<CategoryResponse>();
        var booking = await client.SendAsync(Authorized(HttpMethod.Post, "/api/v1/bookings", tokens.AccessToken,
            new SaveBookingRequest(BookingKinds.Expense, account!.Id, null, new DateOnly(2026, 10, 5), 1m, null,
                [new SaveBookingLine(1m, PotKeys.Wants, category!.Id)])));
        Assert.Equal(HttpStatusCode.Created, booking.StatusCode);
        return (account.Id, category.Id);
    }

    private Task<string?> SqlErrorAsync(string sql, Guid id) => fixture.Factory.WithDbAsync(async db =>
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(sql, id);
            return (string?)null;
        }
        catch (PostgresException e)
        {
            return e.SqlState;
        }
    });

    [Fact]
    public async Task An_account_under_a_booking_cannot_be_deleted()
    {
        var (account, _) = await PersonWithBookingAsync();

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, await SqlErrorAsync("DELETE FROM accounts WHERE id = {0}", account));
    }

    [Fact]
    public async Task A_category_under_a_booking_line_cannot_be_deleted()
    {
        var (_, category) = await PersonWithBookingAsync();

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, await SqlErrorAsync("DELETE FROM categories WHERE id = {0}", category));
    }

    [Fact]
    public async Task Deleting_the_person_takes_accounts_categories_and_bookings_along()
    {
        var (account, _) = await PersonWithBookingAsync();

        Assert.Null(await SqlErrorAsync("DELETE FROM users WHERE id = (SELECT owner_id FROM accounts WHERE id = {0})", account));
        Assert.Equal(0, await fixture.Factory.WithDbAsync(db => db.Bookings.CountAsync(b => b.AccountId == account)));
    }
}
