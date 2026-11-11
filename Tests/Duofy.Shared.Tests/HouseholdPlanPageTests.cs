using Bunit;
using Microsoft.AspNetCore.Components;
using Duofy.Schemas;
using Duofy.Shared.Layout;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 69, R9 und A10: Der Umschalter bietet "Haushalt", solange der Haushalt mehr als ein Mitglied hat, und die
/// Planseite zeigt dann den Haushaltsplan nur zum Lesen. Angemeldet ist hier Ben im Haushalt von Anna; "heute" ist
/// der 21. Oktober 2026.
/// </summary>
public class HouseholdPlanPageTests : PageTestContext
{
    private const string NoJointView = "Für den Haushalt gibt es hier keine gemeinsame Sicht.";

    private readonly HouseholdResponse household;

    public HouseholdPlanPageTests()
    {
        RenderDialogHost();
        household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
            [FakeHouseholdApi.Anna(isMe: false), FakeHouseholdApi.Ben(isMe: true)]);
        HouseholdApiFake.Household = household;
    }

    private ActiveMember Member => Services.GetRequiredService<ActiveMember>();

    // Das Menü des Umschalters geht in der Hülle für Dialoge und Menüs auf, nicht im Umschalter selbst.
    private IRenderedComponent<IComponent>? menuHost;

    private string Names() =>
        string.Join(",", menuHost!.FindAll("[data-testid^='member-choice-']").Select(o => o.TextContent.Trim()));

    private IRenderedComponent<UserMenu> Open(IRenderedComponent<UserMenu> cut)
    {
        menuHost ??= RenderDialogHost();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='user-menu-open']")));
        cut.Find("[data-testid='user-menu-open']").Click();
        return cut;
    }

    // R9
    [Fact]
    public void In_a_household_of_two_the_menu_offers_the_household_even_without_any_grant()
    {
        var cut = Open(Render<UserMenu>());

        menuHost!.WaitForAssertion(() => Assert.NotEqual("true", menuHost.Find("[data-testid='member-choice-household']").GetAttribute("aria-disabled")));
        Assert.Equal("Ich,Haushalt", Names());
    }

    // A10, Spec 44 A2: Bei einem Haushalt von einem ist "Haushalt" sichtbar, aber nicht verfügbar.
    [Fact]
    public void In_a_household_of_one_the_household_entry_is_unavailable()
    {
        HouseholdApiFake.Household = household with { Members = [FakeHouseholdApi.Ben(isMe: true)] };

        var cut = Open(Render<UserMenu>());

        cut.WaitForAssertion(() => Assert.Equal(1, HouseholdApiFake.MineCalls));
        menuHost!.WaitForAssertion(() => Assert.Equal("true", menuHost.Find("[data-testid='member-choice-household']").GetAttribute("aria-disabled")));
    }

    [Fact]
    public void Granted_members_and_the_household_are_both_offered_and_choosing_the_household_clears_the_owner()
    {
        HouseholdApiFake.Household = household with
        {
            Members = [FakeHouseholdApi.Anna(isMe: false) with { GrantsToMe = GrantLevels.None with { Plan = AccessLevelKeys.View } }, FakeHouseholdApi.Ben(isMe: true)],
        };
        var cut = Open(Render<UserMenu>());
        menuHost!.WaitForAssertion(() => Assert.Equal("Ich,Anna,Haushalt", Names()));
        menuHost.Find($"[data-testid='member-choice-{FakeHouseholdApi.AnnaId}']").Click();

        Open(cut);
        menuHost.WaitForAssertion(() => Assert.NotEmpty(menuHost.FindAll("[data-testid='member-choice-household']")));
        menuHost.Find("[data-testid='member-choice-household']").Click();

        Assert.True(Member.IsHousehold);
        Assert.Null(Member.Owner);
        Assert.False(Member.Allows(GrantAreas.Plan, AccessLevelKeys.View));
        Assert.Equal("Ansicht: Haushalt", cut.Find("[data-testid='member-profile']").TextContent.Trim());
    }

    [Fact]
    public async Task Choosing_a_person_or_me_leaves_the_household_view()
    {
        await Member.LoadAsync();
        Member.ChooseHousehold();

        Member.Choose(null);

        Assert.False(Member.IsHousehold);
        Assert.True(Member.Allows(GrantAreas.Plan, AccessLevelKeys.Delete));
    }

    // A10: Haushaltsname, Posten mit Besitzer, keine Knöpfe zum Ändern.
    [Fact]
    public async Task The_plan_page_shows_the_household_plan_with_owner_names_and_no_buttons_to_change_anything()
    {
        var rent = FakeHouseholdApi.OwnedPosition("Miete", 890m, FakeHouseholdApi.AnnaId, "Anna");
        var power = FakeHouseholdApi.OwnedPosition("Strom", 80m, FakeHouseholdApi.BenId, "Ben", booked: 30m);
        var cinema = FakeHouseholdApi.OwnedPosition("Kino", 20m, FakeHouseholdApi.AnnaId, "Anna", PotKeys.Wants);
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10) with
        {
            Positions = [rent, power, cinema],
            Pots = [new HouseholdPotResponse(PotKeys.Needs, 970m, 30m), new HouseholdPotResponse(PotKeys.Wants, 0m, 0m), new HouseholdPotResponse(PotKeys.Savings, 0m, 0m)],
            Income = 3000m,
        };
        await Member.LoadAsync();
        Member.ChooseHousehold();

        var cut = Render<Plan>();
        OpenPlanTab(cut);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"[data-testid='household-position-{rent.Id}']")));

        Assert.Equal("Haushalt von Anna", cut.Find("[data-testid='household-name']").TextContent.Trim());
        // A19 (Spec 82, R16; Tom, 03.11.2026): oben die Töpfe wie im Monat einer Person, darin die Posten unter dem Namen ihres Mitglieds.
        var needs = cut.Find("[data-testid='pot-section-needs']");
        var wants = cut.Find("[data-testid='pot-section-wants']");
        Assert.Contains("970,00 €", needs.QuerySelector("[data-testid='pot-sum-needs']")!.TextContent);
        var anna = needs.QuerySelector($"[data-testid='household-member-group-needs-{FakeHouseholdApi.AnnaId}']")!;
        var ben = needs.QuerySelector($"[data-testid='household-member-group-needs-{FakeHouseholdApi.BenId}']")!;
        Assert.Equal("Anna", anna.QuerySelector("h4")!.TextContent.Trim());
        // Tom, 03.11.2026: Die Personengruppe ist präsent, mit Initialen-Kreis wie im Profil und der Summe der Person im Topf.
        Assert.Equal("A", anna.QuerySelector(".pot-member-head .avatar")!.TextContent.Trim());
        Assert.Contains("890,00 €", anna.QuerySelector(".pot-member-sum")!.TextContent);
        Assert.Equal("Ben", ben.QuerySelector("h4")!.TextContent.Trim());
        Assert.NotNull(anna.QuerySelector($"[data-testid='household-position-{rent.Id}']"));
        Assert.NotNull(ben.QuerySelector($"[data-testid='household-position-{power.Id}']"));
        Assert.Null(anna.QuerySelector($"[data-testid='household-position-{power.Id}']"));
        Assert.NotNull(wants.QuerySelector($"[data-testid='household-member-group-wants-{FakeHouseholdApi.AnnaId}'] [data-testid='household-position-{cinema.Id}']"));
        Assert.Null(wants.QuerySelector($"[data-testid='household-member-group-wants-{FakeHouseholdApi.BenId}']"));
        Assert.Contains("890,00 €", cut.Find($"[data-testid='household-position-{rent.Id}']").TextContent);
        Assert.Contains("30,00 € gebucht", cut.Find($"[data-testid='household-position-{power.Id}']").TextContent);
        Assert.Contains("Oktober 2026", cut.Find("[data-testid='page-title']").TextContent);
        Assert.Equal((household.Id, 2026, 10), Assert.Single(HouseholdApiFake.PlanCalls));
        Assert.Empty(PlanApiFake.GetCalls);
        foreach (var gone in new[] { "plan-add", "plan-carry-over-open", "plan-changes" })
        {
            Assert.Empty(cut.FindAll($"[data-testid='{gone}']"));
        }
        Assert.Empty(cut.FindAll("[role='checkbox']"));
        Assert.Empty(cut.FindAll("button[data-testid^='plan-edit'],button[data-testid^='plan-delete']"));
    }
    // Spec 82, R16: Den Monatsumschalter gibt es nicht mehr; zurück geht es zur Übersicht der Monate.
    [Fact]
    public async Task The_household_month_leads_back_to_the_months()
    {
        await Member.LoadAsync();
        Member.ChooseHousehold();
        var cut = Render<Plan>();
        cut.WaitForAssertion(() => Assert.Contains("Oktober 2026", cut.Find("[data-testid='page-title']").TextContent));

        Assert.Empty(cut.FindAll("[data-testid='plan-next'],[data-testid='plan-previous']"));
        Assert.Equal("/plan", cut.Find("[data-testid='plan-back']").GetAttribute("href"));
    }

    // R8; Tom, 03.11.2026: Die Kontostände stehen in der Auswertung, je Person mit Summe; wer fehlt, steht dabei.
    [Fact]
    public async Task Accounts_are_shown_per_person_in_the_review_for_those_who_granted_them_and_the_others_are_named()
    {
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10) with
        {
            Accounts =
            [
                new HouseholdAccountResponse(Guid.NewGuid(), FakeHouseholdApi.AnnaId, "Anna", "Giro", true, 1234.50m),
                new HouseholdAccountResponse(Guid.NewGuid(), FakeHouseholdApi.BenId, "Ben", "Tagesgeld", false, 500m),
            ],
            AccountsMissing = ["Clara"],
        };
        await Member.LoadAsync();
        Member.ChooseHousehold();

        var cut = Render<Plan>();
        OpenPlanTab(cut);
        Assert.Empty(cut.FindAll("[data-testid='household-accounts']"));
        OpenReviewTab(cut);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='household-accounts']")));

        Assert.Contains("Giro", cut.Find("[data-testid='month-accounts-group-owner-Anna']").TextContent);
        Assert.Contains("1.234,50 €", cut.Find("[data-testid='month-accounts-group-owner-Anna']").TextContent);
        Assert.Contains("Tagesgeld", cut.Find("[data-testid='month-accounts-group-owner-Ben']").TextContent);
        Assert.Contains("1.734,50 €", cut.Find("[data-testid='month-accounts-total']").TextContent);
        Assert.Equal("Ohne Freigabe der Konten fehlen hier: Clara.", cut.Find("[data-testid='household-accounts-missing']").TextContent.Trim());
    }

    [Fact]
    public async Task Without_missing_accounts_there_is_no_notice()
    {
        await Member.LoadAsync();
        Member.ChooseHousehold();

        var cut = Render<Plan>();
        OpenReviewTab(cut);

        Assert.Empty(cut.FindAll("[data-testid='household-accounts-missing']"));
    }

    // R4: Quoten wie bei einer Person, in der Legende des Topf-Diagramms; seit Toms Durchsicht vom 03.11.2026 ohne Textkasten mit Summen.
    [Fact]
    public async Task The_quotas_of_the_household_are_shown_in_the_pot_chart()
    {
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10) with
        {
            Income = 5000m,
            Unpaid = 120m,
            Pots = [new HouseholdPotResponse(PotKeys.Needs, 2500m, 2500m), new HouseholdPotResponse(PotKeys.Wants, 0m, 0m), new HouseholdPotResponse(PotKeys.Savings, 0m, 0m)],
            Quotas = [new PlanQuotaResponse(PotKeys.Needs, 50, 50.0m), new PlanQuotaResponse(PotKeys.Wants, 30, 0m), new PlanQuotaResponse(PotKeys.Savings, 20, 0m)],
            // Eine gebuchte Einnahme, damit das Ist der API gilt (Spec 82, R17).
            Unplanned = new HouseholdUnplannedResponse(10m, 0m, 40m, 0m),
        };
        await Member.LoadAsync();
        Member.ChooseHousehold();

        var cut = Render<Plan>();
        OpenReviewTab(cut);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-review-pots-chart-row-needs']")));
        var needs = cut.FindAll("[data-testid='month-review-pots-chart-row-needs'] td").Select(td => td.TextContent.Trim()).ToList();
        Assert.Equal(["50 %", "50 %"], needs[^2..]);
        Assert.Empty(cut.FindAll("[data-testid='plan-sums']"));
    }

    // Tom, 03.11.2026: Ungeplant ebenso je Person, aus den Buchungen, die der Betrachter sieht; wer fehlt, steht dabei.
    [Fact]
    public async Task Unplanned_is_split_by_person_and_names_who_does_not_share()
    {
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10) with
        {
            Unplanned = new HouseholdUnplannedResponse(0m, 25m, 40m, 0m),
            Bookings =
            [
                new HouseholdBookingResponse(Guid.NewGuid(), new DateOnly(2026, 10, 3), 25m, 2026, 10, FakeHouseholdApi.AnnaId, "Anna",
                    Lines: [new HouseholdBookingLineResponse(null, 25m, "Drogerie")]),
                new HouseholdBookingResponse(Guid.NewGuid(), new DateOnly(2026, 10, 4), 40m, 2026, 10, FakeHouseholdApi.BenId, "Ben",
                    Lines: [new HouseholdBookingLineResponse(null, 40m, "Kino")]),
            ],
            BookingsMissing = ["Clara"],
        };
        await Member.LoadAsync();
        Member.ChooseHousehold();

        var cut = Render<Plan>();
        OpenPlanTab(cut);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='plan-unplanned-people']")));

        var line = cut.Find("[data-testid='plan-unplanned']").TextContent;
        Assert.Contains("65,00 €", line);
        var people = cut.FindAll("[data-testid='plan-unplanned-people'] li").Select(li => li.TextContent).ToList();
        Assert.Equal(2, people.Count);
        Assert.Contains("Anna", people[0]);
        Assert.Contains("25,00 €", people[0]);
        Assert.Contains("Ben", people[1]);
        Assert.Contains("40,00 €", people[1]);
        Assert.Contains("Clara", line);
    }

    [Fact]
    public async Task A_failing_household_plan_shows_the_error()
    {
        HouseholdApiFake.Error = ErrorCodes.HouseholdNotFound;
        await Member.LoadAsync();
        Member.ChooseHousehold();

        var cut = Render<Plan>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(AlertSelector)));
    }

    // R9: Auf den anderen Seiten gibt es keine gemeinsame Sicht.
    [Fact]
    public async Task Other_pages_say_there_is_no_joint_view()
    {
        await Member.LoadAsync();
        Member.ChooseHousehold();

        var bookings = Render<AccountBookings>(c => c.Add(x => x.AccountId, Guid.NewGuid()));
        var accounts = Render<Accounts>();
        var commitments = Render<Commitments>();

        bookings.WaitForAssertion(() => Assert.Equal(NoJointView, bookings.Find("[data-testid='not-shared']").TextContent.Trim()));
        accounts.WaitForAssertion(() => Assert.Equal(NoJointView, accounts.Find("[data-testid='not-shared']").TextContent.Trim()));
        commitments.WaitForAssertion(() => Assert.Equal(NoJointView, commitments.Find("[data-testid='not-shared']").TextContent.Trim()));
        Assert.Empty(BookingApiFake.ListOwners);
    }

    [Fact]
    public async Task Leaving_the_household_of_two_drops_the_household_view()
    {
        await Member.LoadAsync();
        Member.ChooseHousehold();

        HouseholdApiFake.Household = household with { Members = [FakeHouseholdApi.Ben(isMe: true)] };
        await Member.RefreshAsync();

        Assert.False(Member.IsHousehold);
        Assert.False(Member.CanShowHousehold);
    }

    // Spec 82, R16 (A19): Das Ziel "Haushalt" zeigt die Monate des Haushalts wie die eigenen Pläne und lässt die Auswahl in Ruhe.
    [Fact]
    public void The_target_household_lists_the_months_of_the_household()
    {
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10);

        var cut = Render<HouseholdPlanPage>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='plan-month-row-2026-10']")));
        Assert.Equal("/haushaltsplan/2026/10", cut.Find("[data-testid='plan-month-row-2026-10']").GetAttribute("href"));
        Assert.Equal("Haushalt von Anna", cut.Find("[data-testid='page-title']").TextContent.Trim());
        Assert.Equal("page", cut.Find("[data-testid='household-tab-plan']").GetAttribute("aria-current"));
        Assert.False(Member.IsHousehold);
    }

    // Spec 82, R16 (A19): Ein Monat des Haushalts hat seine Adresse und führt zurück zur Übersicht.
    [Fact]
    public void A_month_of_the_household_opens_at_its_address()
    {
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10);

        var cut = Render<HouseholdPlanPage>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));

        cut.WaitForAssertion(() => Assert.Contains("Oktober 2026", cut.Find("[data-testid='page-title']").TextContent));
        Assert.Equal("/haushaltsplan", cut.Find("[data-testid='plan-back']").GetAttribute("href"));
        Assert.Empty(cut.FindAll("[data-testid='plan-next']"));
    }

    // Spec 51, A3: Auch ein Haushalt von einer Person hat einen Plan.
    [Fact]
    public void A_household_of_one_also_opens_its_plan()
    {
        HouseholdApiFake.Household = household with { Members = [FakeHouseholdApi.Ben(isMe: true)] };
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10);

        var cut = Render<HouseholdPlanPage>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='plan-month-row-2026-10']")));
    }

    // Spec 51, A4
    [Fact]
    public void Without_a_household_the_target_leads_to_founding_or_joining()
    {
        HouseholdApiFake.Household = null;

        Render<HouseholdPlanPage>();

        Assert.EndsWith("/haushalt", Navigation.Uri);
    }

    // Spec 51, A10, A13: Der Haushaltsplan nennt je Posten seine Buchungen und je Buchung Art und Zweck.
    [Fact]
    public async Task The_household_plan_lists_kind_purpose_and_the_bookings_of_a_position()
    {
        var position = FakeHouseholdApi.OwnedPosition("Lebensmittel", 400m, FakeHouseholdApi.BenId, "Ben");
        var booking = new HouseholdBookingResponse(Guid.NewGuid(), new DateOnly(2026, 10, 3), 30m, 2026, 10, FakeHouseholdApi.BenId, "Ben",
            BookingKinds.Expense, "Lebensmittel", [new HouseholdBookingLineResponse(position.Id, 30m, "Drogerie")]);
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10) with { Positions = [position], Bookings = [booking] };
        await Member.LoadAsync();
        var cut = Render<HouseholdPlanPage>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-tab-bookings']")));

        cut.Find("[data-testid='month-tab-bookings']").Click();

        var row = cut.Find($"[data-testid='month-booking-{booking.Id}']");
        Assert.Contains("Ausgabe", row.TextContent);
        Assert.Contains("Lebensmittel", row.TextContent);

        cut.Find("[data-testid='month-tab-plan']").Click();

        var list = cut.Find($"[data-testid='position-bookings-{position.Id}']");
        Assert.Contains("1 Buchung", list.TextContent);
        Assert.Contains("Drogerie · Ben", list.TextContent);
    }

    // Gefunden im Browser: Beim direkten Aufruf antwortet der Server später; bis dahin darf die Sicht nicht mit Monat 0 zeichnen.
    [Fact]
    public async Task The_view_draws_its_month_while_the_members_are_still_loading()
    {
        var gate = new TaskCompletionSource();
        HouseholdApiFake.MineGate = gate.Task;
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10);

        var cut = Render<HouseholdPlanView>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));

        Assert.Contains("Oktober 2026", cut.Find("h1").TextContent);
        gate.SetResult();
        await cut.InvokeAsync(() => { });
        cut.WaitForAssertion(() => Assert.Contains("Oktober 2026", cut.Find("h1").TextContent));
    }
}
