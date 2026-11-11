using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Layout;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>Das Drucken im Speicher: zählt, wie oft die Seite drucken wollte.</summary>
public sealed class FakePrintService : IPrintService
{
    public int Calls { get; private set; }

    /// <summary>Läuft im Moment des Druckens: Dort steht das Druckbild noch auf der Seite.</summary>
    public Action? AtPrint { get; set; }

    public Task PrintAsync()
    {
        Calls++;
        AtPrint?.Invoke();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Spec 125: Der Haushaltsplan ist die Zentrale. Angemeldet ist Anna im Haushalt mit Jasmin; Jasmin hat Anna auf dem Plan
/// Anlegen und Ändern, bei den Buchungen aber nur Ansehen freigegeben. "Heute" ist der 21. Oktober 2026.
/// </summary>
public class HouseholdPlanWorkTests : PageTestContext
{
    private static readonly Guid JasminId = Guid.NewGuid();
    private const string Actions = ".page-head .page-actions ";

    private readonly IRenderedComponent<IComponent> dialogs;
    private readonly FakePrintService printing = new();
    private readonly HouseholdResponse household;
    private readonly HouseholdPlanPositionResponse strom;
    private readonly HouseholdPlanPositionResponse miete;

    public HouseholdPlanWorkTests()
    {
        Services.AddSingleton<IPrintService>(printing);
        dialogs = RenderDialogHost();
        Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true));
        household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
        [
            FakeHouseholdApi.Anna(isMe: true),
            new MemberResponse(JasminId, "Jasmin", "Muster", "jasmin@example.org", HouseholdRoles.Member, false,
                GrantLevels.None with { Plan = AccessLevelKeys.Edit, Book = AccessLevelKeys.View, Accounts = AccessLevelKeys.View }, GrantLevels.None),
        ]);
        HouseholdApiFake.Household = household;
        miete = FakeHouseholdApi.OwnedPosition("Miete", 890m, FakeHouseholdApi.AnnaId, "Anna");
        strom = FakeHouseholdApi.OwnedPosition("Strom", 80m, JasminId, "Jasmin", booked: 80m);
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10) with
        {
            Positions = [miete, strom],
            Pots = [new HouseholdPotResponse(PotKeys.Needs, 970m, 80m), new HouseholdPotResponse(PotKeys.Wants, 0m, 0m), new HouseholdPotResponse(PotKeys.Savings, 0m, 0m)],
            Income = 3000m,
        };
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with
        {
            Positions = [FakePlanApi.Position("Strom", 80m, booked: 80m) with { Id = strom.Id }],
        };
    }

    private ActiveMember Member => Services.GetRequiredService<ActiveMember>();

    private async Task<IRenderedComponent<Plan>> OpenHouseholdAsync()
    {
        await Member.LoadAsync();
        Member.ChooseHousehold();
        var cut = Render<Plan>();
        OpenPlanTab(cut);
        return cut;
    }

    private IRenderedComponent<Plan> OpenPersonal()
    {
        var cut = Render<Plan>();
        OpenPlanTab(cut);
        return cut;
    }

    private static IReadOnlyList<string> Buttons(IRenderedComponent<Plan> cut) =>
        cut.FindAll(Actions + "[data-testid]").Select(b => b.GetAttribute("data-testid")!).ToList();

    private IReadOnlyList<string> Choices() =>
        dialogs.FindAll("[data-testid^='person-choice-']").Select(c => c.GetAttribute("data-testid")!["person-choice-".Length..]).ToList();

    // A1
    [Fact]
    public async Task Both_plans_show_print_booking_and_position_in_the_same_toolbar_at_the_same_place()
    {
        var personal = OpenPersonal();
        var personalButtons = Buttons(personal);
        Assert.Single(personal.FindComponents<PlanHead>());

        var cut = await OpenHouseholdAsync();

        Assert.Single(cut.FindComponents<PlanHead>());
        Assert.Equal(["plan-print", "month-action-booking", "plan-add", "page-more"], Buttons(cut));
        Assert.Equal(personalButtons, Buttons(cut));
    }

    // A4, personal
    [Fact]
    public void Print_in_the_personal_plan_prints_the_month_at_once()
    {
        var cut = OpenPersonal();
        var subject = "";
        printing.AtPrint = () => subject = cut.Find("[data-testid='month-print'] .print-subject").TextContent;

        cut.Find("[data-testid='plan-print']").Click();

        cut.WaitForAssertion(() => Assert.Equal(1, printing.Calls));
        Assert.Contains("Meine Planung", subject);
    }

    // A4, household
    [Fact]
    public async Task Print_in_the_household_plan_asks_for_one_person_or_the_whole_household_and_prints_the_household()
    {
        var cut = await OpenHouseholdAsync();
        var subject = "";
        printing.AtPrint = () => subject = cut.Find("[data-testid='month-print'] .print-subject").TextContent;

        cut.Find("[data-testid='plan-print']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid^='person-choice-']")));
        Assert.Equal(["household", FakeHouseholdApi.AnnaId.ToString(), JasminId.ToString()], Choices());
        Assert.Equal(0, printing.Calls);
        dialogs.Find("[data-testid='person-choice-household']").Click();

        cut.WaitForAssertion(() => Assert.Equal(1, printing.Calls));
        Assert.Contains("Haushalt von Anna", subject);
    }

    [Fact]
    public async Task Print_for_one_person_prints_that_persons_plan_from_their_grant()
    {
        var cut = await OpenHouseholdAsync();
        var subject = "";
        printing.AtPrint = () => subject = cut.Find("[data-testid='month-print'] .print-subject").TextContent;

        cut.Find("[data-testid='plan-print']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid^='person-choice-']")));
        dialogs.Find($"[data-testid='person-choice-{JasminId}']").Click();

        cut.WaitForAssertion(() => Assert.Equal(1, printing.Calls));
        Assert.Contains("Planung von Jasmin", subject);
        Assert.Equal(JasminId, PlanApiFake.Owners.Last());
    }

    // A2
    [Fact]
    public async Task Adding_a_position_asks_for_the_person_first_and_the_position_belongs_to_the_chosen_one()
    {
        var cut = await OpenHouseholdAsync();

        cut.Find("[data-testid='plan-add']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid^='person-choice-']")));
        Assert.Equal([FakeHouseholdApi.AnnaId.ToString(), JasminId.ToString()], Choices());
        dialogs.Find($"[data-testid='person-choice-{JasminId}']").Click();

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='position-label']")));
        Field(dialogs, "position-label").Change("Geschenk");
        Field(dialogs, "position-amount").Change("50,00");
        Field(dialogs, "position-day").Change("20");
        dialogs.Find("[data-testid='position-category']").Change(FakeCategoryApi.Restaurant.Id.ToString());
        dialogs.Find("[data-testid='position-save']").Click();

        cut.WaitForAssertion(() => Assert.Single(PlanApiFake.CreatedPositions));
        Assert.Equal(JasminId, Assert.Single(PlanApiFake.WriteOwners));
        Assert.Null(Member.Owner);
        Assert.True(Member.IsHousehold);
    }

    [Fact]
    public async Task Adding_a_booking_offers_only_those_who_granted_creating_bookings()
    {
        var cut = await OpenHouseholdAsync();

        cut.Find("[data-testid='month-action-booking']").Click();

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid^='person-choice-']")));
        Assert.Equal([FakeHouseholdApi.AnnaId.ToString()], Choices());
    }

    // A3
    [Fact]
    public async Task Ticking_a_position_of_another_member_runs_as_that_member_under_their_grant()
    {
        HouseholdApiFake.Household = household with
        {
            Members = [household.Members[0], household.Members[1] with { GrantsToMe = household.Members[1].GrantsToMe with { Book = AccessLevelKeys.Delete } }],
        };
        var open = HouseholdApiFake.HouseholdPlans[(2026, 10)];
        var power = strom with { DoneAt = null };
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = open with { Positions = [miete, power] };
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with { Positions = [FakePlanApi.Position("Strom", 80m, booked: 80m) with { Id = power.Id }] };
        var cut = await OpenHouseholdAsync();

        cut.Find($"[data-testid='household-tick-{power.Id}']").Click();

        cut.WaitForAssertion(() => Assert.Equal(power.Id, Assert.Single(PlanApiFake.Ticks).Id));
        Assert.Equal(JasminId, Assert.Single(PlanApiFake.WriteOwners));
    }

    [Fact]
    public async Task Ticking_without_the_grant_is_refused_and_says_what_is_missing()
    {
        var cut = await OpenHouseholdAsync();

        var tick = cut.Find($"[data-testid='household-tick-{strom.Id}']");
        tick.Click();

        Assert.Equal("true", tick.GetAttribute("aria-disabled"));
        Assert.Empty(PlanApiFake.Ticks);
        Assert.Empty(PlanApiFake.Unticks);
        Assert.NotEmpty(cut.FindAll("[data-testid='grant-reason']"));
    }
}
