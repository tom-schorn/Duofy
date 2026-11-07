using System.Net.Http.Json;
using Duofy.Schemas;
using Duofy.Domain;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Api.Tests;

/// <summary>
/// Spec 68, A10: Jeder Endpunkt der Konten, Buchungen, Verträge, Pläne und Kategorien, mit fremdem Besitzer und
/// ohne Freigabe aufgerufen, antwortet ohne Erfolg. Die Liste der Endpunkte kommt aus der laufenden API; ein neuer
/// Endpunkt ohne Eintrag hier lässt den Test scheitern, so wird keiner vergessen.
/// </summary>
[Collection(ApiCollection.Name)]
public class AccessWalkTests
{
    private static readonly string[] Areas =
        ["/api/v1/accounts", "/api/v1/bookings", "/api/v1/commitments", "/api/v1/plans", "/api/v1/categories"];

    /// <summary>
    /// Endpunkte, an denen sich kein fremder Besitzer nennen lässt: Eine Kategorie legt man immer für sich selbst an.
    /// Der Test prüft für sie, dass das Ergebnis der angemeldeten Person gehört und nicht der fremden.
    /// </summary>
    private static readonly string[] OwnOnly = ["POST /api/v1/categories"];

    private readonly ApiFixture fixture;
    private readonly HttpClient client;

    public AccessWalkTests(ApiFixture fixture)
    {
        this.fixture = fixture;
        client = fixture.Factory.CreateClient();
    }

    /// <summary>Die Daten der fremden Person, auf die die Anfragen zielen.</summary>
    private sealed record Foreign(Guid OwnerId, Guid Account, Guid Booking, Guid Commitment, Guid Category, Guid Position);

