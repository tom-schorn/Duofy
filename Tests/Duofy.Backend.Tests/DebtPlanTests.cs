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
/// Der Ratenplan einer Schuld über die API mit echter Datenbank (Issue 137, Spec 137-debt-installment-plan).
/// "Heute" ist der 15. Dezember 2026 in Berlin.
/// </summary>
[Collection(ApiCollection.Name)]
public class DebtPlanTests : IDisposable
{
    private const string Debts = "/api/v1/debts";
    private const string Bookings = "/api/v1/bookings";
    private const string Commitments = "/api/v1/commitments";
    private const string Plans = "/api/v1/plans";

    private static readonly Guid Debt = CategoryCatalog.IdFor("finance.debt");
    private static readonly Guid Rent = CategoryCatalog.IdFor("housing.rent");

    private readonly WebApplicationFactory<Program> api;
    private readonly HttpClient client;

    public DebtPlanTests(ApiFixture fixture)
    {
        var now = new FixedTime(new DateTimeOffset(2026, 12, 15, 8, 0, 0, TimeSpan.Zero));
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

    private async Task<DebtResponse> DebtAsync(Person owner, decimal amount, DebtPlanRequest? plan = null, string? reference = null, string? note = null)
    {
        var response = await client.SendAsync(owner, HttpMethod.Post, Debts,
            new CreateDebtRequest(DebtKinds.Claim, "Ausgleich Zusammenveranlagung", "Insolvenzverwalter", amount, Note: note, Reference: reference, Plan: plan));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DebtResponse>())!;
    }

    private async Task<DebtResponse> DebtOfAsync(Person owner, Guid id) =>
        (await (await client.SendAsync(owner, HttpMethod.Get, Debts)).Content.ReadFromJsonAsync<List<DebtResponse>>())!.Single(d => d.Id == id);

    private static DebtPlanRequest PlanOf(Guid giro, decimal rate = 50m, DateOnly? first = null, decimal? last = null, DateOnly? from = null) =>
        new(rate, 1, first ?? new DateOnly(2026, 12, 1), giro, PaymentMethods.DirectDebit, Debt, last, from);

    private Task<HttpResponseMessage> PayAsync(Person owner, Guid giro, Guid debt, decimal amount, DateOnly date) =>
        client.SendAsync(owner, HttpMethod.Post, Bookings, new SaveBookingRequest(
            BookingKinds.Expense, giro, null, date, amount, null, [new SaveBookingLine(amount, PotKeys.Savings, Debt, DebtId: debt)]));

    private async Task<PlanResponse> PlanAsync(Person person, int year, int month) =>
        (await (await client.SendAsync(person, HttpMethod.Get, $"{Plans}/{year}/{month}")).Content.ReadFromJsonAsync<PlanResponse>())!;

    private async Task<List<CommitmentResponse>> ContractsAsync(Person person) =>
        (await (await client.SendAsync(person, HttpMethod.Get, $"{Commitments}?includeEnded=true")).Content.ReadFromJsonAsync<List<CommitmentResponse>>())!;

    // A1 (R1): das echte Beispiel aus der Spec.
    [Fact]
    public async Task The_real_example_creates_one_debt_contract_in_savings_with_a_last_installment_of_36_94()
    {
        var anna = await client.FounderAsync("Anna");
        var giro = await GiroAsync(anna);
        var debt = await DebtAsync(anna, 786.94m);
        (await PayAsync(anna, giro, debt.Id, 400m, new DateOnly(2026, 11, 20))).EnsureSuccessStatusCode();

        var response = await client.SendAsync(anna, HttpMethod.Patch, $"{Debts}/{debt.Id}", new UpdateDebtRequest(Plan: PlanOf(giro)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var contract = Assert.Single(await ContractsAsync(anna));
        Assert.Equal((CommitmentTypes.Debt, PotKeys.Savings, debt.Id, 50m), (contract.Type, contract.Pot, contract.DebtId, contract.Amount));
        Assert.Equal((36.94m, new DateOnly(2027, 7, 1), new DateOnly(2026, 12, 1)), (contract.LastAmount, contract.LastDueDate, contract.FirstDueDate));
        var read = await DebtOfAsync(anna, debt.Id);
        Assert.Equal(contract.Id, read.Plan!.CommitmentId);
    }

    [Fact]
    public async Task A_last_installment_set_by_hand_is_used_and_the_dates_follow_from_it()
    {
        var anna = await client.FounderAsync("Anna");
        var giro = await GiroAsync(anna);
        var debt = await DebtAsync(anna, 386.94m, PlanOf(giro, last: 40m));

        var contract = Assert.Single(await ContractsAsync(anna));
        Assert.Equal((40m, new DateOnly(2027, 7, 1)), (contract.LastAmount, contract.LastDueDate));
        Assert.Equal(contract.Id, (await DebtOfAsync(anna, debt.Id)).Plan!.CommitmentId);
    }

    [Fact]
    public async Task More_than_600_installments_are_refused()
    {
        var anna = await client.FounderAsync("Anna");
        var giro = await GiroAsync(anna);

        var response = await client.SendAsync(anna, HttpMethod.Post, Debts,
            new CreateDebtRequest(DebtKinds.Loan, "Kredit", "Bank", 30_001m, Plan: PlanOf(giro)));

        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.DebtPlanTooManyInstallments), (response.StatusCode, await response.ReadCodeAsync()));
    }

