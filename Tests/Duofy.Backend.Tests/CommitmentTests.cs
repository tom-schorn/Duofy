using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>
/// Verträge über die API mit echter Datenbank: anlegen, ändern, beenden und die Liste (Spec 64,
/// A1 bis A13). "Heute" ist hier immer der 23. Oktober 2026 in Berlin.
/// </summary>
[Collection(ApiCollection.Name)]
public class CommitmentTests : IDisposable
{
    private const string Path = "/api/v1/commitments";

    private static readonly DateOnly October1 = new(2026, 10, 1);

    private readonly WebApplicationFactory<Program> api;
    private readonly HttpClient client;

    public CommitmentTests(ApiFixture fixture)
    {
        // 23. Oktober 2026, 10 Uhr in Berlin (Sommerzeit, also 8 Uhr UTC).
        var today = new FixedTime(new DateTimeOffset(2026, 10, 23, 8, 0, 0, TimeSpan.Zero));
        api = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<TimeProvider>(today)));
        client = api.CreateClient();
    }

    public void Dispose()
    {
        client.Dispose();
        api.Dispose();
    }

    /// <summary>Eine angemeldete Person mit den eingebauten Kategorien, die die Tests brauchen.</summary>
    private sealed record Person(string Token, Guid Rent, Guid Salary, Guid Savings, Guid Streaming);

    private async Task<Person> SignUpAsync()
    {
        var (_, tokens) = await client.SignUpAsync();
        var categories = await client.SendAsync(Authorized(HttpMethod.Get, "/api/v1/categories", tokens.AccessToken));
        var all = (await categories.Content.ReadFromJsonAsync<List<CategoryResponse>>())!;
        Guid Id(string key) => all.Single(c => c.Key == key).Id;
        return new Person(tokens.AccessToken, Id("housing.rent"), Id("income.earned"), Id("finance.savings"), Id("leisure.subscriptions"));
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, Person person, object? body = null) =>
        client.SendAsync(Authorized(method, path, person.Token, body));

    private async Task<Guid> AccountAsync(Person person, string name, string type = AccountTypes.Checking)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/accounts", person, new CreateAccountRequest(name, type, 100m, October1));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AccountResponse>())!.Id;
    }

    private async Task<CommitmentResponse> CreateAsync(Person person, CreateCommitmentRequest request)
    {
        var response = await SendAsync(HttpMethod.Post, Path, person, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CommitmentResponse>())!;
    }

    private async Task<List<CommitmentResponse>> ListAsync(Person person, bool includeEnded = false)
    {
        var response = await SendAsync(HttpMethod.Get, includeEnded ? Path + "?includeEnded=true" : Path, person);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<CommitmentResponse>>())!;
    }

    /// <summary>Ein gewöhnlicher Vertrag mit sinnvollen Werten; die Tests ändern, was sie prüfen.</summary>
    private static CreateCommitmentRequest Rent(Person person, DateOnly? firstDue = null) => new(
        CommitmentTypes.Contract, "Miete", 890.00m, 1, firstDue ?? new DateOnly(2026, 11, 1), null,
        person.Rent, PotKeys.Needs, PaymentMethod: PaymentMethods.StandingOrder);

    private static CreateCommitmentRequest SavingsRate(Person person, string? pot = PotKeys.Savings) => new(
        CommitmentTypes.SavingsGoal, "Urlaub", 100.00m, 1, new DateOnly(2026, 11, 1), null,
        person.Savings, pot, PaymentMethod: PaymentMethods.StandingOrder);

    private static async Task AssertRefusedAsync(HttpResponseMessage response, string code)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, await response.ReadCodeAsync());
    }

    [Fact]
    public async Task Commitments_need_a_signed_in_person()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Path)).StatusCode);
    }

    // A1
    [Fact]
    public async Task A_new_contract_is_listed_with_its_next_due_date()
    {
        var person = await SignUpAsync();

        var response = await SendAsync(HttpMethod.Post, Path, person, Rent(person));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var listed = Assert.Single(await ListAsync(person));
        Assert.Equal("Miete", listed.Name);
        Assert.Equal(CommitmentTypes.Contract, listed.Type);
        Assert.Equal(890.00m, listed.Amount);
        Assert.Equal(1, listed.IntervalMonths);
        Assert.Equal(person.Rent, listed.CategoryId);
        Assert.Equal(PotKeys.Needs, listed.Pot);
        Assert.Equal(PaymentMethods.StandingOrder, listed.PaymentMethod);
        Assert.Equal(new DateOnly(2026, 11, 1), listed.NextDueDate);
        Assert.True(listed.Running);
    }

    // A2
    [Theory]
    [InlineData("", 890.00, 1, ErrorCodes.CommitmentNameRequired)]
    [InlineData("   ", 890.00, 1, ErrorCodes.CommitmentNameRequired)]
    [InlineData("101", 890.00, 1, ErrorCodes.CommitmentNameTooLong)]
    [InlineData("Miete", 0, 1, ErrorCodes.AmountInvalid)]
    [InlineData("Miete", 1.005, 1, ErrorCodes.AmountInvalid)]
    [InlineData("Miete", 890.00, 0, ErrorCodes.CommitmentIntervalInvalid)]
    [InlineData("Miete", 890.00, 121, ErrorCodes.CommitmentIntervalInvalid)]
    public async Task Invalid_name_amount_or_interval_is_refused_and_nothing_is_created(
        string name, double amount, int interval, string code)
    {
        var person = await SignUpAsync();
        var request = Rent(person) with
        {
            Name = name == "101" ? new string('x', 101) : name,
            Amount = (decimal)amount,
            IntervalMonths = interval,
        };

        await AssertRefusedAsync(await SendAsync(HttpMethod.Post, Path, person, request), code);
        Assert.Empty(await ListAsync(person, includeEnded: true));
    }

    [Fact]
    public async Task A_name_of_exactly_100_characters_is_accepted_and_trimmed()
    {
        var person = await SignUpAsync();

        var saved = await CreateAsync(person, Rent(person) with { Name = "  " + new string('x', 100) + " " });

        Assert.Equal(new string('x', 100), saved.Name);
    }

    // A3
    [Fact]
    public async Task Savings_goals_are_stored_in_the_pot_savings_and_income_in_the_pot_income()
    {
        var person = await SignUpAsync();

        var savings = await CreateAsync(person, SavingsRate(person, pot: PotKeys.Wants));
        var salary = await CreateAsync(person, new CreateCommitmentRequest(
            CommitmentTypes.Income, "Gehalt", 2450.00m, 1, new DateOnly(2026, 10, 28), null,
            person.Salary, PotKeys.Needs, PaymentMethod: PaymentMethods.Transfer));

        Assert.Equal(PotKeys.Savings, savings.Pot);
        Assert.Equal(PotKeys.Income, salary.Pot);
        var listed = await ListAsync(person);
        Assert.Equal(PotKeys.Savings, listed.Single(c => c.Id == savings.Id).Pot);
        Assert.Equal(PotKeys.Income, listed.Single(c => c.Id == salary.Id).Pot);
    }

    // A4
    [Theory]
    [InlineData(PotKeys.Savings)]
    [InlineData(PotKeys.Income)]
    [InlineData("Needs")]
    public async Task A_contract_only_takes_the_pots_needs_or_wants(string pot)
    {
        var person = await SignUpAsync();

        await AssertRefusedAsync(await SendAsync(HttpMethod.Post, Path, person, Rent(person) with { Pot = pot }),
            ErrorCodes.CommitmentPotInvalid);
    }

    [Fact]
    public async Task A_contract_without_pot_or_category_is_refused()
    {
        var person = await SignUpAsync();

        await AssertRefusedAsync(await SendAsync(HttpMethod.Post, Path, person, Rent(person) with { Pot = null }), ErrorCodes.PotRequired);
        await AssertRefusedAsync(await SendAsync(HttpMethod.Post, Path, person, Rent(person) with { CategoryId = null }), ErrorCodes.CategoryRequired);
    }

    [Fact]
    public async Task An_income_needs_an_income_category_and_other_types_another_one()
    {
        var person = await SignUpAsync();
        var salaryWithRent = new CreateCommitmentRequest(
            CommitmentTypes.Income, "Gehalt", 2450.00m, 1, October1, null, person.Rent, PotKeys.Income,
            PaymentMethod: PaymentMethods.Transfer);

        await AssertRefusedAsync(await SendAsync(HttpMethod.Post, Path, person, salaryWithRent), ErrorCodes.CommitmentCategoryInvalid);
        await AssertRefusedAsync(await SendAsync(HttpMethod.Post, Path, person, Rent(person) with { CategoryId = person.Salary }),
            ErrorCodes.CommitmentCategoryInvalid);
    }

    [Fact]
    public async Task An_unknown_or_foreign_category_is_refused()
    {
        var anna = await SignUpAsync();
        var ben = await SignUpAsync();
        var own = await SendAsync(HttpMethod.Post, "/api/v1/categories", anna, new SaveCategoryRequest("Fitness", "leisure", PotKeys.Wants));
        var annasCategory = (await own.Content.ReadFromJsonAsync<CategoryResponse>())!.Id;

        await AssertRefusedAsync(await SendAsync(HttpMethod.Post, Path, ben, Rent(ben) with { CategoryId = annasCategory }),
            ErrorCodes.CategoryNotFound);
        await AssertRefusedAsync(await SendAsync(HttpMethod.Post, Path, ben, Rent(ben) with { CategoryId = Guid.NewGuid() }),
            ErrorCodes.CategoryNotFound);
        Assert.Equal(annasCategory, (await CreateAsync(anna, Rent(anna) with { CategoryId = annasCategory, Pot = PotKeys.Wants })).CategoryId);
    }

    // A5
    [Fact]
    public async Task A_closed_paying_account_is_refused_and_no_account_means_the_default_one()
    {
        var person = await SignUpAsync();
        await AccountAsync(person, "Giro");
        var old = await AccountAsync(person, "Altes Konto");
        var closing = await SendAsync(HttpMethod.Patch, $"/api/v1/accounts/{old}", person, new UpdateAccountRequest(Active: false));
        Assert.Equal(HttpStatusCode.OK, closing.StatusCode);

        await AssertRefusedAsync(await SendAsync(HttpMethod.Post, Path, person, Rent(person) with { AccountId = old }),
            ErrorCodes.AccountClosed);
        var saved = await CreateAsync(person, Rent(person));

        Assert.Null(saved.AccountId);
    }

    [Fact]
    public async Task An_unknown_or_foreign_account_is_refused()
    {
        var anna = await SignUpAsync();
        var ben = await SignUpAsync();
        var annasGiro = await AccountAsync(anna, "Giro");

        await AssertRefusedAsync(await SendAsync(HttpMethod.Post, Path, ben, Rent(ben) with { AccountId = annasGiro }),
            ErrorCodes.AccountNotFound);
        await AssertRefusedAsync(await SendAsync(HttpMethod.Post, Path, ben, Rent(ben) with { CounterAccountId = annasGiro }),
            ErrorCodes.AccountNotFound);
    }

    // A6
    [Fact]
    public async Task A_savings_goal_can_move_money_to_an_own_target_account_but_not_to_the_paying_one()
    {
        var person = await SignUpAsync();
        var giro = await AccountAsync(person, "Giro");
        var savingsAccount = await AccountAsync(person, "Sparkonto", AccountTypes.Savings);

        var response = await SendAsync(HttpMethod.Post, Path, person,
            SavingsRate(person) with { AccountId = giro, CounterAccountId = savingsAccount });
        var same = await SendAsync(HttpMethod.Post, Path, person,
            SavingsRate(person) with { AccountId = giro, CounterAccountId = giro });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(savingsAccount, (await response.Content.ReadFromJsonAsync<CommitmentResponse>())!.CounterAccountId);
        await AssertRefusedAsync(same, ErrorCodes.CommitmentTargetSameAccount);
    }

    [Fact]
    public async Task Without_paying_account_the_target_must_differ_from_the_default_account()
    {
        var person = await SignUpAsync();
        var giro = await AccountAsync(person, "Giro");

        await AssertRefusedAsync(await SendAsync(HttpMethod.Post, Path, person, SavingsRate(person) with { CounterAccountId = giro }),
            ErrorCodes.CommitmentTargetSameAccount);
    }

    [Fact]
    public async Task An_income_cannot_have_a_target_account()
    {
        var person = await SignUpAsync();
        await AccountAsync(person, "Giro");
        var savingsAccount = await AccountAsync(person, "Sparkonto", AccountTypes.Savings);
        var salary = new CreateCommitmentRequest(
            CommitmentTypes.Income, "Gehalt", 2450.00m, 1, October1, null, person.Salary, PotKeys.Income,
            CounterAccountId: savingsAccount, PaymentMethod: PaymentMethods.Transfer);

        await AssertRefusedAsync(await SendAsync(HttpMethod.Post, Path, person, salary), ErrorCodes.CommitmentTargetInvalid);
    }

    // A7
    [Fact]
    public async Task Pass_through_is_kept_and_off_without_the_field()
    {
        var person = await SignUpAsync();

        var passed = await CreateAsync(person, Rent(person) with { Name = "Weitergereicht", PassThrough = true });
        var plain = await CreateAsync(person, Rent(person));

        var listed = await ListAsync(person);
        Assert.True(listed.Single(c => c.Id == passed.Id).PassThrough);
        Assert.False(listed.Single(c => c.Id == plain.Id).PassThrough);
    }

    // A8
    [Theory]
    [InlineData(null)]
    [InlineData("cheque")]
    [InlineData("DirectDebit")]
    public async Task A_missing_or_unknown_payment_method_is_refused(string? method)
    {
        var person = await SignUpAsync();

        await AssertRefusedAsync(await SendAsync(HttpMethod.Post, Path, person, Rent(person) with { PaymentMethod = method }),
            ErrorCodes.PaymentMethodInvalid);
    }

    [Fact]
    public async Task Every_payment_method_key_is_accepted()
    {
        var person = await SignUpAsync();

        foreach (var method in PaymentMethods.All)
        {
            Assert.Equal(method, (await CreateAsync(person, Rent(person) with { PaymentMethod = method })).PaymentMethod);
        }
    }

    // A9
    [Fact]
    public async Task A_savings_goal_keeps_target_amount_and_date_and_a_contract_refuses_them()
    {
        var person = await SignUpAsync();

        var goal = await CreateAsync(person, SavingsRate(person) with { TargetAmount = 3000.00m, TargetDate = new DateOnly(2027, 7, 31) });
        var contract = await SendAsync(HttpMethod.Post, Path, person, Rent(person) with { TargetAmount = 3000.00m });
        var contractWithDate = await SendAsync(HttpMethod.Post, Path, person, Rent(person) with { TargetDate = new DateOnly(2027, 7, 31) });

        Assert.Equal(3000.00m, goal.TargetAmount);
        Assert.Equal(new DateOnly(2027, 7, 31), goal.TargetDate);
        await AssertRefusedAsync(contract, ErrorCodes.CommitmentTargetOnlyForSavingsGoal);
        await AssertRefusedAsync(contractWithDate, ErrorCodes.CommitmentTargetOnlyForSavingsGoal);
    }

    [Fact]
    public async Task A_target_amount_of_zero_is_refused()
    {
        var person = await SignUpAsync();

        await AssertRefusedAsync(await SendAsync(HttpMethod.Post, Path, person, SavingsRate(person) with { TargetAmount = 0m }),
            ErrorCodes.CommitmentTargetAmountInvalid);
    }

    // A10
    [Fact]
    public async Task Ending_a_monthly_contract_sets_the_last_payment_of_this_month_and_hides_it_after_that_day()
    {
        var person = await SignUpAsync();
        var rent = await CreateAsync(person, Rent(person, firstDue: new DateOnly(2026, 1, 1)));

        var response = await SendAsync(HttpMethod.Post, $"{Path}/{rent.Id}/end", person);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ended = (await response.Content.ReadFromJsonAsync<CommitmentResponse>())!;
        Assert.Equal(new DateOnly(2026, 10, 1), ended.LastDueDate);
        Assert.False(ended.Running);
        Assert.Null(ended.NextDueDate);
        Assert.Empty(await ListAsync(person));
        Assert.Equal(rent.Id, Assert.Single(await ListAsync(person, includeEnded: true)).Id);
    }

    // A11
    [Fact]
    public async Task Ending_before_the_first_payment_sets_the_day_before_it_and_ends_at_once()
    {
        var person = await SignUpAsync();
        var contract = await CreateAsync(person, Rent(person, firstDue: new DateOnly(2026, 12, 15)));

        var response = await SendAsync(HttpMethod.Post, $"{Path}/{contract.Id}/end", person);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ended = (await response.Content.ReadFromJsonAsync<CommitmentResponse>())!;
        Assert.Equal(new DateOnly(2026, 12, 14), ended.LastDueDate);
        Assert.False(ended.Running);
        Assert.Empty(await ListAsync(person));
        var listed = Assert.Single(await ListAsync(person, includeEnded: true));
        Assert.Equal(contract.Id, listed.Id);
        Assert.False(listed.Running);
    }

    // A12
    [Fact]
    public async Task A_contract_cannot_be_deleted()
    {
        var person = await SignUpAsync();
        var rent = await CreateAsync(person, Rent(person));

        var response = await SendAsync(HttpMethod.Delete, $"{Path}/{rent.Id}", person);

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        Assert.Equal(rent.Id, Assert.Single(await ListAsync(person)).Id);
    }

    // A13
    [Fact]
    public async Task Another_person_can_neither_change_nor_end_nor_see_a_contract()
    {
        var anna = await SignUpAsync();
        var ben = await SignUpAsync();
        var rent = await CreateAsync(anna, Rent(anna));

        var patch = await SendAsync(HttpMethod.Patch, $"{Path}/{rent.Id}", ben, new UpdateCommitmentRequest(Name: "Gekapert"));
        var end = await SendAsync(HttpMethod.Post, $"{Path}/{rent.Id}/end", ben);

        foreach (var response in new[] { patch, end })
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(ErrorCodes.CommitmentNotFound, await response.ReadCodeAsync());
        }
        Assert.Empty(await ListAsync(ben, includeEnded: true));
        var annas = Assert.Single(await ListAsync(anna));
        Assert.Equal("Miete", annas.Name);
        Assert.Null(annas.LastDueDate);
    }

    [Fact]
    public async Task Patch_changes_only_the_fields_it_names()
    {
        var person = await SignUpAsync();
        var rent = await CreateAsync(person, Rent(person));

        var response = await SendAsync(HttpMethod.Patch, $"{Path}/{rent.Id}", person,
            new UpdateCommitmentRequest(Amount: 920.00m, LastDueDate: new DateOnly(2027, 12, 1)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<CommitmentResponse>())!;
        Assert.Equal(920.00m, saved.Amount);
        Assert.Equal(new DateOnly(2027, 12, 1), saved.LastDueDate);
        Assert.Equal("Miete", saved.Name);
        Assert.Equal(PaymentMethods.StandingOrder, saved.PaymentMethod);
    }

    [Fact]
    public async Task Patch_clears_the_last_payment_and_the_accounts_with_their_switches()
    {
        var person = await SignUpAsync();
        var giro = await AccountAsync(person, "Giro");
        var savingsAccount = await AccountAsync(person, "Sparkonto", AccountTypes.Savings);
        var goal = await CreateAsync(person, SavingsRate(person) with
        {
            AccountId = giro, CounterAccountId = savingsAccount, LastDueDate = new DateOnly(2027, 1, 1),
            TargetAmount = 3000m, TargetDate = new DateOnly(2027, 7, 31),
        });

        var response = await SendAsync(HttpMethod.Patch, $"{Path}/{goal.Id}", person, new UpdateCommitmentRequest(
            ClearLastDueDate: true, ClearAccountId: true, ClearCounterAccountId: true, ClearTarget: true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<CommitmentResponse>())!;
        Assert.Null(saved.LastDueDate);
        Assert.Null(saved.AccountId);
        Assert.Null(saved.CounterAccountId);
        Assert.Null(saved.TargetAmount);
        Assert.Null(saved.TargetDate);
    }

    [Fact]
    public async Task A_value_sent_with_its_clear_switch_wins_so_the_target_amount_can_go_and_the_date_stay()
    {
        var person = await SignUpAsync();
        var goal = await CreateAsync(person, SavingsRate(person) with { TargetAmount = 3000m, TargetDate = new DateOnly(2027, 7, 31) });

        var response = await SendAsync(HttpMethod.Patch, $"{Path}/{goal.Id}", person,
            new UpdateCommitmentRequest(TargetDate: new DateOnly(2027, 7, 31), ClearTarget: true));

        var saved = (await response.Content.ReadFromJsonAsync<CommitmentResponse>())!;
        Assert.Null(saved.TargetAmount);
        Assert.Equal(new DateOnly(2027, 7, 31), saved.TargetDate);
    }

    [Fact]
    public async Task Patch_checks_the_whole_contract_after_the_change()
    {
        var person = await SignUpAsync();
        var goal = await CreateAsync(person, SavingsRate(person) with { TargetAmount = 3000m });

        // Als gewöhnlicher Vertrag passen weder der Topf Sparen noch der Zielbetrag.
        var toContract = await SendAsync(HttpMethod.Patch, $"{Path}/{goal.Id}", person,
            new UpdateCommitmentRequest(Type: CommitmentTypes.Contract, CategoryId: person.Rent));
        var withPot = await SendAsync(HttpMethod.Patch, $"{Path}/{goal.Id}", person,
            new UpdateCommitmentRequest(Type: CommitmentTypes.Contract, CategoryId: person.Rent, Pot: PotKeys.Needs));
        var cleared = await SendAsync(HttpMethod.Patch, $"{Path}/{goal.Id}", person,
            new UpdateCommitmentRequest(Type: CommitmentTypes.Contract, CategoryId: person.Rent, Pot: PotKeys.Needs, ClearTarget: true));

        await AssertRefusedAsync(toContract, ErrorCodes.CommitmentPotInvalid);
        await AssertRefusedAsync(withPot, ErrorCodes.CommitmentTargetOnlyForSavingsGoal);
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Equal(CommitmentTypes.Contract, (await cleared.Content.ReadFromJsonAsync<CommitmentResponse>())!.Type);
    }

    [Fact]
    public async Task A_contract_on_an_account_closed_later_can_still_be_renamed()
    {
        var person = await SignUpAsync();
        await AccountAsync(person, "Giro");
        var card = await AccountAsync(person, "Karte");
        var gym = await CreateAsync(person, Rent(person) with { Name = "Fitness", AccountId = card });
        await SendAsync(HttpMethod.Patch, $"/api/v1/accounts/{card}", person, new UpdateAccountRequest(Active: false));

        var response = await SendAsync(HttpMethod.Patch, $"{Path}/{gym.Id}", person, new UpdateCommitmentRequest(Name: "Fitnessstudio"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_list_is_sorted_by_next_due_date_then_name()
    {
        var person = await SignUpAsync();
        await CreateAsync(person, Rent(person, firstDue: new DateOnly(2026, 1, 28)) with { Name = "Strom" });
        await CreateAsync(person, Rent(person, firstDue: new DateOnly(2026, 1, 1)) with { Name = "Miete" });
        await CreateAsync(person, Rent(person, firstDue: new DateOnly(2026, 1, 1)) with { Name = "Abo" });
        var ended = await CreateAsync(person, Rent(person, firstDue: new DateOnly(2026, 1, 1)) with { Name = "Altvertrag" });
        await SendAsync(HttpMethod.Post, $"{Path}/{ended.Id}/end", person);

        var names = (await ListAsync(person, includeEnded: true)).Select(c => c.Name).ToList();

        // Strom am 28. Oktober, Abo und Miete am 1. November, der beendete ohne nächste Zahlung zuletzt.
        Assert.Equal(["Strom", "Abo", "Miete", "Altvertrag"], names);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
