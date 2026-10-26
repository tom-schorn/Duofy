using System.Net;
using Duofy.Schemas;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.ApiClient.Tests;

/// <summary>Die Clients für Buchungen und Kategorien: richtige Adressen, Inhalte und Fehlercodes.</summary>
public class BookingAndCategoryClientTests
{
    private static readonly Guid Giro = Guid.NewGuid();

    private static (FakeApi Api, ServiceProvider Provider, IServiceScope Scope) Start(Func<SeenRequest, HttpResponseMessage> respond)
    {
        var api = new FakeApi { Respond = respond };
        var provider = TestHost.Create(api, new InMemoryTokenStore { Tokens = new AuthTokens("access", "refresh") });
        return (api, provider, provider.CreateScope());
    }

    private static SaveBookingRequest NewBooking() => new(
        BookingKinds.Expense, Giro, null, new DateOnly(2026, 10, 21), 42.10m, null,
        [new SaveBookingLine(42.10m, PotKeys.Needs, Guid.NewGuid())]);

    [Fact]
    public async Task Month_list_asks_for_year_month_and_account()
    {
        var month = new BookingMonthResponse([], 0m, 0m);
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.OK, month));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IBookingApi>()
            .ListMonthAsync(2026, 10, Giro);

        Assert.True(result.Succeeded);
        var request = Assert.Single(api.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v1/bookings", request.Path);
        Assert.Equal($"?year=2026&month=10&accountId={Giro}", request.Query);
        Assert.Equal("Bearer access", request.Authorization);
    }

    [Fact]
    public async Task Month_list_without_an_account_leaves_the_filter_out()
    {
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.OK, new BookingMonthResponse([], 0m, 0m)));
        using var _p = provider;
        using var _s = scope;

        await scope.ServiceProvider.GetRequiredService<IBookingApi>().ListMonthAsync(2026, 9, accountId: null);

        Assert.Equal("?year=2026&month=9", Assert.Single(api.Requests).Query);
    }

    [Fact]
    public async Task Create_posts_the_booking_with_its_lines_and_returns_the_saved_one()
    {
        var saved = new BookingResponse(Guid.NewGuid(), BookingKinds.Expense, Giro, null, new DateOnly(2026, 10, 21), 42.10m, null,
            [new BookingLineResponse(Guid.NewGuid(), 42.10m, PotKeys.Needs, null)]);
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.Created, saved));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IBookingApi>().CreateAsync(NewBooking());

        Assert.Equal(saved.Id, result.Value!.Id);
        var request = Assert.Single(api.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v1/bookings", request.Path);
        Assert.Contains("\"lines\":[{\"amount\":42.10", request.Body);
        Assert.Contains("\"kind\":\"expense\"", request.Body);
    }

    [Fact]
    public async Task Create_returns_the_code_of_a_400_answer()
    {
        var (_, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.BadRequest, new ErrorResponse(ErrorCodes.LinesSumMismatch)));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IBookingApi>().CreateAsync(NewBooking());

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.LinesSumMismatch, result.ErrorCode);
    }

    [Fact]
    public async Task Update_puts_to_the_id_of_the_booking()
    {
        var id = Guid.NewGuid();
        var saved = new BookingResponse(id, BookingKinds.Expense, Giro, null, new DateOnly(2026, 10, 21), 42.10m, null, []);
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.OK, saved));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IBookingApi>().UpdateAsync(id, NewBooking());

        Assert.True(result.Succeeded);
        var request = Assert.Single(api.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal($"/api/v1/bookings/{id}", request.Path);
    }

    [Fact]
    public async Task Delete_sends_delete_and_succeeds_on_204()
    {
        var id = Guid.NewGuid();
        var (api, provider, scope) = Start(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IBookingApi>().DeleteAsync(id);

        Assert.True(result.Succeeded);
        var request = Assert.Single(api.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal($"/api/v1/bookings/{id}", request.Path);
    }

    [Fact]
    public async Task A_404_answer_is_reported_as_not_found()
    {
        var (_, provider, scope) = Start(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IBookingApi>().DeleteAsync(Guid.NewGuid());

        Assert.Equal(ClientErrorCodes.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Categories_are_listed_from_the_categories_address()
    {
        var all = new List<CategoryResponse> { new(Guid.NewGuid(), "household.groceries", "Lebensmittel", "household", PotKeys.Needs, true) };
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.OK, all));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<ICategoryApi>().ListAsync();

        Assert.Equal("Lebensmittel", Assert.Single(result.Value!).Name);
        Assert.Equal("/api/v1/categories", Assert.Single(api.Requests).Path);
    }

    [Fact]
    public async Task Category_create_update_and_delete_use_post_put_and_delete()
    {
        var id = Guid.NewGuid();
        var category = new CategoryResponse(id, null, "Friseur Kinder", "children", PotKeys.Needs, false);
        var (api, provider, scope) = Start(r => r.Method == HttpMethod.Delete
            ? new HttpResponseMessage(HttpStatusCode.NoContent)
            : FakeApi.Json(r.Method == HttpMethod.Post ? HttpStatusCode.Created : HttpStatusCode.OK, category));
        using var _p = provider;
        using var _s = scope;
        var categories = scope.ServiceProvider.GetRequiredService<ICategoryApi>();
        var body = new SaveCategoryRequest("Friseur Kinder", "children", PotKeys.Needs);

        var created = await categories.CreateAsync(body);
        var updated = await categories.UpdateAsync(id, body);
        var deleted = await categories.DeleteAsync(id);

        Assert.True(created.Succeeded && updated.Succeeded && deleted.Succeeded);
        Assert.Equal(
            [(HttpMethod.Post, "/api/v1/categories"), (HttpMethod.Put, $"/api/v1/categories/{id}"), (HttpMethod.Delete, $"/api/v1/categories/{id}")],
            api.Requests.Select(r => (r.Method, r.Path)));
    }
}
