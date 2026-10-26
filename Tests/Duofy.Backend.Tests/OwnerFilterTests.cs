using System.Net.Http.Json;
using Duofy.Schemas;
using Duofy.Data;
using Duofy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duofy.Api.Tests;

/// <summary>
/// Das Netz unter der zentralen Prüfung (Spec 56): Abfragen ohne eigene Bedingung auf den Besitzer sehen nur
/// die Daten sichtbarer Besitzer, und Speichern für einen fremden Besitzer scheitert. Die Abfragen laufen hier
/// absichtlich direkt auf der Datenbank, so wie ein Endpunkt, der seine Prüfung vergessen hat.
/// </summary>
[Collection(ApiCollection.Name)]
public class OwnerFilterTests(ApiFixture fixture)
{
    private const string November = "/api/v1/plans/2026/11";

    private readonly HttpClient client = fixture.Factory.CreateClient();

    /// <summary>Gibt der Person Konto, eigene Kategorie, Buchung, Vertrag und den November mit einem eigenen Posten.</summary>
    private async Task<Guid> SeedAsync(Person person)
    {
        var account = await ReadAsync<AccountResponse>(await client.SendAsync(person, HttpMethod.Post, "/api/v1/accounts",
            new CreateAccountRequest("Giro", AccountTypes.Checking, 100m, new DateOnly(2026, 1, 1))));
        var category = await ReadAsync<CategoryResponse>(await client.SendAsync(person, HttpMethod.Post, "/api/v1/categories",
            new SaveCategoryRequest("Eigen", "leisure", PotKeys.Wants)));
        (await client.SendAsync(person, HttpMethod.Post, "/api/v1/bookings",
            new SaveBookingRequest(BookingKinds.Expense, account.Id, null, new DateOnly(2026, 10, 5), 12m, null,
                [new SaveBookingLine(12m, PotKeys.Wants, category.Id)]))).EnsureSuccessStatusCode();
        (await client.SendAsync(person, HttpMethod.Post, "/api/v1/commitments", new CreateCommitmentRequest(
            CommitmentTypes.Contract, "Miete", 890.00m, 1, new DateOnly(2026, 11, 5), null,
            CategoryCatalog.IdFor("housing.rent"), PotKeys.Needs, PaymentMethod: PaymentMethods.StandingOrder))).EnsureSuccessStatusCode();
        (await client.SendAsync(person, HttpMethod.Post, $"{November}/positions",
            new CreatePositionRequest("Geschenk", 30.00m, 15, CategoryCatalog.IdFor("household.groceries"), PotKeys.Needs))).EnsureSuccessStatusCode();
        return account.Id;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    /// <summary>
    /// Läuft wie in einer Anfrage von <paramref name="callerId"/>: Die Besitzer werden so geladen, wie es die API
    /// am Anfang jeder Anfrage tut. Ohne Kennung wie eine Anfrage ohne Anmeldung.
    /// </summary>
    private async Task<T> AsAsync<T>(Guid? callerId, Func<DuofyDbContext, Task<T>> action)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DuofyDbContext>();
        if (callerId is { } id)
        {
            await scope.ServiceProvider.GetRequiredService<OwnerContext>().LoadAsync(db, id, CancellationToken.None);
        }
        return await action(db);
    }

    /// <summary>Was jede gefilterte Menge zeigt, ohne eine eigene Bedingung auf den Besitzer.</summary>
    private sealed record Seen(
        List<Guid> AccountOwners,
        List<Guid> BookingOwners,
        List<Guid> CommitmentOwners,
        List<Guid> PlanOwners,
        List<Guid?> CategoryOwners,
        List<Guid> LineIds,
        List<Guid> PositionIds);

    private static async Task<Seen> SeenAsync(DuofyDbContext db) => new(
        await db.Accounts.Select(a => a.OwnerId).Distinct().ToListAsync(),
        await db.Bookings.Select(b => b.OwnerId).Distinct().ToListAsync(),
        await db.Commitments.Select(c => c.OwnerId).Distinct().ToListAsync(),
        await db.Plans.Select(p => p.OwnerId).Distinct().ToListAsync(),
        await db.Categories.Select(c => c.OwnerId).Distinct().ToListAsync(),
        await db.Set<BookingLine>().Select(l => l.Id).ToListAsync(),
        await db.PlanPositions.Select(p => p.Id).ToListAsync());