    // A3 (R1): die letzte Rate steht im Plan des letzten Monats, die früheren haben die volle Rate.
    [Fact]
    public async Task The_last_month_has_the_last_amount_and_earlier_months_the_normal_amount()
    {
        var anna = await client.FounderAsync("Anna");
        var giro = await GiroAsync(anna);
        await DebtAsync(anna, 386.94m, PlanOf(giro));

        Assert.Equal(50m, Assert.Single((await PlanAsync(anna, 2027, 6)).Positions).AmountPlanned);
        Assert.Equal(36.94m, Assert.Single((await PlanAsync(anna, 2027, 7)).Positions).AmountPlanned);
        Assert.Empty((await PlanAsync(anna, 2027, 8)).Positions);
    }

    // A2 (R2): Abhaken bucht die Rate mit der Schuld, offen sinkt um 50 €.
    [Fact]
    public async Task Ticking_the_december_position_books_50_with_the_debt_link_and_lowers_the_open_amount()
    {
        var anna = await client.FounderAsync("Anna");
        var giro = await GiroAsync(anna);
        var debt = await DebtAsync(anna, 386.94m, PlanOf(giro));
        var position = Assert.Single((await PlanAsync(anna, 2026, 12)).Positions);
        Assert.Equal(PotKeys.Savings, position.Pot);

        (await client.SendAsync(anna, HttpMethod.Post, $"{Plans}/2026/12/positions/{position.Id}/tick", new TickPositionRequest())).EnsureSuccessStatusCode();

        var read = await DebtOfAsync(anna, debt.Id);
        Assert.Equal((50m, 336.94m), (read.Paid, read.Open));
    }

    // A5 (R4): bezahlt, offen, nächste Rate, voraussichtliches Ende.
    [Fact]
    public async Task The_debt_shows_paid_open_next_installment_and_expected_end()
    {
        var anna = await client.FounderAsync("Anna");
        var giro = await GiroAsync(anna);
        var debt = await DebtAsync(anna, 386.94m, PlanOf(giro));
        var position = Assert.Single((await PlanAsync(anna, 2026, 12)).Positions);
        (await client.SendAsync(anna, HttpMethod.Post, $"{Plans}/2026/12/positions/{position.Id}/tick", new TickPositionRequest())).EnsureSuccessStatusCode();

        var read = await DebtOfAsync(anna, debt.Id);

        Assert.Equal((50m, 336.94m), (read.Paid, read.Open));
        Assert.Equal((new DateOnly(2027, 1, 1), 50m), (read.NextInstallment!.Date, read.NextInstallment.Amount));
        Assert.Equal(new DateOnly(2027, 7, 1), read.ExpectedEnd);
    }

    [Fact]
    public async Task A_manual_open_amount_is_shown_with_its_date_and_counts_payments_after_that_date()
    {
        var anna = await client.FounderAsync("Anna");
        var giro = await GiroAsync(anna);
        var debt = await DebtAsync(anna, 786.94m);
        (await PayAsync(anna, giro, debt.Id, 100m, new DateOnly(2026, 10, 1))).EnsureSuccessStatusCode();

        (await client.SendAsync(anna, HttpMethod.Patch, $"{Debts}/{debt.Id}",
            new UpdateDebtRequest(AdjustedOpen: 500m, AdjustedOpenOn: new DateOnly(2026, 11, 30)))).EnsureSuccessStatusCode();
        (await PayAsync(anna, giro, debt.Id, 50m, new DateOnly(2026, 12, 1))).EnsureSuccessStatusCode();

        var read = await DebtOfAsync(anna, debt.Id);
        Assert.Equal((450m, 500m, new DateOnly(2026, 11, 30)), (read.Open, read.AdjustedOpen, read.AdjustedOpenOn));
    }

