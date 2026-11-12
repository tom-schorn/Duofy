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

    /// <summary>Gesetzt, scheitert das Drucken damit.</summary>
    public Exception? Failure { get; set; }

    public Task PrintAsync()
    {
        Calls++;
        if (Failure is not null)
        {
            throw Failure;
        }
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
    private HouseholdResponse household;
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
        Assert.Contains("Neuer Posten für Jasmin", dialogs.Find(".step-dialog-title").TextContent);
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

    // --- Hilfen ---

    private void Grant(GrantLevels levels)
    {
        household = household with { Members = [household.Members[0], household.Members[1] with { GrantsToMe = levels }] };
        HouseholdApiFake.Household = household;
    }

    private static GrantLevels Levels(string plan = AccessLevelKeys.None, string book = AccessLevelKeys.None, string accounts = AccessLevelKeys.None) =>
        GrantLevels.None with { Plan = plan, Book = book, Accounts = accounts };

    // Ein Posten von Jasmin ohne Buchungen, offen oder abgehakt, im Haushaltsplan und in ihrem eigenen Monat.
    private HouseholdPlanPositionResponse AddJasminPosition(string label = "Wasser", bool done = false)
    {
        var position = FakeHouseholdApi.OwnedPosition(label, 30m, JasminId, "Jasmin", done: done);
        var plan = HouseholdApiFake.HouseholdPlans[(2026, 10)];
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = plan with { Positions = [.. plan.Positions, position] };
        var own = PlanApiFake.Plans[(2026, 10)];
        PlanApiFake.Plans[(2026, 10)] = own with { Positions = [.. own.Positions, FakePlanApi.Position(label, 30m) with { Id = position.Id, DoneAt = position.DoneAt }] };
        return position;
    }

    private string Notice(IRenderedComponent<Plan> cut) => cut.Find("[data-testid='notice']").TextContent;

    // --- Person als erster Schritt (R7, A7) ---

    [Fact]
    public async Task Adding_a_booking_skips_the_person_step_when_only_oneself_is_possible()
    {
        var cut = await OpenHouseholdAsync();

        cut.Find("[data-testid='month-action-booking']").Click();

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='booking-amount']")));
        Assert.Empty(dialogs.FindAll("[data-testid^='person-choice-']"));
        Assert.Contains("Neue Buchung", dialogs.Find(".step-dialog-title").TextContent);
    }

    [Fact]
    public async Task Adding_a_booking_asks_for_the_person_in_step_one_and_names_her_in_the_title()
    {
        Grant(Levels(plan: AccessLevelKeys.Edit, book: AccessLevelKeys.Create, accounts: AccessLevelKeys.View));
        var cut = await OpenHouseholdAsync();

        cut.Find("[data-testid='month-action-booking']").Click();

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid^='person-choice-']")));
        Assert.Equal([FakeHouseholdApi.AnnaId.ToString(), JasminId.ToString()], Choices());
        Assert.Contains("Für wen?", dialogs.Find("[data-testid='step-dialog-progress']").TextContent);
        dialogs.Find($"[data-testid='person-choice-{JasminId}']").Click();

        dialogs.WaitForAssertion(() => Assert.Contains("Neue Buchung für Jasmin", dialogs.Find(".step-dialog-title").TextContent));
        Assert.Contains(JasminId, Accounts.Owners.OfType<Guid>());
        Assert.Null(Member.Owner);
    }

    // Der sichtbare Schritt zählt, nicht nur, was im DOM steht: Alle Schritte stehen gezeichnet da, nur der aktuelle ist nicht hidden.
    private static string Progress(IRenderedComponent<IComponent> host) => host.Find("[data-testid='step-dialog-progress']").TextContent;

    private static bool IsShown(IRenderedComponent<IComponent> host, string testId) =>
        host.Find($"[data-testid='{testId}']").Closest(".step-dialog-step")!.GetAttribute("hidden") is null;

    [Fact]
    public async Task Tapping_a_person_in_the_position_dialog_shows_step_two_of_two_and_a_second_tap_after_Zurueck_shows_it_again()
    {
        var cut = await OpenHouseholdAsync();
        cut.Find("[data-testid='plan-add']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid^='person-choice-']")));

        dialogs.Find($"[data-testid='person-choice-{JasminId}']").Click();

        dialogs.WaitForAssertion(() => Assert.Contains("Schritt 2 von", Progress(dialogs)));
        Assert.True(IsShown(dialogs, "position-label"));
        Assert.False(IsShown(dialogs, "person-step"));
        Assert.DoesNotContain("Bitte gib dem Posten eine Bezeichnung", dialogs.Markup);

        // Ist die Person schon geladen, zeichnet nichts anderes den Rahmen neu: Der Tipp allein muss den Schritt wechseln.
        dialogs.FindAll(".step-dialog-actions button").Single(b => b.TextContent.Trim() == "Zurück").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Schritt 1 von", Progress(dialogs)));
        dialogs.Find($"[data-testid='person-choice-{JasminId}']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Schritt 2 von", Progress(dialogs)));
        Assert.True(IsShown(dialogs, "position-label"));
        Assert.Contains("Schritt 2 von 2", Progress(dialogs));
        Assert.DoesNotContain("Bitte gib dem Posten eine Bezeichnung", dialogs.Markup);
    }

    [Fact]
    public async Task Tapping_a_person_in_the_booking_dialog_shows_step_two_and_Weiter_does_not_skip_the_amount_step()
    {
        Grant(Levels(plan: AccessLevelKeys.Edit, book: AccessLevelKeys.Create, accounts: AccessLevelKeys.View));
        var cut = await OpenHouseholdAsync();
        cut.Find("[data-testid='month-action-booking']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid^='person-choice-']")));

        dialogs.Find($"[data-testid='person-choice-{JasminId}']").Click();

        dialogs.WaitForAssertion(() => Assert.Contains("Schritt 2 von", Progress(dialogs)));
        Assert.True(IsShown(dialogs, "booking-amount"));
        Assert.False(IsShown(dialogs, "person-step"));

        // Ist die Person schon geladen, zeichnet nichts anderes den Rahmen neu: Der Tipp allein muss den Schritt wechseln.
        dialogs.FindAll(".step-dialog-actions button").Single(b => b.TextContent.Trim() == "Zurück").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Schritt 1 von", Progress(dialogs)));
        dialogs.Find($"[data-testid='person-choice-{JasminId}']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Schritt 2 von", Progress(dialogs)));
        Assert.True(IsShown(dialogs, "booking-amount"));
        Assert.DoesNotContain("Schritt 3 von", Progress(dialogs));

        // Weiter führt erst jetzt weiter: Der Betragsschritt war nicht übersprungen, danach ist er verborgen.
        dialogs.Find("[data-testid='booking-next']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Schritt 3 von", Progress(dialogs)));
        Assert.False(IsShown(dialogs, "booking-amount"));
    }

    [Fact]
    public async Task A_second_tap_while_the_person_is_loading_does_not_advance_twice()
    {
        var gate = new TaskCompletionSource();
        Categories.ListGate = null;
        var cut = await OpenHouseholdAsync();
        cut.Find("[data-testid='plan-add']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid^='person-choice-']")));
        Categories.ListGate = gate.Task;

        dialogs.Find($"[data-testid='person-choice-{JasminId}']").Click();
        // Während Jasmin lädt, sind die Personen gesperrt, und Weiter tut nichts.
        dialogs.WaitForAssertion(() => Assert.All(dialogs.FindAll("[data-testid^='person-choice-']"), b => Assert.True(b.HasAttribute("disabled"))));
        dialogs.Find("[data-testid='position-next']").Click();
        gate.SetResult();

        dialogs.WaitForAssertion(() => Assert.Contains("Schritt 2 von", Progress(dialogs)));
        Assert.True(IsShown(dialogs, "position-label"));
        Assert.Equal(1, Categories.Owners.Count(o => o == JasminId));
    }

    [Fact]
    public async Task Adding_a_position_works_with_Anlegen_but_without_Ansehen_on_the_accounts()
    {
        Grant(Levels(plan: AccessLevelKeys.Create));
        var cut = await OpenHouseholdAsync();

        cut.Find("[data-testid='plan-add']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid^='person-choice-']")));
        dialogs.Find($"[data-testid='person-choice-{JasminId}']").Click();

        dialogs.WaitForAssertion(() => Assert.Contains("Neuer Posten für Jasmin", dialogs.Find(".step-dialog-title").TextContent));
        Assert.DoesNotContain(JasminId, Accounts.Owners.OfType<Guid>());
        Assert.Empty(dialogs.FindAll("[data-testid='person-error']"));
    }

    [Fact]
    public async Task Adding_a_position_skips_the_person_step_when_only_oneself_is_possible()
    {
        Grant(GrantLevels.None);
        var cut = await OpenHouseholdAsync();

        cut.Find("[data-testid='plan-add']").Click();

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='position-label']")));
        Assert.Empty(dialogs.FindAll("[data-testid^='person-choice-']"));
    }

    // --- Abhaken, Ändern, Löschen für ein anderes Mitglied (R3, A3) ---

    [Fact]
    public async Task Ticking_a_position_of_another_member_runs_as_that_member_under_their_grant()
    {
        Grant(Levels(plan: AccessLevelKeys.Edit, book: AccessLevelKeys.Edit, accounts: AccessLevelKeys.View));
        var cut = await OpenHouseholdAsync();

        cut.Find($"[data-testid='household-tick-{strom.Id}']").Click();

        cut.WaitForAssertion(() => Assert.Equal(strom.Id, Assert.Single(PlanApiFake.Ticks).Id));
        Assert.Equal(JasminId, Assert.Single(PlanApiFake.WriteOwners));
    }

    [Fact]
    public async Task Ticking_without_the_grant_is_refused_and_says_what_is_missing()
    {
        var cut = await OpenHouseholdAsync();

        cut.Find($"[data-testid='household-tick-{strom.Id}']").Click();

        Assert.Equal("true", cut.Find($"[data-testid='household-tick-{strom.Id}']").GetAttribute("aria-disabled"));
        cut.WaitForAssertion(() => Assert.Contains("Jasmin hat dir das nicht freigegeben. Es fehlt: Buchungen: Ändern", Notice(cut)));
        Assert.Empty(PlanApiFake.Ticks);
        Assert.Empty(PlanApiFake.Unticks);
    }

    [Fact]
    public async Task Ticking_without_Buchungen_Aendern_is_refused_and_names_that_step()
    {
        var cut = await OpenHouseholdAsync();

        cut.Find($"[data-testid='household-tick-{strom.Id}']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Jasmin hat dir das nicht freigegeben", Notice(cut)));
        Assert.Contains("Buchungen: Ändern", Notice(cut));
        Assert.DoesNotContain("Konten", Notice(cut));
        Assert.Empty(PlanApiFake.Ticks);
        Assert.Empty(PlanApiFake.WriteOwners);
    }

    [Fact]
    public async Task Ticking_without_Konten_Ansehen_names_the_accounts()
    {
        Grant(Levels(plan: AccessLevelKeys.Edit, book: AccessLevelKeys.Edit));
        var open = AddJasminPosition();
        var cut = await OpenHouseholdAsync();

        cut.Find($"[data-testid='household-tick-{open.Id}']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Konten: Ansehen", Notice(cut)));
        Assert.DoesNotContain("Buchungen", Notice(cut));
        Assert.Empty(PlanApiFake.Ticks);
    }

    [Fact]
    public async Task Ticking_a_position_without_bookings_asks_for_date_and_amount_and_books_for_that_member()
    {
        Grant(Levels(plan: AccessLevelKeys.Edit, book: AccessLevelKeys.Edit, accounts: AccessLevelKeys.View));
        var open = AddJasminPosition();
        var cut = await OpenHouseholdAsync();

        cut.Find($"[data-testid='household-tick-{open.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='tick-save']")));
        Assert.Empty(PlanApiFake.Ticks);
        dialogs.Find("[data-testid='tick-save']").Click();

        cut.WaitForAssertion(() => Assert.Equal(open.Id, Assert.Single(PlanApiFake.Ticks).Id));
        Assert.Equal(JasminId, Assert.Single(PlanApiFake.WriteOwners));
        Assert.Null(Member.Owner);
    }

    [Fact]
    public async Task Unticking_needs_Loeschen_on_the_bookings_and_runs_as_that_member()
    {
        var done = AddJasminPosition("Internet", done: true);
        var cut = await OpenHouseholdAsync();
        cut.Find($"[data-testid='household-tick-{done.Id}']").Click();
        cut.WaitForAssertion(() => Assert.Contains("Buchungen: Löschen", Notice(cut)));
        Assert.Empty(PlanApiFake.Unticks);

        Grant(Levels(plan: AccessLevelKeys.Edit, book: AccessLevelKeys.Delete));
        await Member.RefreshAsync();
        cut.Find($"[data-testid='household-tick-{done.Id}']").Click();

        cut.WaitForAssertion(() => Assert.Equal(done.Id, Assert.Single(PlanApiFake.Unticks)));
        Assert.Equal(JasminId, Assert.Single(PlanApiFake.WriteOwners));
    }

    [Fact]
    public async Task Editing_a_position_of_another_member_saves_it_for_that_member()
    {
        var water = AddJasminPosition();
        var cut = await OpenHouseholdAsync();

        cut.Find($"[data-testid='household-edit-{water.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.Equal("Wasser", Field(dialogs, "position-label").GetAttribute("value")));
        Field(dialogs, "position-label").Change("Wasser und Abwasser");
        dialogs.Find("[data-testid='position-save']").Click();

        cut.WaitForAssertion(() => Assert.Equal(water.Id, Assert.Single(PlanApiFake.UpdatedPositions).Id));
        Assert.Equal(JasminId, Assert.Single(PlanApiFake.WriteOwners));
        Assert.Null(Member.Owner);
    }

    [Fact]
    public async Task Editing_without_Aendern_on_the_plan_is_refused_and_names_that_step()
    {
        Grant(Levels(plan: AccessLevelKeys.View));
        var cut = await OpenHouseholdAsync();

        cut.Find($"[data-testid='household-edit-{strom.Id}']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Planung: Ändern", Notice(cut)));
        Assert.Empty(dialogs.FindAll("[data-testid='position-label']"));
    }

    // Spec 136, R7: Verschieben steht im Haushaltsplan wie in der eigenen Planung, für die Person der Zeile.
    [Fact]
    public async Task Postponing_a_position_of_another_member_asks_once_and_runs_for_that_member()
    {
        Grant(Levels(plan: AccessLevelKeys.Edit));
        var water = AddJasminPosition();
        var cut = await OpenHouseholdAsync();

        cut.Find($"[data-testid='household-postpone-{water.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("November", dialogs.Markup));
        Assert.Empty(PlanApiFake.Postponed);
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Verschieben").Click();

        cut.WaitForAssertion(() => Assert.Equal(water.Id, Assert.Single(PlanApiFake.Postponed)));
        Assert.Equal(JasminId, Assert.Single(PlanApiFake.WriteOwners));
    }

    [Fact]
    public async Task Postponing_without_Aendern_on_the_plan_is_refused_and_names_that_step()
    {
        Grant(Levels(plan: AccessLevelKeys.View));
        var water = AddJasminPosition();
        var cut = await OpenHouseholdAsync();

        cut.Find($"[data-testid='household-postpone-{water.Id}']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Planung: Ändern", Notice(cut)));
        Assert.Empty(PlanApiFake.Postponed);
    }

    [Fact]
    public async Task A_moved_row_and_its_copy_say_where_they_went_and_came_from_and_the_member_total_leaves_the_moved_one_out()
    {
        var moved = FakeHouseholdApi.OwnedPosition("GitHub", 4m, JasminId, "Jasmin", PotKeys.Wants) with { MovedToMonth = new PlanMonthRef(2026, 11) };
        var copy = FakeHouseholdApi.OwnedPosition("Gym", 20m, JasminId, "Jasmin", PotKeys.Wants) with { MovedFromMonth = new PlanMonthRef(2026, 9) };
        var plan = HouseholdApiFake.HouseholdPlans[(2026, 10)];
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = plan with { Positions = [.. plan.Positions, moved, copy] };
        var cut = await OpenHouseholdAsync();

        var movedRow = cut.Find($"[data-testid='household-position-{moved.Id}']");
        Assert.Contains("row-moved", movedRow.ClassName);
        Assert.Contains("in November verschoben", movedRow.TextContent);
        Assert.Contains("aus September", cut.Find($"[data-testid='household-position-{copy.Id}']").TextContent);
        Assert.Contains("20,00", cut.Find($"[data-testid='household-member-group-{PotKeys.Wants}-{JasminId}'] .pot-member-sum").TextContent);
    }

    [Fact]
    public async Task Deleting_a_position_of_another_member_asks_first_and_deletes_only_after_confirming()
    {
        Grant(Levels(plan: AccessLevelKeys.Delete));
        var water = AddJasminPosition();
        var cut = await OpenHouseholdAsync();
        cut.Find($"[data-testid='household-edit-{water.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='plan-delete']")));

        dialogs.Find("[data-testid='plan-delete']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Posten löschen?", dialogs.Markup));
        Assert.Empty(PlanApiFake.DeletedPositions);
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Abbrechen").Click();
        dialogs.WaitForAssertion(() => Assert.DoesNotContain("Posten löschen?", dialogs.Markup));
        Assert.Empty(PlanApiFake.DeletedPositions);

        cut.Find($"[data-testid='household-edit-{water.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='plan-delete']")));
        dialogs.Find("[data-testid='plan-delete']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Posten löschen?", dialogs.Markup));
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Löschen").Click();

        cut.WaitForAssertion(() => Assert.Equal(water.Id, Assert.Single(PlanApiFake.DeletedPositions)));
        Assert.Equal(JasminId, Assert.Single(PlanApiFake.WriteOwners));
    }

    // --- Die Rolle von Jasmin gilt nur, solange der Dialog zu ihr gehört (Spec 125, Review) ---

    [Fact]
    public async Task Leaving_the_page_closes_the_dialog_and_ends_acting_for_the_other_member()
    {
        var water = AddJasminPosition();
        var cut = await OpenHouseholdAsync();
        cut.Find($"[data-testid='household-edit-{water.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='position-label']")));
        Assert.Equal(JasminId, Member.Owner);

        Navigation.NavigateTo("/konten");

        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindAll("[data-testid='position-label']")));
        cut.WaitForAssertion(() => Assert.Null(Member.Owner));
        Assert.Null(Member.Name);
        Assert.True(Member.IsHousehold);
    }

    [Fact]
    public async Task Going_back_in_the_browser_with_a_dialog_for_another_member_open_resets_the_side_navigation()
    {
        var side = Render<SideNavigation>();
        var water = AddJasminPosition();
        var cut = await OpenHouseholdAsync();
        cut.Find($"[data-testid='household-edit-{water.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='position-label']")));

        Navigation.NavigateTo("/konten");

        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindAll("[data-testid='position-label']")));
        side.WaitForAssertion(() => Assert.DoesNotContain("Planung von Jasmin", side.Markup));
    }

    // --- Drucken ---

    [Fact]
    public void A_failing_print_shows_a_notice_and_does_not_throw()
    {
        printing.Failure = new InvalidOperationException("kein Drucker");
        var cut = OpenPersonal();

        cut.Find("[data-testid='plan-print']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Das Drucken hat nicht geklappt", Notice(cut)));
    }

    [Fact]
    public async Task Print_for_oneself_says_Meine_Planung()
    {
        var cut = await OpenHouseholdAsync();
        var subject = "";
        printing.AtPrint = () => subject = cut.Find("[data-testid='month-print'] .print-subject").TextContent;

        cut.Find("[data-testid='plan-print']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid^='person-choice-']")));
        dialogs.Find($"[data-testid='person-choice-{FakeHouseholdApi.AnnaId}']").Click();

        cut.WaitForAssertion(() => Assert.Equal(1, printing.Calls));
        Assert.Contains("Meine Planung", subject);
        Assert.DoesNotContain("Planung von", subject);
    }

    // Am Handy steht Posten anlegen im Menü; es muss zu sein, wenn die Wahl der Person aufgeht, und nicht dahinter offen bleiben.
    [Fact]
    public async Task The_phone_menu_closes_when_the_person_choice_opens()
    {
        var cut = await OpenHouseholdAsync();
        cut.Find("[data-testid='page-more']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='plan-add-menu']")));
        Assert.Equal("true", cut.Find("[data-testid='page-more']").GetAttribute("aria-expanded"));

        dialogs.Find("[data-testid='plan-add-menu']").Click();

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid^='person-choice-']")));
        cut.WaitForAssertion(() => Assert.Equal("false", cut.Find("[data-testid='page-more']").GetAttribute("aria-expanded")));
    }
}