    /// <summary>Die Besitzer der Buchungszeilen und Posten, an jedem Filter vorbei nachgeschlagen.</summary>
    private Task<(List<Guid> LineOwners, List<Guid> PositionOwners)> OwnersOfAsync(Seen seen) => fixture.Factory.WithDbAsync(async db => (
        await db.Bookings.SelectMany(b => b.Lines.Select(l => new { l.Id, b.OwnerId }))
            .Where(l => seen.LineIds.Contains(l.Id)).Select(l => l.OwnerId).Distinct().ToListAsync(),
        await db.Plans.SelectMany(p => p.Positions.Select(position => new { position.Id, p.OwnerId }))
            .Where(p => seen.PositionIds.Contains(p.Id)).Select(p => p.OwnerId).Distinct().ToListAsync()));

    // A1 (R1, R5)
    [Fact]
    public async Task Without_any_grant_bens_context_sees_only_his_own_rows_in_every_set()
    {
        var anna = await client.PersonAsync("Anna");
        var ben = await client.PersonAsync("Ben");
        await SeedAsync(anna);
        await SeedAsync(ben);

        var seen = await AsAsync(ben.Id, SeenAsync);
        var (lineOwners, positionOwners) = await OwnersOfAsync(seen);

        Assert.Equal([ben.Id], seen.AccountOwners);
        Assert.Equal([ben.Id], seen.BookingOwners);
        Assert.Equal([ben.Id], seen.CommitmentOwners);
        Assert.Equal([ben.Id], seen.PlanOwners);
        Assert.Equal([ben.Id], lineOwners);
        Assert.Equal([ben.Id], positionOwners);
        // Die eingebauten Kategorien ohne Besitzer sieht jeder, der angemeldet ist; eigene nur von Ben.
        Assert.Equal(new Guid?[] { null, ben.Id }.Order(), seen.CategoryOwners.Order());
    }