    /// <summary>Jeder Endpunkt der Bereiche als "METHODE /muster", so wie die API ihn eingetragen hat.</summary>
    private List<string> MappedEndpoints()
    {
        var source = fixture.Factory.Services.GetRequiredService<EndpointDataSource>();
        return source.Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText is { } raw && Areas.Any(raw.StartsWith))
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []).Select(m => $"{m} {e.RoutePattern.RawText!.TrimEnd('/')}"))
            .Order()
            .ToList();
    }

    private static readonly Guid Groceries = CategoryCatalog.IdFor("household.groceries");

    private static SaveBookingRequest Expense(Guid account, Guid? ownerId = null) =>
        new(BookingKinds.Expense, account, null, new DateOnly(2026, 2, 2), 10.00m, null, [new SaveBookingLine(10.00m, PotKeys.Needs, Groceries)], OwnerId: ownerId);

    /// <summary>Für jeden Endpunkt eine gültige Anfrage auf die Daten der fremden Person.</summary>
    private static Dictionary<string, (HttpMethod Method, string Path, object? Body)> Requests(Foreign f)
    {
        var owner = $"owner={f.OwnerId}";
        var plan = "/api/v1/plans/2026/11";
        return new()
        {
            ["GET /api/v1/accounts"] = (HttpMethod.Get, $"/api/v1/accounts?{owner}", null),
            ["POST /api/v1/accounts"] = (HttpMethod.Post, "/api/v1/accounts",
                new CreateAccountRequest("Giro", AccountTypes.Checking, 1m, new DateOnly(2026, 1, 1), OwnerId: f.OwnerId)),
            ["PATCH /api/v1/accounts/{id:guid}"] = (HttpMethod.Patch, $"/api/v1/accounts/{f.Account}", new UpdateAccountRequest(Name: "Fremd")),
            ["GET /api/v1/bookings"] = (HttpMethod.Get, $"/api/v1/bookings?year=2026&month=2&{owner}", null),
            ["POST /api/v1/bookings"] = (HttpMethod.Post, "/api/v1/bookings", Expense(f.Account, f.OwnerId)),
            ["PUT /api/v1/bookings/{id:guid}"] = (HttpMethod.Put, $"/api/v1/bookings/{f.Booking}", Expense(f.Account)),
            ["DELETE /api/v1/bookings/{id:guid}"] = (HttpMethod.Delete, $"/api/v1/bookings/{f.Booking}", null),
            ["GET /api/v1/commitments"] = (HttpMethod.Get, $"/api/v1/commitments?{owner}", null),
            ["POST /api/v1/commitments"] = (HttpMethod.Post, "/api/v1/commitments", new CreateCommitmentRequest(
                CommitmentTypes.Contract, "Miete", 890.00m, 1, new DateOnly(2026, 11, 1), null,
                CategoryCatalog.IdFor("housing.rent"), PotKeys.Needs, PaymentMethod: PaymentMethods.StandingOrder, OwnerId: f.OwnerId)),
            ["PATCH /api/v1/commitments/{id:guid}"] = (HttpMethod.Patch, $"/api/v1/commitments/{f.Commitment}", new UpdateCommitmentRequest(Name: "Fremd")),
            ["POST /api/v1/commitments/{id:guid}/end"] = (HttpMethod.Post, $"/api/v1/commitments/{f.Commitment}/end", null),
            ["GET /api/v1/categories"] = (HttpMethod.Get, $"/api/v1/categories?{owner}", null),
            ["POST /api/v1/categories"] = (HttpMethod.Post, "/api/v1/categories", new { name = $"Fremd {Guid.NewGuid():N}", group = CategoryGroupKeys.All[0], pot = PotKeys.Needs, ownerId = f.OwnerId }),
            ["PUT /api/v1/categories/{id:guid}"] = (HttpMethod.Put, $"/api/v1/categories/{f.Category}", new SaveCategoryRequest("Fremd", CategoryGroupKeys.All[0], PotKeys.Needs)),
            ["DELETE /api/v1/categories/{id:guid}"] = (HttpMethod.Delete, $"/api/v1/categories/{f.Category}", null),
            ["GET /api/v1/plans"] = (HttpMethod.Get, $"/api/v1/plans?{owner}", null),
            ["GET /api/v1/plans/overview"] = (HttpMethod.Get, $"/api/v1/plans/overview?year=2026&{owner}", null),
            ["GET /api/v1/plans/{year:int}/{month:int}"] =(HttpMethod.Get, $"{plan}?{owner}", null),
            ["POST /api/v1/plans/{year:int}/{month:int}/positions"] = (HttpMethod.Post, $"{plan}/positions?{owner}",
                new CreatePositionRequest("Fremd", 5m, 3, Groceries, PotKeys.Needs)),
            ["PATCH /api/v1/plans/{year:int}/{month:int}/positions/{id:guid}"] = (HttpMethod.Patch, $"{plan}/positions/{f.Position}?{owner}", new UpdatePositionRequest(Label: "Fremd")),
            ["DELETE /api/v1/plans/{year:int}/{month:int}/positions/{id:guid}"] = (HttpMethod.Delete, $"{plan}/positions/{f.Position}?{owner}", null),
            ["POST /api/v1/plans/{year:int}/{month:int}/positions/{id:guid}/tick"] = (HttpMethod.Post, $"{plan}/positions/{f.Position}/tick?{owner}", null),
            ["DELETE /api/v1/plans/{year:int}/{month:int}/positions/{id:guid}/tick"] = (HttpMethod.Delete, $"{plan}/positions/{f.Position}/tick?{owner}", null),
            ["POST /api/v1/plans/{year:int}/{month:int}/changes/apply"] = (HttpMethod.Post, $"{plan}/changes/apply?{owner}", null),
            ["POST /api/v1/plans/{year:int}/{month:int}/carry-over"] = (HttpMethod.Post, $"{plan}/carry-over?{owner}", new CreateCarryOverRequest(5m)),
            ["DELETE /api/v1/plans/{year:int}/{month:int}/carry-over"] = (HttpMethod.Delete, $"{plan}/carry-over?{owner}", null),
        };
    }

    /// <summary>Legt für die Person ein Konto, eine Buchung, einen Vertrag, eine Kategorie, einen Posten und einen Übertrag an.</summary>
    private async Task<Foreign> DataOfAsync(Person owner)
    {
        async Task<T> PostAsync<T>(string path, object body)
        {
            var response = await client.SendAsync(owner, HttpMethod.Post, path, body);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<T>())!;
        }

        var account = await PostAsync<AccountResponse>("/api/v1/accounts", new CreateAccountRequest("Giro", AccountTypes.Checking, 100m, new DateOnly(2026, 1, 1)));
        var booking = await PostAsync<BookingResponse>("/api/v1/bookings", Expense(account.Id));
        var commitment = await PostAsync<CommitmentResponse>("/api/v1/commitments", new CreateCommitmentRequest(
            CommitmentTypes.Contract, "Strom", 60.00m, 1, new DateOnly(2026, 12, 1), null,
            CategoryCatalog.IdFor("housing.utilities"), PotKeys.Needs, PaymentMethod: PaymentMethods.StandingOrder));
        var category = await PostAsync<CategoryResponse>("/api/v1/categories", new SaveCategoryRequest("Kita", CategoryGroupKeys.All[0], PotKeys.Needs));
        var position = await PostAsync<PlanPositionResponse>("/api/v1/plans/2026/11/positions", new CreatePositionRequest("Geschenk", 30m, 15, Groceries, PotKeys.Needs));
        await PostAsync<PlanResponse>("/api/v1/plans/2026/11/carry-over", new CreateCarryOverRequest(20m));
        return new Foreign(owner.Id, account.Id, booking.Id, commitment.Id, category.Id, position.Id);
    }

    /// <summary>Alles, was der Person gehört, als Text; vorher und nachher gleich heißt: nichts geändert.</summary>
    private Task<string> SnapshotAsync(Guid ownerId) => fixture.Factory.WithDbAsync(async db =>
    {
        var accounts = await db.Accounts.AsNoTracking().Where(a => a.OwnerId == ownerId).OrderBy(a => a.Id)
            .Select(a => $"{a.Id}:{a.Name}:{a.IsDefault}:{a.Active}").ToListAsync();
        var bookings = await db.Bookings.AsNoTracking().Where(b => b.OwnerId == ownerId).OrderBy(b => b.Id)
            .Select(b => $"{b.Id}:{b.Amount}:{b.PaymentDate}").ToListAsync();
        var commitments = await db.Commitments.AsNoTracking().Where(c => c.OwnerId == ownerId).OrderBy(c => c.Id)
            .Select(c => $"{c.Id}:{c.Name}:{c.LastDueDate}").ToListAsync();
        var categories = await db.Categories.AsNoTracking().Where(c => c.OwnerId == ownerId).OrderBy(c => c.Id)
            .Select(c => $"{c.Id}:{c.Name}").ToListAsync();
        var positions = await db.Plans.AsNoTracking().Where(p => p.OwnerId == ownerId)
            .SelectMany(p => p.Positions).OrderBy(p => p.Id).Select(p => $"{p.Id}:{p.Label}:{p.DoneAt}").ToListAsync();
        var plans = await db.Plans.AsNoTracking().Where(p => p.OwnerId == ownerId).OrderBy(p => p.Id).Select(p => $"{p.Id}:{p.Year}-{p.Month}").ToListAsync();
        return string.Join("|", accounts.Concat(bookings).Concat(commitments).Concat(categories).Concat(positions).Concat(plans));
    });

    // A10
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task No_endpoint_answers_with_success_for_a_foreign_owner_without_a_grant(bool sameHousehold)
    {
        // Ohne das erste Konto ist das Giro das Standardkonto; das erste Konto startet heute und trüge keinen Übertrag.
        var (anna, ben) = await client.AnnaAndBenAsync(fixture.Factory);
        var foreignPerson = sameHousehold ? anna : await client.FounderAsync("Clara", withoutFirstAccount: fixture.Factory);
        var foreign = await DataOfAsync(foreignPerson);
        // Ben gibt Anna alles; was er gibt, darf ihm selbst nichts öffnen.
        await client.GrantAsync(ben, anna, new UpdateGrantsRequest(AccessLevelKeys.Delete, AccessLevelKeys.Delete, AccessLevelKeys.Delete, AccessLevelKeys.Delete));
        var before = await SnapshotAsync(foreign.OwnerId);
        var requests = Requests(foreign);

        Assert.Equal(MappedEndpoints(), requests.Keys.Order().ToList());
        var successes = new List<string>();
        foreach (var (endpoint, (method, path, body)) in requests)
        {
            var answer = await client.SendAsync(ben, method, path, body);
            if (answer.IsSuccessStatusCode && !OwnOnly.Contains(endpoint))
            {
                successes.Add($"{endpoint} -> {(int)answer.StatusCode}");
            }
        }

        Assert.Empty(successes);
        Assert.Equal(before, await SnapshotAsync(foreign.OwnerId));
        Assert.True(await fixture.Factory.WithDbAsync(db => db.Categories.AnyAsync(c => c.OwnerId == ben.Id && c.Name!.StartsWith("Fremd"))));
    }
}
