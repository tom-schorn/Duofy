using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Duofy.Shared.Tests;

/// <summary>
/// Die Monatsseite /plan/{Jahr}/{Monat} mit den Tabs Übersicht, Plan und Buchungen (Spec 24, A1 bis A13).
/// Die bisherigen Tests des Monatsplans (A7) stehen in PlanPageTests und den Nachbardateien und öffnen dort zuerst den Tab Plan.
/// "Heute" ist der 21. Oktober 2026.
/// </summary>
public class MonthPageTests : PageTestContext
{
    private readonly IRenderedComponent<MudDialogProvider> dialogs;

    private ActiveMember Member => Services.GetRequiredService<ActiveMember>();

    public MonthPageTests()
    {
        Render<MudPopoverProvider>();
        dialogs = Render<MudDialogProvider>();
        Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true));
    }

    private static PlanOverviewResponse Overview(
        decimal income = 3000m, decimal fixedAmount = 1200m, decimal free = 1800m,
        PlanPotResponse[]? pots = null, params BookingResponse[] bookings) =>
        new(income, fixedAmount, free,
            pots ?? [new PlanPotResponse(PotKeys.Needs, 1000m, 1200m), new PlanPotResponse(PotKeys.Wants, 0m, 0m), new PlanPotResponse(PotKeys.Savings, 0m, 0m)],
            bookings);

    private static BookingResponse Booking(DateOnly date, decimal amount, int planYear, int planMonth) =>
        new(Guid.NewGuid(), BookingKinds.Expense, Guid.NewGuid(), null, date, amount, null, [], planYear, planMonth);

    private IRenderedComponent<Plan> Open(int year, int month, PlanOverviewResponse? overview, params PlanPositionResponse[] positions)
    {
        PlanApiFake.Plans[(year, month)] = FakePlanApi.Empty(year, month) with { Positions = positions, Overview = overview };
        var cut = Render<Plan>(p => p.Add(x => x.Year, year).Add(x => x.Month, month));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-tab-overview']")));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-overview']")));
        return cut;
    }

    private static string Text(IRenderedComponent<Plan> cut, string testId) => cut.Find($"[data-testid='{testId}']").TextContent;

    private static bool Selected(IRenderedComponent<Plan> cut, string tab) =>
        cut.Find($"[data-testid='month-tab-{tab}']").GetAttribute("aria-selected") == "true";

    // A1 (R1): Tabs Übersicht, Plan und Buchungen; die Übersicht ist offen. (Die Spec nennt in A1 zwei Tabs, die
    // Anforderung R1 und der Kunde drei: Buchungen kommt mit A9 dazu.)
    [Fact]
    public void The_month_opens_with_the_tabs_overview_plan_and_bookings_and_the_overview_is_active()
    {
        var cut = Open(2026, 10, Overview());

        Assert.Equal(["Übersicht", "Plan", "Buchungen", "Auswertung"], cut.FindAll("[role='tab']").Select(t => t.TextContent.Trim()));
        Assert.True(Selected(cut, "overview"));
        Assert.False(Selected(cut, "plan"));
        Assert.Empty(cut.FindAll("[data-testid='plan-sums']"));
        Assert.Equal("tablist", cut.Find(".month-tabs").GetAttribute("role"));
    }

    // A1 (R1): der gewählte Tab bleibt beim Blättern zwischen den Monaten.
    [Fact]
    public void The_chosen_tab_stays_while_paging_between_months()
    {
        var cut = Open(2026, 10, Overview());

        cut.Find("[data-testid='month-tab-plan']").Click();
        cut.Find("[data-testid='plan-next']").Click();

        cut.WaitForAssertion(() => Assert.Contains("November 2026", Text(cut, "plan-month")));
        Assert.True(Selected(cut, "plan"));
        Assert.NotEmpty(cut.FindAll("[data-testid='plan-sums']"));
    }

    [Fact]
    public void The_arrow_keys_move_to_the_neighbouring_tab()
    {
        var cut = Open(2026, 10, Overview());

        cut.Find("[data-testid='month-tab-overview']").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "ArrowRight" });

        Assert.True(Selected(cut, "plan"));
    }

    // A2 (R2)
    [Fact]
    public void The_three_blocks_show_income_fixed_and_free()
    {
        var cut = Open(2026, 10, Overview(3000m, 1200m, 1800m));

        Assert.Contains("Einnahmen", Text(cut, "month-block-income"));
        Assert.Contains("3.000,00 €", Text(cut, "month-block-income"));
        Assert.Contains("Festes", Text(cut, "month-block-fixed"));
        Assert.Contains("1.200,00 €", Text(cut, "month-block-fixed"));
        Assert.Contains("Frei", Text(cut, "month-block-free"));
        Assert.Contains("1.800,00 €", Text(cut, "month-block-free"));
    }

    // A6 (R6)
    [Fact]
    public void Free_at_zero_says_everything_is_planned()
    {
        var cut = Open(2026, 10, Overview(3000m, 3000m, 0m));

        Assert.Contains("Alles verplant", Text(cut, "month-block-free"));
        Assert.DoesNotContain("0,00", Text(cut, "month-block-free"));
    }

    // A3 (R3)
    [Fact]
    public void The_target_actual_bar_of_a_pot_shows_the_actual_percent_as_text()
    {
        var plan = FakePlanApi.Empty(2026, 10) with
        {
            Quotas = [new PlanQuotaResponse(PotKeys.Needs, 50, 60m), new PlanQuotaResponse(PotKeys.Wants, 30, 10m), new PlanQuotaResponse(PotKeys.Savings, 20, 0m)],
            Overview = Overview(),
        };
        PlanApiFake.Plans[(2026, 10)] = plan;
        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-pot-needs']")));

        Assert.Contains("Grundbedarf: Ziel 50 %, Ist 60 %", Text(cut, "month-pot-needs"));
        Assert.Contains("Wünsche: Ziel 30 %, Ist 10 %", Text(cut, "month-pot-wants"));
        Assert.NotEmpty(cut.Find("[data-testid='month-pot-needs']").QuerySelectorAll(".chart-target-bar"));
    }

    // A4 (R4)
    [Fact]
    public void The_gap_chart_shows_planned_against_actual_in_euro_without_an_alarm_colour()
    {
        var cut = Open(2026, 10, Overview(pots: [
            new PlanPotResponse(PotKeys.Needs, 1000m, 1200m),
            new PlanPotResponse(PotKeys.Wants, 100m, 40m),
            new PlanPotResponse(PotKeys.Savings, 200m, 200m)]));

        var gap = Text(cut, "month-gap");
        Assert.Contains("Grundbedarf, mehr als geplant", gap);
        Assert.Contains("200,00 €", gap);
        Assert.Contains("Wünsche, weniger als geplant", gap);
        Assert.Contains("60,00 €", gap);
        Assert.Contains("Sparen, wie geplant", gap);
        var markup = cut.Find("[data-testid='month-gap']").InnerHtml;
        Assert.DoesNotContain("error", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("danger", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("warning", markup, StringComparison.OrdinalIgnoreCase);
    }

    // A10 (R10)
    [Fact]
    public void The_overview_lists_only_the_five_newest_of_seven_bookings_with_date_and_amount()
    {
        var bookings = Enumerable.Range(1, 7).Select(d => Booking(new DateOnly(2026, 10, d), d * 10m, 2026, 10)).Reverse().ToArray();
        var cut = Open(2026, 10, Overview(bookings: bookings));

        var rows = cut.FindAll("[data-testid^='month-recent-'][data-testid!='month-recent-empty']");
        Assert.Equal(5, rows.Count);
        Assert.Contains("07.10.2026", rows[0].TextContent);
        Assert.Contains("70,00 €", rows[0].TextContent);
        Assert.Contains("03.10.2026", rows[4].TextContent);
    }

    // A11 (R11)
    [Fact]
    public void Soon_due_and_overdue_payments_are_both_listed_and_marked_apart()
    {
        var soon = FakePlanApi.Position("Strom", 80m, day: 23);
        var late = FakePlanApi.Position("Miete", 890m, day: 18);
        var cut = Open(2026, 10, Overview(), soon, late);

        var due = Text(cut, "month-due");
        Assert.Contains("Strom", due);
        Assert.Contains("Miete", due);
        Assert.Single(cut.FindAll("[data-testid='month-overdue-badge']"));
        Assert.Single(cut.FindAll("[data-testid='month-upcoming-badge']"));
        Assert.Contains("überfällig", Text(cut, "month-due-item-overdue"));
        Assert.Contains("Miete", Text(cut, "month-due-item-overdue"));
        Assert.Contains("anstehend", Text(cut, "month-due-item-upcoming"));
    }

    // A12 (R12)
    [Fact]
    public void The_quick_actions_are_new_booking_new_position_and_tick_a_position()
    {
        var cut = Open(2026, 10, Overview());

        Assert.Equal("Neue Buchung", Text(cut, "month-action-booking").Trim());
        Assert.Equal("Neuer Posten", Text(cut, "month-action-position").Trim());
        Assert.Equal("Posten abhaken", Text(cut, "month-action-tick").Trim());
    }

    [Fact]
    public void New_booking_and_new_position_open_their_dialogs_and_tick_leads_to_the_plan_tab()
    {
        var cut = Open(2026, 10, Overview());

        cut.Find("[data-testid='month-action-booking']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll(".mud-dialog")));
        dialogs.Find("button[data-testid='booking-cancel'],button.mud-button-text").Click();

        cut.Find("[data-testid='month-action-tick']").Click();
        Assert.True(Selected(cut, "plan"));
        Assert.NotEmpty(cut.FindAll("[data-testid='plan-sums']"));
    }

    // Fund S2: /plan/2026/12 am 21. Oktober bot bisher nur September bis November an.
    [Fact]
    public void New_booking_starts_in_the_month_on_the_screen_even_when_it_is_far_from_today()
    {
        var cut = Open(2026, 12, Overview());

        cut.Find("[data-testid='month-action-booking']").Click();

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='booking-plan-month']")));
        var select = dialogs.Find("[data-testid='booking-plan-month']");
        Assert.Equal("2026-12", select.GetAttribute("value"));
        Assert.Contains("Dezember 2026", select.TextContent);
        Assert.Equal("2026-10-21", Field(dialogs, "booking-date").GetAttribute("value"));
    }

    [Fact]
    public void New_position_opens_the_position_dialog()
    {
        var cut = Open(2026, 10, Overview());

        cut.Find("[data-testid='month-action-position']").Click();

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='position-label']")));
    }

    // A5 (R5)
    [Fact]
    public void The_plan_tab_groups_positions_by_pot_in_line_tables_and_they_can_be_ticked()
    {
        var rent = FakePlanApi.Position("Miete", 890m);
        var fun = FakePlanApi.Position("Streaming", 12.99m, PotKeys.Wants);
        var cut = Open(2026, 10, Overview(), rent, fun);

        cut.Find("[data-testid='month-tab-plan']").Click();

        Assert.Equal(["Grundbedarf", "Wünsche"], cut.FindAll("[data-testid^='plan-group-']").Select(g => g.TextContent.Trim()));
        Assert.Equal(2, cut.FindAll("table.line-table").Count);
        Assert.NotEmpty(cut.Find("table.line-table").QuerySelectorAll($"[data-testid='plan-tick-{rent.Id}']"));
        Field(cut, $"plan-tick-{rent.Id}").Change(true);
        cut.WaitForAssertion(() => Assert.Equal(rent.Id, Assert.Single(PlanApiFake.Ticks).Id));
    }

    // A9 (R8)
    [Fact]
    public void The_bookings_tab_of_december_holds_a_booking_of_november_20_with_a_note_and_november_does_not()
    {
        var assigned = Booking(new DateOnly(2026, 11, 20), 45m, 2026, 12);
        var cut = Open(2026, 12, Overview(bookings: assigned));

        cut.Find("[data-testid='month-tab-bookings']").Click();

        var row = Text(cut, $"month-booking-{assigned.Id}");
        Assert.Contains("20.11.2026", row);
        Assert.Contains("45,00 €", row);
        Assert.Contains("Datum im November", Text(cut, $"month-booking-other-{assigned.Id}"));

        PlanApiFake.Plans[(2026, 11)] = FakePlanApi.Empty(2026, 11) with { Overview = Overview() };
        cut.Find("[data-testid='plan-previous']").Click();
        cut.WaitForAssertion(() => Assert.Contains("November 2026", Text(cut, "plan-month")));
        Assert.Empty(cut.FindAll($"[data-testid='month-booking-{assigned.Id}']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='month-bookings-empty']"));
    }

    [Fact]
    public void A_booking_dated_inside_its_plan_month_has_no_note_and_shows_the_account()
    {
        var plain = Booking(new DateOnly(2026, 10, 5), 12m, 2026, 10) with { AccountId = Accounts.Accounts[0].Id };
        var cut = Open(2026, 10, Overview(bookings: plain));

        cut.Find("[data-testid='month-tab-bookings']").Click();

        Assert.Empty(cut.FindAll($"[data-testid='month-booking-other-{plain.Id}']"));
        Assert.Contains("Girokonto", Text(cut, $"month-booking-{plain.Id}"));
    }

    // A8 (R7): Blöcke untereinander auf dem Handy, ab 600 px nebeneinander.
    [Fact]
    public void The_blocks_stand_side_by_side_from_600_px_and_below_each_other_under_it()
    {
        var css = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "app.css"));
        var start = css.IndexOf("/* Monatsseite (Spec 24)", StringComparison.Ordinal);
        Assert.True(start >= 0, "Abschnitt 'Monatsseite (Spec 24)' fehlt in app.css");
        var section = css[start..].Replace("\r\n", "\n");

        var basic = section[section.IndexOf(".month-blocks {", StringComparison.Ordinal)..];
        basic = basic[..basic.IndexOf('}')];
        Assert.Contains("grid-template-columns: 1fr", basic);

        var media = section[section.IndexOf("@media (min-width: 600px)", StringComparison.Ordinal)..];
        media = media[..(media.IndexOf("\n}\n", StringComparison.Ordinal) + 3)];
        Assert.Contains(".month-blocks", media);
        Assert.Contains("repeat(3, 1fr)", media);
    }

    // A13 (R9): Haushalt, nur lesend.
    [Fact]
    public async Task The_household_overview_shows_blocks_gaps_recent_bookings_and_due_payments_without_actions()
    {
        var household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
            [FakeHouseholdApi.Anna(isMe: true), FakeHouseholdApi.Ben()]);
        HouseholdApiFake.Household = household;
        var late = FakeHouseholdApi.OwnedPosition("Miete", 890m, FakeHouseholdApi.AnnaId, "Anna") with { DueDay = 18 };
        var soon = FakeHouseholdApi.OwnedPosition("Strom", 80m, FakeHouseholdApi.BenId, "Ben") with { DueDay = 23 };
        var fromAnna = new HouseholdBookingResponse(Guid.NewGuid(), new DateOnly(2026, 10, 12), 40m, 2026, 10, FakeHouseholdApi.AnnaId, "Anna");
        var fromBen = new HouseholdBookingResponse(Guid.NewGuid(), new DateOnly(2026, 10, 14), 25m, 2026, 10, FakeHouseholdApi.BenId, "Ben");
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10) with
        {
            Positions = [late, soon],
            Income = 3000m,
            Fixed = 1200m,
            Free = 1800m,
            Pots = [new HouseholdPotResponse(PotKeys.Needs, 1000m, 1200m), new HouseholdPotResponse(PotKeys.Wants, 0m, 0m), new HouseholdPotResponse(PotKeys.Savings, 0m, 0m)],
            Bookings = [fromBen, fromAnna],
        };
        await Member.LoadAsync();
        Member.ChooseHousehold();

        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-overview']")));

        Assert.Contains("3.000,00 €", Text(cut, "month-block-income"));
        Assert.Contains("1.200,00 €", Text(cut, "month-block-fixed"));
        Assert.Contains("1.800,00 €", Text(cut, "month-block-free"));
        Assert.Contains("200,00 €", Text(cut, "month-gap"));
        Assert.Contains("Ben", Text(cut, $"month-recent-{fromBen.Id}"));
        Assert.Contains("Anna", Text(cut, $"month-recent-{fromAnna.Id}"));
        Assert.Contains("Miete", Text(cut, "month-due-item-overdue"));
        Assert.Contains("Strom", Text(cut, "month-due-item-upcoming"));
        Assert.Empty(cut.FindAll("[data-testid='month-actions']"));
        Assert.Empty(cut.FindAll("[data-testid^='month-action-']"));
    }

    // Spec 66, R9: Der Text am Frei-Betrag sagt, dass der Übertrag enthalten ist, sobald einer da ist.
    [Fact]
    public void The_free_block_says_that_it_contains_the_carry_over_only_when_there_is_one()
    {
        var with = Open(2026, 11, Overview(2500m, 2200m, 530m) with
        {
            CarryOvers = [new PlanCarryOverLineResponse(CarryOverDirections.In, 2026, 10, 230m)],
        });
        Assert.Contains("530,00 €", Text(with, "month-block-free"));
        Assert.Contains("Enthält Übertrag: 230,00 €", Text(with, "month-block-free"));

        var outgoing = Open(2026, 10, Overview(3000m, 2770m, 230m) with
        {
            CarryOvers = [new PlanCarryOverLineResponse(CarryOverDirections.Out, 2026, 11, 230m)],
        });
        Assert.DoesNotContain("Übertrag", Text(outgoing, "month-block-free"));

        var without = Open(2026, 9, Overview(3000m, 2770m, 230m));
        Assert.DoesNotContain("Übertrag", Text(without, "month-block-free"));
    }

    // Die Summe des Haushalts nennt, wessen Übertrag fehlen kann, ohne zu verraten, ob es einen gibt.
    [Fact]
    public async Task The_household_free_block_names_whose_carry_over_it_cannot_contain()
    {
        var household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
            [FakeHouseholdApi.Anna(), FakeHouseholdApi.Ben()]);
        HouseholdApiFake.Household = household;
        HouseholdApiFake.HouseholdPlans[(2026, 11)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 11) with
        {
            Free = 400m,
            CarryOvers = [new PlanCarryOverLineResponse(CarryOverDirections.In, 2026, 10, 100m, "Ben")],
            BookingsMissing = ["Anna"],
        };
        await Member.LoadAsync();
        Member.ChooseHousehold();

        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 11));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-overview']")));

        Assert.Contains("Enthält Übertrag: 100,00 €", Text(cut, "month-block-free"));
        Assert.Contains("Ein möglicher Übertrag von Anna ist nicht enthalten.", Text(cut, "month-block-free"));
    }

    // Spec 66, R9: Der Übertrag ist keine Buchung, sondern eine eigene Zeile in allen drei Tabs.
    [Fact]
    public void Overview_bookings_tab_and_plan_tab_show_the_carry_overs_as_their_own_lines()
    {
        var overview = Overview() with
        {
            CarryOvers =
            [
                new PlanCarryOverLineResponse(CarryOverDirections.In, 2026, 9, 230m),
                new PlanCarryOverLineResponse(CarryOverDirections.Out, 2026, 11, -150m),
            ],
        };

        var cut = Open(2026, 10, overview);

        Assert.Contains("Übertrag aus September", Text(cut, "month-carry-over-in"));
        Assert.Contains("230,00 €", Text(cut, "month-carry-over-in"));
        Assert.Contains("Übertrag nach November", Text(cut, "month-carry-over-out"));
        Assert.Contains("150,00 €", Text(cut, "month-carry-over-out"));
        Assert.Contains("Übertrag aus September", Text(cut, "month-recent"));
        Assert.Empty(cut.FindAll("[data-testid='month-recent-empty']"));

        cut.Find("[data-testid='month-tab-bookings']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Übertrag nach November", Text(cut, "month-bookings")));
        Assert.Contains("Übertrag aus September", Text(cut, "month-bookings"));
        Assert.Empty(cut.FindAll("[data-testid='month-bookings-empty']"));

        cut.Find("[data-testid='month-tab-plan']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Übertrag nach November", Text(cut, "plan-carry-over")));
        Assert.Contains("Übertrag aus September", Text(cut, "plan-carry-over"));
    }

    // Zurücknehmen bleibt im Plan; die Zeile ersetzt dort den Satz über den Vormonat.
    [Fact]
    public void The_plan_tab_names_the_line_and_keeps_the_way_to_remove_the_carry_over()
    {
        var overview = Overview() with { CarryOvers = [new PlanCarryOverLineResponse(CarryOverDirections.In, 2026, 9, 230m)] };
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with
        {
            Overview = overview,
            CarryOver = new PlanCarryOverResponse(230m, true, 230m, []),
        };
        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-tab-plan']")));

        cut.Find("[data-testid='month-tab-plan']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Übertrag aus September", Text(cut, "plan-carry-over")));
        Assert.DoesNotContain("Vormonat", Text(cut, "plan-carry-over"));
        Assert.NotEmpty(cut.FindAll("[data-testid='plan-carry-over-remove']"));
    }

    // Im Haushalt steht dabei, wessen Übertrag es ist; in den Plan-Tab gehört die Zeile ebenso.
    [Fact]
    public async Task The_household_shows_a_carry_over_as_a_line_with_the_first_name()
    {
        var cut = await OpenHouseholdAsync([], [], [new PlanCarryOverLineResponse(CarryOverDirections.Out, 2026, 11, 230m, "Anna")]);

        Assert.Contains("Übertrag nach November", Text(cut, "month-recent"));
        Assert.Contains("Anna", Text(cut, "month-carry-over-out"));

        cut.Find("[data-testid='month-tab-bookings']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Übertrag nach November", Text(cut, "month-bookings")));

        cut.Find("[data-testid='month-tab-plan']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Anna", Text(cut, "household-carry-overs")));
    }

    private async Task<IRenderedComponent<Plan>> OpenHouseholdAsync(
        IReadOnlyList<HouseholdBookingResponse> bookings, IReadOnlyList<string> missing, IReadOnlyList<PlanCarryOverLineResponse>? carryOvers = null)
    {
        var household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
            [FakeHouseholdApi.Anna(isMe: true), FakeHouseholdApi.Ben()]);
        HouseholdApiFake.Household = household;
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10) with
        {
            Bookings = bookings,
            BookingsMissing = missing,
            CarryOvers = carryOvers,
        };
        await Member.LoadAsync();
        Member.ChooseHousehold();
        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-recent']")));
        return cut;
    }

    // Fund S6: "noch keine Buchungen" wäre falsch, wenn Buchungen nur nicht freigegeben sind.
    [Fact]
    public async Task The_household_overview_names_missing_members_instead_of_saying_there_are_no_bookings()
    {
        var cut = await OpenHouseholdAsync([], ["Ben"]);

        Assert.Contains("Nicht freigegebene Buchungen von: Ben", Text(cut, "month-recent"));
        Assert.Empty(cut.FindAll("[data-testid='month-recent-empty']"));
    }

    [Fact]
    public async Task The_household_overview_shows_the_visible_bookings_and_still_names_missing_members()
    {
        var fromAnna = new HouseholdBookingResponse(Guid.NewGuid(), new DateOnly(2026, 10, 12), 40m, 2026, 10, FakeHouseholdApi.AnnaId, "Anna");

        var cut = await OpenHouseholdAsync([fromAnna], ["Ben"]);

        Assert.Contains("Anna", Text(cut, $"month-recent-{fromAnna.Id}"));
        Assert.Contains("Nicht freigegebene Buchungen von: Ben", Text(cut, "month-recent"));
    }

    [Fact]
    public async Task The_household_overview_says_there_are_no_bookings_when_nobody_is_missing()
    {
        var cut = await OpenHouseholdAsync([], []);

        Assert.Contains("noch keine Buchungen", Text(cut, "month-recent-empty"));
        Assert.Empty(cut.FindAll("[data-testid='month-recent-missing']"));
    }

    [Fact]
    public async Task The_household_bookings_tab_names_members_who_did_not_grant_their_bookings()
    {
        var household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
            [FakeHouseholdApi.Anna(isMe: true), FakeHouseholdApi.Ben()]);
        HouseholdApiFake.Household = household;
        var fromAnna = new HouseholdBookingResponse(Guid.NewGuid(), new DateOnly(2026, 10, 12), 40m, 2026, 10, FakeHouseholdApi.AnnaId, "Anna");
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10) with
        {
            Bookings = [fromAnna],
            BookingsMissing = ["Ben"],
        };
        await Member.LoadAsync();
        Member.ChooseHousehold();
        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-tab-bookings']")));

        cut.Find("[data-testid='month-tab-bookings']").Click();

        Assert.Contains("Anna", Text(cut, $"month-booking-{fromAnna.Id}"));
        Assert.Contains("Nicht freigegebene Buchungen von: Ben", Text(cut, "month-bookings-missing"));
    }

    private async Task<IRenderedComponent<Plan>> OpenHouseholdOverviewAsync(IReadOnlyList<HouseholdBookingResponse> bookings, IReadOnlyList<string> missing)
    {
        var household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
            [FakeHouseholdApi.Anna(isMe: true), FakeHouseholdApi.Ben()]);
        HouseholdApiFake.Household = household;
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10) with
        {
            Bookings = bookings,
            BookingsMissing = missing,
        };
        await Member.LoadAsync();
        Member.ChooseHousehold();
        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-overview']")));
        return cut;
    }

    // Spec 46, A7: die letzten Buchungen des Haushalts nennen, wessen Buchungen nicht freigegeben sind.
    [Fact]
    public async Task The_household_overview_names_members_whose_recent_bookings_are_not_released()
    {
        var fromAnna = new HouseholdBookingResponse(Guid.NewGuid(), new DateOnly(2026, 10, 12), 40m, 2026, 10, FakeHouseholdApi.AnnaId, "Anna");

        var cut = await OpenHouseholdOverviewAsync([fromAnna], ["Ben"]);

        Assert.Contains("Anna", Text(cut, $"month-recent-{fromAnna.Id}"));
        Assert.Contains("Nicht freigegebene Buchungen von: Ben", Text(cut, "month-recent-missing"));
    }

    // Spec 46, A8: ohne eine sichtbare Buchung steht nicht da, der Monat habe keine Buchungen.
    [Fact]
    public async Task The_household_overview_does_not_claim_an_empty_month_while_bookings_are_not_released()
    {
        var cut = await OpenHouseholdOverviewAsync([], ["Ben"]);

        Assert.Contains("Nicht freigegebene Buchungen von: Ben", Text(cut, "month-recent-missing"));
        Assert.Empty(cut.FindAll("[data-testid='month-recent-empty']"));
    }

    // Spec 46, A9: ohne fehlende Namen und bei einer Person gibt es den Hinweis nicht.
    [Fact]
    public async Task Without_missing_names_and_for_a_single_person_the_overview_has_no_notice()
    {
        var fromAnna = new HouseholdBookingResponse(Guid.NewGuid(), new DateOnly(2026, 10, 12), 40m, 2026, 10, FakeHouseholdApi.AnnaId, "Anna");
        var household = await OpenHouseholdOverviewAsync([fromAnna], []);
        var person = Open(2026, 10, Overview());

        Assert.Empty(household.FindAll("[data-testid='month-recent-missing']"));
        Assert.Empty(person.FindAll("[data-testid='month-recent-missing']"));
    }
}
