using System.Net;
using System.Net.Http.Json;
using Duofy.Domain;
using Duofy.Schemas;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static Duofy.Api.Tests.HouseholdDriver;

namespace Duofy.Api.Tests;

/// <summary>
/// Schulden und Kredite über die API mit echter Datenbank (Issue 31): anlegen, Gezahltes und Offenes aus zugeordneten
/// Ausgaben, Rechte im Bereich Sparen, Rate im Plan. "Heute" ist der 24. Oktober 2026 in Berlin.
/// </summary>
[Collection(ApiCollection.Name)]
public class DebtTests : IDisposable
{
    private const string Debts = "/api/v1/debts";
    private const string Bookings = "/api/v1/bookings";
    private const string Commitments = "/api/v1/commitments";
    private const string Plans = "/api/v1/plans";

    private static readonly DateOnly Today = new(2026, 10, 24);
    private static readonly Guid Groceries = CategoryCatalog.IdFor("household.groceries");
    private static readonly Guid Rent = CategoryCatalog.IdFor("housing.rent");

    private readonly ApiFixture fixture;
    private readonly WebApplicationFactory<Program> api;
    private readonly HttpClient client;

    public DebtTests(ApiFixture fixture)
    {
        this.fixture = fixture;
        var now = new FixedTime(new DateTimeOffset(2026, 10, 24, 8, 0, 0, TimeSpan.Zero));
        api = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<TimeProvider>(now)));
        client = api.CreateClient();
    }

    public void Dispose()
    {
        client.Dispose();
        api.Dispose();
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private async Task<Guid> GiroAsync(Person owner)
    {
        var response = await client.SendAsync(owner, HttpMethod.Post, "/api/v1/accounts",
            new CreateAccountRequest("Giro", AccountTypes.Checking, 5000m, new DateOnly(2026, 1, 1)));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountResponse>())!.Id;
    }

    private async Task<DebtResponse> DebtAsync(Person owner, string kind = DebtKinds.Claim, string name = "Stromrechnung", decimal amount = 300m)
    {
        var response = await client.SendAsync(owner, HttpMethod.Post, Debts, new CreateDebtRequest(kind, name, "Stadtwerke", amount));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DebtResponse>())!;
    }

    private async Task<DebtResponse> DebtOfAsync(Person owner, Guid id) =>
        (await (await client.SendAsync(owner, HttpMethod.Get, Debts)).Content.ReadFromJsonAsync<List<DebtResponse>>())!.Single(d => d.Id == id);

    private static SaveBookingRequest Payment(Guid account, decimal amount, Guid? debt, string pot = PotKeys.Needs) =>
        new(BookingKinds.Expense, account, null, Today, amount, null, [new SaveBookingLine(amount, pot, Groceries, DebtId: debt)]);

    private Task<HttpResponseMessage> BookAsync(Person owner, SaveBookingRequest request) =>
        client.SendAsync(owner, HttpMethod.Post, Bookings, request);

    [Fact]
    public async Task A_claim_and_a_loan_are_created_and_show_what_is_open_and_to_whom()
    {
        var (anna, _) = await client.AnnaAndBenAsync();

        var claim = await DebtAsync(anna, DebtKinds.Claim, "Stromrechnung", 300m);
        var loan = await DebtAsync(anna, DebtKinds.Loan, "Autokredit", 5000m);

        Assert.Equal((DebtKinds.Claim, "Stadtwerke", 300m, 0m, 300m, false), (claim.Kind, claim.Creditor, claim.OriginalAmount, claim.Paid, claim.Open, claim.Settled));
        var list = (await (await client.SendAsync(anna, HttpMethod.Get, Debts)).Content.ReadFromJsonAsync<List<DebtResponse>>())!;
        Assert.Equal(["Autokredit", "Stromrechnung"], list.Select(d => d.Name));
        Assert.Equal(DebtKinds.Loan, list[0].Kind);
        Assert.Equal(loan.Id, list[0].Id);
    }

    [Theory]
    [InlineData("", "Stadtwerke", 100, ErrorCodes.DebtNameRequired)]
    [InlineData("Rechnung", "  ", 100, ErrorCodes.DebtCreditorRequired)]
    [InlineData("Rechnung", "Stadtwerke", 0, ErrorCodes.DebtAmountInvalid)]
    [InlineData("Rechnung", "Stadtwerke", -5, ErrorCodes.DebtAmountInvalid)]
    [InlineData("Rechnung", "Stadtwerke", 1.005, ErrorCodes.DebtAmountInvalid)]
    public async Task Invalid_input_answers_400_with_its_code(string name, string creditor, double amount, string code)
    {
        var (anna, _) = await client.AnnaAndBenAsync();

        var response = await client.SendAsync(anna, HttpMethod.Post, Debts, new CreateDebtRequest(DebtKinds.Claim, name, creditor, (decimal)amount));

        Assert.Equal((HttpStatusCode.BadRequest, code), (response.StatusCode, await response.ReadCodeAsync()));
    }

    [Fact]
    public async Task An_unknown_kind_and_too_long_texts_are_refused()
    {
        var (anna, _) = await client.AnnaAndBenAsync();

        var kind = await client.SendAsync(anna, HttpMethod.Post, Debts, new CreateDebtRequest("gift", "A", "B", 10m));
        var name = await client.SendAsync(anna, HttpMethod.Post, Debts, new CreateDebtRequest(DebtKinds.Claim, new string('x', 101), "B", 10m));
        var note = await client.SendAsync(anna, HttpMethod.Post, Debts, new CreateDebtRequest(DebtKinds.Claim, "A", "B", 10m, Note: new string('n', 1001)));

        Assert.Equal(ErrorCodes.DebtKindInvalid, await kind.ReadCodeAsync());
        Assert.Equal(ErrorCodes.DebtNameTooLong, await name.ReadCodeAsync());
        Assert.Equal(ErrorCodes.DebtNoteTooLong, await note.ReadCodeAsync());
    }

    // Kriterium 2: Gezahltes und Offenes kommen aus den zugeordneten Ausgaben.
    [Fact]
    public async Task Payments_reduce_what_is_open_until_it_is_settled()
    {
        var (anna, _) = await client.AnnaAndBenAsync();
        var giro = await GiroAsync(anna);
        var debt = await DebtAsync(anna, amount: 300m);

        (await BookAsync(anna, Payment(giro, 100m, debt.Id))).EnsureSuccessStatusCode();
        var partly = await DebtOfAsync(anna, debt.Id);
        (await BookAsync(anna, Payment(giro, 250m, debt.Id, PotKeys.Wants))).EnsureSuccessStatusCode();
        var settled = await DebtOfAsync(anna, debt.Id);

        Assert.Equal((100m, 200m, false), (partly.Paid, partly.Open, partly.Settled));
        Assert.Equal((350m, 0m, true), (settled.Paid, settled.Open, settled.Settled));
    }

    [Fact]
    public async Task Only_expenses_can_be_payments_and_a_line_cannot_also_belong_to_a_goal()
    {
        var (anna, _) = await client.AnnaAndBenAsync();
        var giro = await GiroAsync(anna);
        var debt = await DebtAsync(anna);
        var income = new SaveBookingRequest(BookingKinds.Income, giro, null, Today, 50m, null,
            [new SaveBookingLine(50m, PotKeys.Income, CategoryCatalog.IdFor("income.earned"), DebtId: debt.Id)]);
        var goal = (await (await client.SendAsync(anna, HttpMethod.Post, "/api/v1/savings-goals", new CreateSavingsGoalRequest("Ziel"))).Content.ReadFromJsonAsync<SavingsGoalResponse>())!;
        var both = new SaveBookingRequest(BookingKinds.Expense, giro, null, Today, 20m, null,
            [new SaveBookingLine(20m, PotKeys.Savings, CategoryCatalog.IdFor("finance.savings"), SavingsGoalId: goal.Id, DebtId: debt.Id)]);

        var asIncome = await BookAsync(anna, income);
        var asBoth = await BookAsync(anna, both);

        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.DebtBookingInvalid), (asIncome.StatusCode, await asIncome.ReadCodeAsync()));
        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.DebtBookingInvalid), (asBoth.StatusCode, await asBoth.ReadCodeAsync()));
        Assert.Equal(0m, (await DebtOfAsync(anna, debt.Id)).Paid);
    }

    [Fact]
    public async Task A_booking_cannot_name_the_debt_of_another_person()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var annasDebt = await DebtAsync(anna);
        var bensGiro = await GiroAsync(ben);

        var response = await BookAsync(ben, Payment(bensGiro, 10m, annasDebt.Id));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_debt_with_payments_is_not_deleted_but_an_unused_one_is()
    {
        var (anna, _) = await client.AnnaAndBenAsync();
        var giro = await GiroAsync(anna);
        var used = await DebtAsync(anna, name: "Benutzt");
        var unused = await DebtAsync(anna, name: "Leer");
        (await BookAsync(anna, Payment(giro, 10m, used.Id))).EnsureSuccessStatusCode();

        var refused = await client.SendAsync(anna, HttpMethod.Delete, $"{Debts}/{used.Id}");
        var deleted = await client.SendAsync(anna, HttpMethod.Delete, $"{Debts}/{unused.Id}");

        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.DebtHasBookings), (refused.StatusCode, await refused.ReadCodeAsync()));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task An_entry_is_changed_and_its_due_date_and_note_can_be_cleared()
    {
        var (anna, _) = await client.AnnaAndBenAsync();
        var created = (await (await client.SendAsync(anna, HttpMethod.Post, Debts,
            new CreateDebtRequest(DebtKinds.Claim, "Rechnung", "Stadtwerke", 100m, new DateOnly(2026, 12, 1), "AZ 123"))).Content.ReadFromJsonAsync<DebtResponse>())!;

        var response = await client.SendAsync(anna, HttpMethod.Patch, $"{Debts}/{created.Id}",
            new UpdateDebtRequest(Creditor: "Inkasso GmbH", ClearDueDate: true, ClearNote: true));

        var changed = (await response.Content.ReadFromJsonAsync<DebtResponse>())!;
        Assert.Equal(("Inkasso GmbH", (DateOnly?)null, (string?)null), (changed.Creditor, changed.DueDate, changed.Note));
    }

    [Fact]
    public async Task Ben_sees_annas_debts_only_with_a_savings_grant_and_changes_them_only_with_edit()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var debt = await DebtAsync(anna);

        var without = await client.SendAsync(ben, HttpMethod.Get, $"{Debts}?owner={anna.Id}");
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Savings: AccessLevelKeys.View));
        var withView = await client.SendAsync(ben, HttpMethod.Get, $"{Debts}?owner={anna.Id}");
        var change = await client.SendAsync(ben, HttpMethod.Patch, $"{Debts}/{debt.Id}", new UpdateDebtRequest(Name: "X"));
        var create = await client.SendAsync(ben, HttpMethod.Post, Debts, new CreateDebtRequest(DebtKinds.Claim, "N", "G", 5m, OwnerId: anna.Id));

        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.OwnerNotFound), (without.StatusCode, await without.ReadCodeAsync()));
        Assert.Equal(debt.Id, Assert.Single((await withView.Content.ReadFromJsonAsync<List<DebtResponse>>())!).Id);
        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NoEditGranted), (change.StatusCode, await change.ReadCodeAsync()));
        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NoCreateGranted), (create.StatusCode, await create.ReadCodeAsync()));
    }

    [Fact]
    public async Task Changing_the_amount_or_deleting_a_payment_booking_needs_the_savings_area()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var giro = await GiroAsync(anna);
        var debt = await DebtAsync(anna);
        var booking = (await (await BookAsync(anna, Payment(giro, 80m, debt.Id))).Content.ReadFromJsonAsync<BookingResponse>())!;
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Book: AccessLevelKeys.Delete, Accounts: AccessLevelKeys.View));

        var otherAmount = await client.SendAsync(ben, HttpMethod.Put, $"{Bookings}/{booking.Id}", Payment(giro, 60m, debt.Id));
        var unassign = await client.SendAsync(ben, HttpMethod.Put, $"{Bookings}/{booking.Id}", Payment(giro, 80m, null));
        var delete = await client.SendAsync(ben, HttpMethod.Delete, $"{Bookings}/{booking.Id}");

        Assert.Equal(HttpStatusCode.NotFound, otherAmount.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unassign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal(80m, (await DebtOfAsync(anna, debt.Id)).Paid);
    }

    // Kriterium 4: Zahlungen darauf werden im Plan berücksichtigt.
    private async Task<(Person Anna, DebtResponse Debt, CommitmentResponse Rate)> RateOnDebtAsync(decimal amount)
    {
        var anna = await client.FounderAsync("Anna");
        var giro = await GiroAsync(anna);
        var debt = await DebtAsync(anna, DebtKinds.Loan, "Kredit", amount);
        var request = new CreateCommitmentRequest(CommitmentTypes.Contract, "Rate Kredit", 100m, 1, new DateOnly(2026, 11, 1), null,
            Rent, PotKeys.Needs, AccountId: giro, PaymentMethod: PaymentMethods.StandingOrder, DebtId: debt.Id);
        var response = await client.SendAsync(anna, HttpMethod.Post, Commitments, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (anna, debt, (await response.Content.ReadFromJsonAsync<CommitmentResponse>())!);
    }

    private async Task<PlanResponse> PlanAsync(Person person, int year, int month) =>
        (await (await client.SendAsync(person, HttpMethod.Get, $"{Plans}/{year}/{month}")).Content.ReadFromJsonAsync<PlanResponse>())!;

    [Fact]
    public async Task Ticking_the_rate_in_the_plan_pays_on_the_debt()
    {
        var (anna, debt, rate) = await RateOnDebtAsync(500m);
        Assert.Equal(debt.Id, rate.DebtId);
        var position = Assert.Single((await PlanAsync(anna, 2026, 11)).Positions);

        (await client.SendAsync(anna, HttpMethod.Post, $"{Plans}/2026/11/positions/{position.Id}/tick", new TickPositionRequest())).EnsureSuccessStatusCode();

        var seen = await DebtOfAsync(anna, debt.Id);
        Assert.Equal((100m, 400m), (seen.Paid, seen.Open));
        (await client.SendAsync(anna, HttpMethod.Delete, $"{Plans}/2026/11/positions/{position.Id}/tick")).EnsureSuccessStatusCode();
        Assert.Equal(0m, (await DebtOfAsync(anna, debt.Id)).Paid);
    }

    [Fact]
    public async Task Once_the_debt_is_paid_the_plan_no_longer_asks_for_the_rate()
    {
        var (anna, debt, _) = await RateOnDebtAsync(100m);
        Assert.Single((await PlanAsync(anna, 2026, 11)).Positions);
        var giro = await GiroAsync(anna);
        (await BookAsync(anna, Payment(giro, 100m, debt.Id))).EnsureSuccessStatusCode();

        var december = await PlanAsync(anna, 2026, 12);
        var november = await PlanAsync(anna, 2026, 11);

        Assert.Empty(december.Positions);
        Assert.Equal(PlanChangeKinds.NoLongerDue, Assert.Single(november.Changes).Kind);
    }

    [Fact]
    public async Task A_contract_with_a_debt_must_be_an_expense_without_target_account_or_goal()
    {
        var (anna, _) = await client.AnnaAndBenAsync();
        var giro = await GiroAsync(anna);
        var other = (await (await client.SendAsync(anna, HttpMethod.Post, "/api/v1/accounts",
            new CreateAccountRequest("Spar", AccountTypes.Savings, 0m, new DateOnly(2026, 1, 1)))).Content.ReadFromJsonAsync<AccountResponse>())!.Id;
        var debt = await DebtAsync(anna);
        CreateCommitmentRequest With(Guid? counter) => new(
            CommitmentTypes.Contract, "Rate", 50m, 1, new DateOnly(2026, 11, 1), null, Rent, PotKeys.Needs,
            AccountId: giro, CounterAccountId: counter, PaymentMethod: PaymentMethods.StandingOrder, DebtId: debt.Id);

        var withTarget = await client.SendAsync(anna, HttpMethod.Post, Commitments, With(other));

        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.CommitmentDebtInvalid), (withTarget.StatusCode, await withTarget.ReadCodeAsync()));
    }

    [Fact]
    public async Task Linking_a_debt_to_a_contract_needs_the_savings_area_and_the_debt_of_the_same_person()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var giro = await GiroAsync(anna);
        var bensDebt = await DebtAsync(ben, name: "Bens");
        var annasRequest = new CreateCommitmentRequest(CommitmentTypes.Contract, "Rate", 50m, 1, new DateOnly(2026, 11, 1), null, Rent, PotKeys.Needs,
            AccountId: giro, PaymentMethod: PaymentMethods.StandingOrder, DebtId: bensDebt.Id);

        var foreign = await client.SendAsync(anna, HttpMethod.Post, Commitments, annasRequest);

        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.DebtNotFound), (foreign.StatusCode, await foreign.ReadCodeAsync()));
    }
}
