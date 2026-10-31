using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>Konten anlegen, ändern, auflösen und lesen (Spec 58, R1 bis R7).</summary>
[Collection(ApiCollection.Name)]
public class AccountTests
{
    private const string Path = "/api/v1/accounts";

    private static readonly DateOnly October1 = new(2026, 10, 1);

    private readonly ApiFixture fixture;
    private readonly HttpClient client;

    public AccountTests(ApiFixture fixture)
    {
        this.fixture = fixture;
        client = fixture.Factory.CreateClient();
    }

    private static DateOnly TodayInBerlin =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin")).DateTime);

    private static CreateAccountRequest Giro(string name = "Giro") =>
        new(name, AccountTypes.Checking, 1250.00m, October1);

    private Task<HttpResponseMessage> PostAsync(string token, CreateAccountRequest request) =>
        client.SendAsync(Authorized(HttpMethod.Post, Path, token, request));

    private Task<HttpResponseMessage> PatchAsync(string token, Guid id, UpdateAccountRequest request) =>
        client.SendAsync(Authorized(HttpMethod.Patch, $"{Path}/{id}", token, request));

    private async Task<AccountResponse> CreateAsync(string token, CreateAccountRequest request)
    {
        var response = await PostAsync(token, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AccountResponse>())!;
    }

    private async Task<AccountResponse> UpdateAsync(string token, Guid id, UpdateAccountRequest request)
    {
        var response = await PatchAsync(token, id, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AccountResponse>())!;
    }