    // A2 (R1)
    [Fact]
    public async Task With_annas_plan_view_ben_lists_annas_and_his_plans_but_none_of_claras()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var clara = await client.FounderAsync("Clara");
        await SeedAsync(anna);
        await SeedAsync(ben);
        await SeedAsync(clara);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.View));

        var planOwners = await AsAsync(ben.Id, db => db.Plans.Select(p => p.OwnerId).Distinct().ToListAsync());

        Assert.Equal(new[] { anna.Id, ben.Id }.Order(), planOwners.Order());
    }

    // A9 (R1): Eine Freigabezeile zählt nur im selben Haushalt. Clara aus einem anderen Haushalt hat eine Zeile an
    // Ben (wie sie nach einem Austritt stehen bleiben könnte); Ben sieht trotzdem keinen ihrer Pläne.
    [Fact]
    public async Task A_grant_row_from_clara_of_another_household_shows_ben_none_of_her_plans()
    {
        var (_, ben) = await client.AnnaAndBenAsync();
        var clara = await client.FounderAsync("Clara");
        await SeedAsync(ben);
        await SeedAsync(clara);
        await fixture.Factory.WithDbAsync(async db =>
        {
            db.Grants.Add(new Grant { GranterId = clara.Id, GranteeId = ben.Id, Area = Area.Plan, Level = AccessLevel.Delete });
            return await db.SaveChangesAsync();
        });

        var planOwners = await AsAsync(ben.Id, db => db.Plans.Select(p => p.OwnerId).Distinct().ToListAsync());

        Assert.Equal([ben.Id], planOwners);
    }

    // A10 (R1): Eine Anfrage mit ?owner= liest die Freigaben genau einmal; der Besitzer-Filter und die zentrale
    // Prüfung nutzen dasselbe Ergebnis. Der Plan fragt mehrere Bereiche ab (Planung, Buchungen, Konten, Verträge).
    [Fact]
    public async Task One_request_for_annas_month_reads_the_grants_once()
    {
        var logs = new CapturedLogs();
        await using var api = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logs)));
        var counted = api.CreateClient();
        var (anna, ben) = await counted.AnnaAndBenAsync();
        await counted.GrantAsync(anna, ben, new UpdateGrantsRequest(
            Plan: AccessLevelKeys.View, Book: AccessLevelKeys.View, Accounts: AccessLevelKeys.View, Commitments: AccessLevelKeys.View));
        var before = GrantQueries(logs);

        var month = await counted.SendAsync(ben, HttpMethod.Get, $"{November}?owner={anna.Id}");

        Assert.Equal(System.Net.HttpStatusCode.OK, month.StatusCode);
        Assert.Equal(1, GrantQueries(logs) - before);
    }

    // Jede Abfrage, die EF Core auf der Tabelle der Freigaben ausführt.
    private static int GrantQueries(CapturedLogs logs) =>
        logs.Messages("Microsoft.EntityFrameworkCore.Database.Command")
            .Count(m => m.Contains("Executed DbCommand") && m.Contains("FROM grants"));

    // A3 (R2)
    [Fact]
    public async Task Without_a_signed_in_person_every_filtered_set_is_empty()
    {
        await SeedAsync(await client.PersonAsync("Anna"));

        var seen = await AsAsync(null, SeenAsync);

        Assert.Empty(seen.AccountOwners);
        Assert.Empty(seen.BookingOwners);
        Assert.Empty(seen.CommitmentOwners);
        Assert.Empty(seen.PlanOwners);
        Assert.Empty(seen.CategoryOwners);
        Assert.Empty(seen.LineIds);
        Assert.Empty(seen.PositionIds);
    }

    private static Booking BookingFor(Guid ownerId, Guid accountId) => new()
    {
        Id = Guid.NewGuid(),
        OwnerId = ownerId,
        AccountId = accountId,
        PaymentDate = new DateOnly(2026, 11, 6),
        PlanYear = 2026,
        PlanMonth = 11,
        Amount = 5m,
    };

    private Task<bool> ExistsAsync(Guid bookingId) => fixture.Factory.WithDbAsync(db => db.Bookings.AnyAsync(b => b.Id == bookingId));

    // A4 (R3)
    [Fact]
    public async Task Without_a_grant_a_booking_with_annas_owner_is_refused_and_not_stored()
    {
        var anna = await client.PersonAsync("Anna");
        var ben = await client.PersonAsync("Ben");
        var annasAccount = await SeedAsync(anna);
        var booking = BookingFor(anna.Id, annasAccount);

        var refused = await AsAsync(ben.Id, async db =>
        {
            db.Bookings.Add(booking);
            return await Assert.ThrowsAsync<OwnerViolationException>(() => db.SaveChangesAsync());
        });

        Assert.Equal((nameof(Booking), anna.Id, (Guid?)ben.Id), (refused.EntityType, refused.OwnerId, refused.CurrentUserId));
        Assert.False(await ExistsAsync(booking.Id));
    }

    // A4 (R3)
    [Fact]
    public async Task With_annas_book_create_grant_the_same_booking_is_stored()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var annasAccount = await SeedAsync(anna);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Book: AccessLevelKeys.Create));
        var booking = BookingFor(anna.Id, annasAccount);

        await AsAsync(ben.Id, async db =>
        {
            db.Bookings.Add(booking);
            return await db.SaveChangesAsync();
        });

        Assert.True(await ExistsAsync(booking.Id));
    }

    // R3: Ansehen reicht zum Lesen, nicht zum Ändern.
    [Fact]
    public async Task With_only_view_ben_reads_annas_account_but_cannot_save_a_change_to_it()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var annasAccount = await SeedAsync(anna);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Accounts: AccessLevelKeys.View));

        var refused = await AsAsync(ben.Id, async db =>
        {
            var account = await db.Accounts.SingleAsync(a => a.Id == annasAccount);
            account.Name = "Gekapert";
            return await Assert.ThrowsAsync<OwnerViolationException>(() => db.SaveChangesAsync());
        });

        Assert.Equal((nameof(Account), annasAccount.ToString()), (refused.EntityType, refused.EntityKey));
        Assert.Equal("Giro", await fixture.Factory.WithDbAsync(db => db.Accounts.Where(a => a.Id == annasAccount).Select(a => a.Name).SingleAsync()));
    }

    // R3: Wer Annas Konto nur sieht, kann es auch nicht zu seinem eigenen machen; es zählt auch der alte Besitzer.
    [Fact]
    public async Task With_only_view_ben_cannot_take_annas_account_over_by_changing_its_owner()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var annasAccount = await SeedAsync(anna);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Accounts: AccessLevelKeys.View));

        var refused = await AsAsync(ben.Id, async db =>
        {
            var account = await db.Accounts.SingleAsync(a => a.Id == annasAccount);
            account.OwnerId = ben.Id;
            return await Assert.ThrowsAsync<OwnerViolationException>(() => db.SaveChangesAsync());
        });

        Assert.Equal(anna.Id, refused.OwnerId);
        Assert.Equal(anna.Id, await fixture.Factory.WithDbAsync(db => db.Accounts.Where(a => a.Id == annasAccount).Select(a => a.OwnerId).SingleAsync()));
    }

    /// <summary>Alles von Anna, das A7 anfasst, an jedem Filter vorbei gelesen: so lässt sich "nichts ändert sich" vergleichen.</summary>
    private Task<string> FingerprintAsync(Guid ownerId) => fixture.Factory.WithDbAsync(async db =>
    {
        var positions = await db.Plans.Where(p => p.OwnerId == ownerId)
            .SelectMany(p => p.Positions.Select(position => position.Id + ":" + position.Label + ":" + position.AmountPlanned)).ToListAsync();
        var lines = await db.Bookings.Where(b => b.OwnerId == ownerId)
            .SelectMany(b => b.Lines.Select(l => l.Id + ":" + l.Amount)).ToListAsync();
        var accounts = await db.Accounts.Where(a => a.OwnerId == ownerId).Select(a => a.Id + ":" + a.Name).ToListAsync();
        return string.Join("|", positions.Order().Concat(lines.Order()).Concat(accounts.Order()));
    });

    /// <summary>Was Ben in A7 mit Annas Daten versucht, jeweils in seinem eigenen Anfragekontext.</summary>
    public static TheoryData<string> AttemptsOnAnnasData() =>
    [
        "add a position to her tracked plan",
        "change a position of her tracked plan",
        "remove a position from her tracked plan",
        "change a position loaded without its plan",
        "remove a position loaded without its plan",
        "add a line to her tracked booking",
        "change a line loaded without its booking",
        "remove a line from her tracked booking",
        "remove her account",
    ];

    private static async Task TryAsync(DuofyDbContext db, string attempt, Guid annaId)
    {
        switch (attempt)
        {
            case "add a position to her tracked plan":
                var plan = await db.Plans.Include(p => p.Positions).SingleAsync(p => p.OwnerId == annaId && p.Month == 11);
                plan.Positions.Add(new PlanPosition
                {
                    Id = Guid.NewGuid(), Label = "Fremd", AmountPlanned = 1m, DueDay = 1, Type = CommitmentType.Contract,
                    CategoryId = CategoryCatalog.IdFor("household.groceries"), Pot = Pot.Needs,
                });
                break;
            case "change a position of her tracked plan":
                (await db.Plans.Include(p => p.Positions).SingleAsync(p => p.OwnerId == annaId && p.Month == 11)).Positions[0].Label = "Fremd";
                break;
            case "remove a position from her tracked plan":
                var withPositions = await db.Plans.Include(p => p.Positions).SingleAsync(p => p.OwnerId == annaId && p.Month == 11);
                withPositions.Positions.RemoveAt(0);
                break;
            case "change a position loaded without its plan":
                (await PositionOfAsync(db, annaId)).AmountPlanned = 999m;
                break;
            case "remove a position loaded without its plan":
                db.PlanPositions.Remove(await PositionOfAsync(db, annaId));
                break;
            case "add a line to her tracked booking":
                var booking = await db.Bookings.Include(b => b.Lines).SingleAsync(b => b.OwnerId == annaId);
                booking.Lines.Add(new BookingLine { Amount = 1m, Pot = Pot.Needs, CategoryId = CategoryCatalog.IdFor("household.groceries") });
                break;
            case "change a line loaded without its booking":
                (await LineOfAsync(db, annaId)).Amount = 999m;
                break;
            case "remove a line from her tracked booking":
                var withLines = await db.Bookings.Include(b => b.Lines).SingleAsync(b => b.OwnerId == annaId);
                withLines.Lines.RemoveAt(0);
                break;
            case "remove her account":
                db.Accounts.Remove(await db.Accounts.SingleAsync(a => a.OwnerId == annaId));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(attempt), attempt, "Unbekannter Versuch.");
        }
    }

    // Ein Posten oder eine Zeile allein, ohne den Plan oder die Buchung zu laden: die Prüfung muss den Besitzer nachschlagen.
    private static Task<PlanPosition> PositionOfAsync(DuofyDbContext db, Guid ownerId) =>
        db.PlanPositions.FirstAsync(p => db.Plans.Any(plan => plan.OwnerId == ownerId && plan.Id == EF.Property<Guid>(p, DuofyDbContext.PositionPlan)));

    private static Task<BookingLine> LineOfAsync(DuofyDbContext db, Guid ownerId) =>
        db.Set<BookingLine>().FirstAsync(l => db.Bookings.Any(b => b.OwnerId == ownerId && b.Id == EF.Property<Guid>(l, "BookingId")));

    // A7 (R3): Mit nur Planung Ansehen ändert Ben nichts an Annas Posten, Buchungszeilen oder Konten, auch nicht über
    // Kinder ohne eigenen Besitzer und nicht durch Entfernen.
    [Theory]
    [MemberData(nameof(AttemptsOnAnnasData))]
    public async Task With_only_plan_view_ben_changes_nothing_of_anna(string attempt)
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        await SeedAsync(anna);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.View));
        var before = await FingerprintAsync(anna.Id);

        var refused = await AsAsync(ben.Id, async db =>
        {
            await TryAsync(db, attempt, anna.Id);
            return await Assert.ThrowsAsync<OwnerViolationException>(() => db.SaveChangesAsync());
        });

        Assert.Equal(((Guid?)anna.Id, (Guid?)ben.Id), (refused.OwnerId, refused.CurrentUserId));
        Assert.Equal(before, await FingerprintAsync(anna.Id));
    }

    // A7 (R3): Eine eingebaute Kategorie hat keinen Besitzer; niemand kann sie sich zuschreiben, auch nicht für sich selbst.
    [Fact]
    public async Task Nobody_can_give_a_built_in_category_an_owner()
    {
        var ben = await client.PersonAsync("Ben");
        var builtIn = CategoryCatalog.IdFor("household.groceries");

        await AsAsync(ben.Id, async db =>
        {
            (await db.Categories.SingleAsync(c => c.Id == builtIn)).OwnerId = ben.Id;
            return await Assert.ThrowsAsync<OwnerViolationException>(() => db.SaveChangesAsync());
        });

        Assert.Null(await fixture.Factory.WithDbAsync(db => db.Categories.Where(c => c.Id == builtIn).Select(c => c.OwnerId).SingleAsync()));
    }

    // R4: Im Bereich ohne Filter sieht und schreibt die Verbindung alles; danach ist der Filter wieder an.
    [Fact]
    public async Task Inside_a_bypass_everything_is_visible_and_afterwards_the_filter_is_back()
    {
        var anna = await client.PersonAsync("Anna");
        var ben = await client.PersonAsync("Ben");
        await SeedAsync(anna);

        var (inside, after) = await AsAsync(ben.Id, async db =>
        {
            bool sawAnna;
            using (db.Owners.Bypass("Test des Bereichs"))
            {
                sawAnna = await db.Accounts.AnyAsync(a => a.OwnerId == anna.Id);
            }
            return (sawAnna, await db.Accounts.AnyAsync(a => a.OwnerId == anna.Id));
        });

        Assert.True(inside);
        Assert.False(after);
    }

    // R4 und Spec 68: Wer Annas Planung nur ansieht, öffnet trotzdem einen Monat, den es noch nicht gibt. Das
    // Anlegen gehört zum Öffnen (Spec 66, R1) und ist das Anlegen eines Mitgliedermonats.
    [Fact]
    public async Task With_plan_view_ben_opens_a_month_of_anna_that_does_not_exist_yet()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.View));

        var open = await client.SendAsync(ben, HttpMethod.Get, $"/api/v1/plans/2026/12?owner={anna.Id}");

        Assert.Equal(System.Net.HttpStatusCode.OK, open.StatusCode);
        Assert.True(await fixture.Factory.WithDbAsync(db => db.Plans.AnyAsync(p => p.OwnerId == anna.Id && p.Year == 2026 && p.Month == 12)));
    }
}