    // A6 (R5)
    [Fact]
    public async Task A_reference_and_a_note_of_1000_characters_are_saved_and_1001_are_refused()
    {
        var anna = await client.FounderAsync("Anna");
        var note = new string('n', 1000);

        var debt = await DebtAsync(anna, 100m, reference: "AZ 123/45", note: note);
        var tooLong = await client.SendAsync(anna, HttpMethod.Patch, $"{Debts}/{debt.Id}", new UpdateDebtRequest(Note: note + "n"));

        var read = await DebtOfAsync(anna, debt.Id);
        Assert.Equal(("AZ 123/45", note), (read.Reference, read.Note));
        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.DebtNoteTooLong), (tooLong.StatusCode, await tooLong.ReadCodeAsync()));
        var longReference = await client.SendAsync(anna, HttpMethod.Patch, $"{Debts}/{debt.Id}", new UpdateDebtRequest(Reference: new string('r', 51)));
        Assert.Equal(ErrorCodes.DebtReferenceTooLong, await longReference.ReadCodeAsync());
    }

    // A7 (R6)
    [Fact]
    public async Task Changing_the_rate_from_march_keeps_december_to_february_and_starts_a_new_contract()
    {
        var anna = await client.FounderAsync("Anna");
        var giro = await GiroAsync(anna);
        var debt = await DebtAsync(anna, 600m, PlanOf(giro));
        foreach (var (year, month) in new[] { (2026, 12), (2027, 1), (2027, 2) })
        {
            Assert.Equal(50m, Assert.Single((await PlanAsync(anna, year, month)).Positions).AmountPlanned);
        }

        var response = await client.SendAsync(anna, HttpMethod.Patch, $"{Debts}/{debt.Id}",
            new UpdateDebtRequest(Plan: PlanOf(giro, rate: 100m, first: new DateOnly(2027, 3, 1), from: new DateOnly(2027, 3, 1))));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contracts = await ContractsAsync(anna);
        var old = contracts.Single(c => c.Amount == 50m);
        var created = contracts.Single(c => c.Amount == 100m);
        Assert.Equal(new DateOnly(2027, 2, 1), old.LastDueDate);
        Assert.Equal((new DateOnly(2027, 3, 1), new DateOnly(2027, 7, 1)), (created.FirstDueDate, created.LastDueDate));
        foreach (var (year, month) in new[] { (2026, 12), (2027, 1), (2027, 2) })
        {
            Assert.Equal(50m, Assert.Single((await PlanAsync(anna, year, month)).Positions).AmountPlanned);
        }
        Assert.Equal(100m, Assert.Single((await PlanAsync(anna, 2027, 3)).Positions).AmountPlanned);
        Assert.Equal(created.Id, (await DebtOfAsync(anna, debt.Id)).Plan!.CommitmentId);
    }

    [Fact]
    public async Task Changing_an_existing_plan_without_a_month_is_refused()
    {
        var anna = await client.FounderAsync("Anna");
        var giro = await GiroAsync(anna);
        var debt = await DebtAsync(anna, 600m, PlanOf(giro));

        var response = await client.SendAsync(anna, HttpMethod.Patch, $"{Debts}/{debt.Id}", new UpdateDebtRequest(Plan: PlanOf(giro, rate: 100m)));

        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.DebtPlanFromMonthRequired), (response.StatusCode, await response.ReadCodeAsync()));
    }

    [Fact]
    public async Task Removing_the_plan_ends_the_contract_at_the_end_of_the_current_month()
    {
        var anna = await client.FounderAsync("Anna");
        var giro = await GiroAsync(anna);
        var debt = await DebtAsync(anna, 600m, PlanOf(giro));

        (await client.SendAsync(anna, HttpMethod.Patch, $"{Debts}/{debt.Id}", new UpdateDebtRequest(ClearPlan: true))).EnsureSuccessStatusCode();

        var contract = Assert.Single(await ContractsAsync(anna));
        Assert.Equal((new DateOnly(2026, 12, 1), null), (contract.LastDueDate, (await DebtOfAsync(anna, debt.Id)).Plan));
    }

    // A4 (R3)
    [Fact]
    public async Task A_contract_of_the_kind_debt_is_accepted_with_a_debt_link_and_a_contract_can_be_linked_from_the_debt()
    {
        var anna = await client.FounderAsync("Anna");
        var giro = await GiroAsync(anna);
        var debt = await DebtAsync(anna, 300m);
        var kindDebt = new CreateCommitmentRequest(CommitmentTypes.Debt, "Rate", 50m, 1, new DateOnly(2026, 12, 1), null, Debt, PotKeys.Savings,
            AccountId: giro, PaymentMethod: PaymentMethods.StandingOrder, DebtId: debt.Id);
        var normal = new CreateCommitmentRequest(CommitmentTypes.Contract, "Normale Rate", 10m, 1, new DateOnly(2026, 12, 1), null, Rent, PotKeys.Needs,
            AccountId: giro, PaymentMethod: PaymentMethods.StandingOrder);

        var accepted = await client.SendAsync(anna, HttpMethod.Post, Commitments, kindDebt);
        var other = (await (await client.SendAsync(anna, HttpMethod.Post, Commitments, normal)).Content.ReadFromJsonAsync<CommitmentResponse>())!;
        var other2 = await DebtAsync(anna, 200m);
        (await client.SendAsync(anna, HttpMethod.Patch, $"{Debts}/{other2.Id}", new UpdateDebtRequest(LinkedCommitmentId: other.Id))).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        var plan = (await DebtOfAsync(anna, other2.Id)).Plan!;
        Assert.Equal((other.Id, false), (plan.CommitmentId, plan.IsOwnPlan));
    }

    // A8 (R7)
    [Fact]
    public async Task The_contract_list_says_which_debt_a_plan_contract_belongs_to_and_the_contract_endpoint_refuses_changes()
    {
        var anna = await client.FounderAsync("Anna");
        var giro = await GiroAsync(anna);
        var debt = await DebtAsync(anna, 600m, PlanOf(giro));
        var contract = Assert.Single(await ContractsAsync(anna));

        var change = await client.SendAsync(anna, HttpMethod.Patch, $"{Commitments}/{contract.Id}", new UpdateCommitmentRequest(Amount: 70m));
        var end = await client.SendAsync(anna, HttpMethod.Post, $"{Commitments}/{contract.Id}/end");

        Assert.Equal((debt.Id, debt.Name), (contract.Debt!.Id, contract.Debt.Name));
        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.CommitmentIsDebtPlan), (change.StatusCode, await change.ReadCodeAsync()));
        Assert.Equal(HttpStatusCode.BadRequest, end.StatusCode);
    }

    [Fact]
    public async Task The_error_bodies_for_pot_and_debt_carry_a_german_explanation()
    {
        var anna = await client.FounderAsync("Anna");
        var giro = await GiroAsync(anna);
        var wrongPot = await client.SendAsync(anna, HttpMethod.Post, Commitments, new CreateCommitmentRequest(
            CommitmentTypes.Contract, "X", 5m, 1, new DateOnly(2026, 12, 1), null, Rent, PotKeys.Savings, AccountId: giro, PaymentMethod: PaymentMethods.StandingOrder));

        var body = (await wrongPot.Content.ReadFromJsonAsync<ErrorResponse>())!;

        Assert.Equal(ErrorCodes.CommitmentPotInvalid, body.Code);
        Assert.Contains("Sparen", body.Message);
    }

    // Rechte: wer Schulden ändern darf, legt den Plan für den Besitzer an; ohne Freigabe wie bei Schulden.
    [Fact]
    public async Task A_member_with_savings_edit_creates_the_plan_for_the_owner_and_without_it_is_refused()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var giro = await GiroAsync(anna);
        var debt = await DebtAsync(anna, 600m);

        var refused = await client.SendAsync(ben, HttpMethod.Patch, $"{Debts}/{debt.Id}", new UpdateDebtRequest(Plan: PlanOf(giro)));
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Savings: AccessLevelKeys.Edit));
        var allowed = await client.SendAsync(ben, HttpMethod.Patch, $"{Debts}/{debt.Id}", new UpdateDebtRequest(Plan: PlanOf(giro)));

        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Single(await ContractsAsync(anna));
    }
}