    private async Task<List<AccountResponse>> ListAsync(string token, bool activeOnly = false)
    {
        var response = await client.SendAsync(Authorized(HttpMethod.Get, activeOnly ? Path + "?active=true" : Path, token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<AccountResponse>>())!;
    }

    [Fact]
    public async Task Accounts_need_a_signed_in_person()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Path, Giro())).StatusCode);
    }

    // A1
    [Fact]
    public async Task Created_account_is_listed_with_its_opening_balance()
    {
        var (_, tokens) = await client.SignUpAsync(fixture.Factory);

        var response = await PostAsync(tokens.AccessToken, Giro());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<AccountResponse>())!;
        var listed = Assert.Single(await ListAsync(tokens.AccessToken));
        Assert.Equal(created.Id, listed.Id);
        Assert.Equal("Giro", listed.Name);
        Assert.Equal(AccountTypes.Checking, listed.Type);
        Assert.Equal(1250.00m, listed.OpeningBalance);
        Assert.Equal(October1, listed.OpeningDate);
        Assert.Equal(1250.00m, listed.Balance);
        Assert.True(listed.Active);
        Assert.Null(listed.Iban);
    }

    [Fact]
    public async Task Name_is_trimmed_and_the_opening_balance_may_be_negative()
    {
        var (_, tokens) = await client.SignUpAsync();

        var created = await CreateAsync(tokens.AccessToken, Giro("  Überzogen  ") with { OpeningBalance = -320.15m });

        Assert.Equal("Überzogen", created.Name);
        Assert.Equal(-320.15m, created.Balance);
    }

    // A2
    [Theory]
    [InlineData("", AccountTypes.Checking, 0, ErrorCodes.AccountNameRequired)]
    [InlineData("   ", AccountTypes.Checking, 0, ErrorCodes.AccountNameRequired)]
    [InlineData(null, AccountTypes.Checking, 0, ErrorCodes.AccountNameRequired)]
    [InlineData("x", "credit_card", 0, ErrorCodes.AccountTypeInvalid)]
    [InlineData("x", "Checking", 0, ErrorCodes.AccountTypeInvalid)]
    [InlineData("x", null, 0, ErrorCodes.AccountTypeInvalid)]
    [InlineData("x", AccountTypes.Checking, 1, ErrorCodes.OpeningDateInFuture)]
    public async Task Invalid_account_answers_400_and_creates_nothing(string? name, string? type, int daysFromToday, string code)
    {
        var (_, tokens) = await client.SignUpAsync(fixture.Factory);
        var date = daysFromToday == 0 ? October1 : TodayInBerlin.AddDays(daysFromToday);

        var response = await PostAsync(tokens.AccessToken, new CreateAccountRequest(name, type, 0m, date));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, await response.ReadCodeAsync());
        Assert.Empty(await ListAsync(tokens.AccessToken));
    }

    // A2
    [Fact]
    public async Task Name_may_have_100_characters_but_not_101()
    {
        var (_, tokens) = await client.SignUpAsync(fixture.Factory);

        var ok = await PostAsync(tokens.AccessToken, Giro(new string('a', 100)));
        var tooLong = await PostAsync(tokens.AccessToken, Giro(new string('b', 101)));

        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(ErrorCodes.AccountNameTooLong, await tooLong.ReadCodeAsync());
        Assert.Single(await ListAsync(tokens.AccessToken));
    }

    [Fact]
    public async Task Opening_balance_has_at_most_two_decimals_and_a_limit()
    {
        var (_, tokens) = await client.SignUpAsync();

        var threeDecimals = await PostAsync(tokens.AccessToken, Giro() with { OpeningBalance = 1.005m });
        var tooLarge = await PostAsync(tokens.AccessToken, Giro() with { OpeningBalance = -(MoneyAccountRules.MaxOpeningBalance + 0.01m) });

        Assert.Equal(ErrorCodes.OpeningBalanceInvalid, await threeDecimals.ReadCodeAsync());
        Assert.Equal(ErrorCodes.OpeningBalanceInvalid, await tooLarge.ReadCodeAsync());
    }

    [Fact]
    public async Task Opening_date_today_is_allowed()
    {
        var (_, tokens) = await client.SignUpAsync();

        var response = await PostAsync(tokens.AccessToken, Giro() with { OpeningDate = TodayInBerlin });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // R2: Die Art schlägt "zählt als verfügbar" vor, die Person darf es ändern.
    [Fact]
    public async Task Counts_as_available_follows_the_type_unless_it_is_given()
    {
        var (_, tokens) = await client.SignUpAsync();

        var savings = await CreateAsync(tokens.AccessToken, new CreateAccountRequest("Spar", AccountTypes.Savings, 0m, October1));
        var cash = await CreateAsync(tokens.AccessToken, new CreateAccountRequest("Kasse", AccountTypes.Cash, 0m, October1));
        var savingsForSpending = await CreateAsync(tokens.AccessToken,
            new CreateAccountRequest("Tagesgeld", AccountTypes.Savings, 0m, October1, CountsAsAvailable: true));

        Assert.False(savings.CountsAsAvailable);
        Assert.True(cash.CountsAsAvailable);
        Assert.True(savingsForSpending.CountsAsAvailable);
    }

    // A5
    [Fact]
    public async Task First_checking_account_becomes_default_and_marking_another_replaces_it()
    {
        var (_, tokens) = await client.SignUpAsync(fixture.Factory);

        var first = await CreateAsync(tokens.AccessToken, Giro("Giro"));
        var second = await CreateAsync(tokens.AccessToken, Giro("Zweitkonto"));
        Assert.True(first.IsDefault);
        Assert.False(second.IsDefault);

        var marked = await UpdateAsync(tokens.AccessToken, second.Id, new UpdateAccountRequest(IsDefault: true));

        Assert.True(marked.IsDefault);
        var accounts = await ListAsync(tokens.AccessToken);
        Assert.Equal([second.Id], accounts.Where(a => a.IsDefault).Select(a => a.Id));
    }

    [Fact]
    public async Task Savings_or_cash_first_do_not_become_default_but_the_first_checking_account_does()
    {
        var (_, tokens) = await client.SignUpAsync(fixture.Factory);

        var savings = await CreateAsync(tokens.AccessToken, new CreateAccountRequest("Spar", AccountTypes.Savings, 0m, October1));
        var giro = await CreateAsync(tokens.AccessToken, Giro());

        Assert.False(savings.IsDefault);
        Assert.True(giro.IsDefault);
    }

    [Fact]
    public async Task Creating_with_is_default_replaces_the_old_default()
    {
        var (_, tokens) = await client.SignUpAsync();
        var giro = await CreateAsync(tokens.AccessToken, Giro());

        var cash = await CreateAsync(tokens.AccessToken,
            new CreateAccountRequest("Kasse", AccountTypes.Cash, 0m, October1, IsDefault: true));

        Assert.True(cash.IsDefault);
        Assert.False((await ListAsync(tokens.AccessToken)).Single(a => a.Id == giro.Id).IsDefault);
    }

    [Fact]
    public async Task No_new_automatic_default_once_the_person_had_a_checking_account()
    {
        var (_, tokens) = await client.SignUpAsync();
        var giro = await CreateAsync(tokens.AccessToken, Giro());
        await UpdateAsync(tokens.AccessToken, giro.Id, new UpdateAccountRequest(IsDefault: false));

        var second = await CreateAsync(tokens.AccessToken, Giro("Zweitkonto"));

        Assert.False(second.IsDefault);
    }

    // A6
    [Fact]
    public async Task Closing_the_default_account_clears_the_mark_and_hides_it_from_pickers()
    {
        var (_, tokens) = await client.SignUpAsync(fixture.Factory);
        var giro = await CreateAsync(tokens.AccessToken, Giro());
        Assert.True(giro.IsDefault);

        var closed = await UpdateAsync(tokens.AccessToken, giro.Id, new UpdateAccountRequest(Active: false));

        Assert.False(closed.Active);
        Assert.False(closed.IsDefault);
        Assert.Contains(await ListAsync(tokens.AccessToken), a => a.Id == giro.Id && !a.Active);
        Assert.DoesNotContain(await ListAsync(tokens.AccessToken, activeOnly: true), a => a.Id == giro.Id);
    }

    // A7
    [Fact]
    public async Task Closed_account_can_be_reopened()
    {
        var (_, tokens) = await client.SignUpAsync();
        var giro = await CreateAsync(tokens.AccessToken, Giro());
        await UpdateAsync(tokens.AccessToken, giro.Id, new UpdateAccountRequest(Active: false));

        var reopened = await UpdateAsync(tokens.AccessToken, giro.Id, new UpdateAccountRequest(Active: true));

        Assert.True(reopened.Active);
        Assert.Contains(await ListAsync(tokens.AccessToken, activeOnly: true), a => a.Id == giro.Id);
    }

    [Fact]
    public async Task Closed_account_cannot_become_default()
    {
        var (_, tokens) = await client.SignUpAsync();
        var giro = await CreateAsync(tokens.AccessToken, Giro());
        await UpdateAsync(tokens.AccessToken, giro.Id, new UpdateAccountRequest(Active: false));

        var response = await PatchAsync(tokens.AccessToken, giro.Id, new UpdateAccountRequest(IsDefault: true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.AccountClosed, await response.ReadCodeAsync());
    }

    // A8
    [Fact]
    public async Task Iban_is_stored_without_spaces_in_capitals_and_checked()
    {
        var (_, tokens) = await client.SignUpAsync();

        var created = await CreateAsync(tokens.AccessToken, Giro() with { Iban = "de89 3704 0044 0532 0130 00" });
        var wrong = await PostAsync(tokens.AccessToken, Giro("Falsch") with { Iban = "DE89370400440532013001" });

        Assert.Equal("DE89370400440532013000", created.Iban);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal(ErrorCodes.IbanInvalid, await wrong.ReadCodeAsync());
    }

    [Fact]
    public async Task Iban_can_be_changed_and_removed_with_an_empty_field()
    {
        var (_, tokens) = await client.SignUpAsync();
        var giro = await CreateAsync(tokens.AccessToken, Giro() with { Iban = "  " });
        Assert.Null(giro.Iban);

        var withIban = await UpdateAsync(tokens.AccessToken, giro.Id, new UpdateAccountRequest(Iban: "DE89370400440532013000"));
        var invalid = await PatchAsync(tokens.AccessToken, giro.Id, new UpdateAccountRequest(Iban: "DE00"));
        var removed = await UpdateAsync(tokens.AccessToken, giro.Id, new UpdateAccountRequest(Iban: ""));

        Assert.Equal("DE89370400440532013000", withIban.Iban);
        Assert.Equal(ErrorCodes.IbanInvalid, await invalid.ReadCodeAsync());
        Assert.Null(removed.Iban);
    }

    // R8 (ohne Buchungen): Startbetrag und Starttag bleiben korrigierbar.
    [Fact]
    public async Task Fields_can_be_changed_and_what_is_left_out_stays()
    {
        var (_, tokens) = await client.SignUpAsync(fixture.Factory);
        var giro = await CreateAsync(tokens.AccessToken, Giro());

        var changed = await UpdateAsync(tokens.AccessToken, giro.Id,
            new UpdateAccountRequest(Name: "Hauptkonto", OpeningBalance: 1300.00m, OpeningDate: new DateOnly(2026, 9, 30)));

        Assert.Equal("Hauptkonto", changed.Name);
        Assert.Equal(1300.00m, changed.OpeningBalance);
        Assert.Equal(new DateOnly(2026, 9, 30), changed.OpeningDate);
        Assert.Equal(1300.00m, changed.Balance);
        Assert.Equal(AccountTypes.Checking, changed.Type);
        Assert.True(changed.IsDefault);
    }

    [Fact]
    public async Task Changes_follow_the_same_rules_as_creating()
    {
        var (_, tokens) = await client.SignUpAsync(fixture.Factory);
        var giro = await CreateAsync(tokens.AccessToken, Giro());

        var emptyName = await PatchAsync(tokens.AccessToken, giro.Id, new UpdateAccountRequest(Name: " "));
        var badType = await PatchAsync(tokens.AccessToken, giro.Id, new UpdateAccountRequest(Type: "depot"));
        var future = await PatchAsync(tokens.AccessToken, giro.Id, new UpdateAccountRequest(OpeningDate: TodayInBerlin.AddDays(1)));

        Assert.Equal(ErrorCodes.AccountNameRequired, await emptyName.ReadCodeAsync());
        Assert.Equal(ErrorCodes.AccountTypeInvalid, await badType.ReadCodeAsync());
        Assert.Equal(ErrorCodes.OpeningDateInFuture, await future.ReadCodeAsync());
        Assert.Equal("Giro", (await ListAsync(tokens.AccessToken)).Single().Name);
    }

    // A9
    [Fact]
    public async Task Another_persons_account_answers_404_and_stays_unchanged()
    {
        var (_, anna) = await client.SignUpAsync(fixture.Factory);
        var (_, ben) = await client.SignUpAsync(fixture.Factory);
        var annas = await CreateAsync(anna.AccessToken, Giro("Annas Giro"));

        var patch = await PatchAsync(ben.AccessToken, annas.Id, new UpdateAccountRequest(Name: "Bens", Active: false));

        Assert.Equal(HttpStatusCode.NotFound, patch.StatusCode);
        Assert.Equal(ErrorCodes.AccountNotFound, await patch.ReadCodeAsync());
        Assert.Empty(await ListAsync(ben.AccessToken));
        var unchanged = Assert.Single(await ListAsync(anna.AccessToken));
        Assert.Equal("Annas Giro", unchanged.Name);
        Assert.True(unchanged.Active);
    }

    [Fact]
    public async Task Unknown_id_answers_404()
    {
        var (_, tokens) = await client.SignUpAsync();

        var patch = await PatchAsync(tokens.AccessToken, Guid.NewGuid(), new UpdateAccountRequest(Name: "x"));

        Assert.Equal(HttpStatusCode.NotFound, patch.StatusCode);
        Assert.Equal(ErrorCodes.AccountNotFound, await patch.ReadCodeAsync());
    }

    [Fact]
    public async Task Making_another_persons_account_default_leaves_both_defaults_alone()
    {
        var (_, anna) = await client.SignUpAsync(fixture.Factory);
        var (_, ben) = await client.SignUpAsync(fixture.Factory);
        var annas = await CreateAsync(anna.AccessToken, Giro());
        var bens = await CreateAsync(ben.AccessToken, Giro());

        await UpdateAsync(ben.AccessToken, bens.Id, new UpdateAccountRequest(IsDefault: true));

        Assert.True((await ListAsync(anna.AccessToken)).Single(a => a.Id == annas.Id).IsDefault);
    }

    [Fact]
    public async Task There_is_no_delete()
    {
        var (_, tokens) = await client.SignUpAsync(fixture.Factory);
        var giro = await CreateAsync(tokens.AccessToken, Giro());

        var delete = await client.SendAsync(Authorized(HttpMethod.Delete, $"{Path}/{giro.Id}", tokens.AccessToken));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, delete.StatusCode);
        Assert.Single(await ListAsync(tokens.AccessToken));
    }

    [Fact]
    public async Task Deleting_the_person_deletes_their_accounts()
    {
        var (_, tokens) = await client.SignUpAsync();
        await CreateAsync(tokens.AccessToken, Giro());

        var delete = await client.SendAsync(Authorized(HttpMethod.Delete, "/api/v1/users/me", tokens.AccessToken, new DeleteMeRequest(Password)));

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }
}
