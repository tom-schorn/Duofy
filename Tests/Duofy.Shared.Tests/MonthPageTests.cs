using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Duofy.Shared.Resources;
using Microsoft.Extensions.Localization;
using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;

namespace Duofy.Shared.Tests;

/// <summary>
/// Die Monatsseite /plan/{Jahr}/{Monat} mit den Reitern Plan, Buchungen und Auswertung (Spec 24, Spec 82). Der Reiter
/// Übersicht ist entfallen: Aktionen im Seitenkopf, Fälliges, Lücke und Quoten in Auswertung, alle Buchungen im Reiter Buchungen.
/// Die Tests der Posten stehen in PlanPageTests und den Nachbardateien.
/// "Heute" ist der 21. Oktober 2026.
/// </summary>
public class MonthPageTests : PageTestContext
{
    private readonly IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> dialogs;

    private ActiveMember Member => Services.GetRequiredService<ActiveMember>();

    public MonthPageTests()
    {
        dialogs = RenderDialogHost();
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
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-metrics']")));
        return cut;
    }

    private static string Text(IRenderedComponent<Plan> cut, string testId) => cut.Find($"[data-testid='{testId}']").TextContent;

    private static bool Selected(IRenderedComponent<Plan> cut, string tab) =>
        cut.Find($"[data-testid='month-tab-{tab}']").GetAttribute("aria-selected") == "true";

    // A1 (R1), Toms Entscheidung vom 02.11.: kein Reiter Übersicht; der Monat öffnet mit dem Plan.
    [Fact]
    public void The_month_opens_with_the_tabs_plan_bookings_and_review_and_the_plan_is_active()
    {
        var cut = Open(2026, 10, Overview());

        Assert.Equal(["Plan", "Buchungen", "Auswertung"], cut.FindAll("[role='tab']").Select(t => t.TextContent.Trim()));
        Assert.True(Selected(cut, "plan"));
        Assert.False(Selected(cut, "bookings"));
        Assert.Empty(cut.FindAll("[data-testid='month-tab-overview']"));
        Assert.Empty(cut.FindAll("[data-testid='month-overview']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='plan-carry-over']"));
        Assert.Empty(cut.FindAll("[data-testid='month-review']"));
        Assert.Equal("tablist", cut.Find(".month-tabs").GetAttribute("role"));
    }

    // A1 (R1): der gewählte Tab bleibt, wenn die Adresse zu einem anderen Monat wechselt (Spec 82: ohne Pfeile im Monat).
    [Fact]
    public void The_chosen_tab_stays_while_paging_between_months()
    {
        var cut = Open(2026, 10, Overview());

        OpenReviewTab(cut);
        cut.Render(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 11));

        cut.WaitForAssertion(() => Assert.Contains("November 2026", Text(cut, "page-title")));
        Assert.True(Selected(cut, "review"));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-review']")));
    }

    [Fact]
    public void The_arrow_keys_move_to_the_neighbouring_tab()
    {
        var cut = Open(2026, 10, Overview());

        cut.Find("[data-testid='month-tab-plan']").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "ArrowRight" });

        Assert.True(Selected(cut, "bookings"));
    }

    // A10 (Spec 82), A17 (R15): vier Kennzahlen mit den Beträgen der API; Anstehend ist der offene Rest der Posten, der Übertrag
    // ist die vierte.
    [Fact]
    public void The_four_blocks_show_income_allocatable_what_is_still_due_and_the_carry_over()
    {
        var cut = Open(2026, 10, Overview(3000m, 1200m, 1800m), FakePlanApi.Position("Strom", 80m, booked: 30m));

        Assert.Contains("Einnahmen", Text(cut, "month-block-income"));
        Assert.Contains("3.000,00 €", Text(cut, "month-block-income"));
        Assert.Contains("Verplanbar", Text(cut, "month-block-free"));
        Assert.Contains("1.800,00 €", Text(cut, "month-block-free"));
        Assert.Contains("Anstehend", Text(cut, "month-block-unpaid"));
        Assert.Contains("50,00 €", Text(cut, "month-block-unpaid"));
        Assert.Equal(4, cut.Find("[data-testid='month-metrics']").Children.Length);
        Assert.Contains("Übertrag", Text(cut, "plan-carry-over"));
        Assert.All(cut.Find("[data-testid='month-metrics']").Children, block => Assert.NotNull(block.QuerySelector(".metric")));
    }

    // A6 (R6)
    [Fact]
    public void Free_at_zero_says_everything_is_planned()
    {
        var cut = Open(2026, 10, Overview(3000m, 3000m, 0m));

        Assert.Contains("Alles verplant", Text(cut, "month-block-free"));
        Assert.DoesNotContain("0,00", Text(cut, "month-block-free"));
    }

    // A3 (R3); Tom, 03.11.2026: Ziel und Ist je Topf stehen in der Legende des Topf-Diagramms, nicht als eigene Balken, und
    // unter der Auswertung steht kein Textkasten mit Summen mehr.
    [Fact]
    public void The_pot_chart_legend_shows_target_and_actual_of_each_pot_and_there_is_no_sums_box()
    {
        var plan = FakePlanApi.Empty(2026, 10) with
        {
            Quotas = [new PlanQuotaResponse(PotKeys.Needs, 50, 60m), new PlanQuotaResponse(PotKeys.Wants, 30, 10m), new PlanQuotaResponse(PotKeys.Savings, 20, 0m)],
            Overview = Overview(),
            BookedIncome = 1000m,
        };
        PlanApiFake.Plans[(2026, 10)] = plan;
        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        OpenReviewTab(cut);

        var head = cut.FindAll("[data-testid='month-review-pots-chart'] thead th").Select(th => th.TextContent.Trim()).ToList();
        Assert.Equal(["Bezeichnung", "Geplant", "Gebucht", "Ziel", "Ist"], head);
        var needs = cut.FindAll("[data-testid='month-review-pots-chart-row-needs'] td").Select(td => td.TextContent.Trim()).ToList();
        Assert.Equal(["50 %", "60 %"], needs[^2..]);
        var wants = cut.FindAll("[data-testid='month-review-pots-chart-row-wants'] td").Select(td => td.TextContent.Trim()).ToList();
        Assert.Equal(["30 %", "10 %"], wants[^2..]);
        Assert.Empty(cut.FindAll("[data-testid='month-pots'], [data-testid='plan-sums'], [data-testid='month-review'] .chart-target-bar"));
    }

    // A4 (R4), seit Spec 82 R14 im runden Diagramm: je Topf geplant und gebucht in Euro, ohne Alarmfarbe.
    [Fact]
    public void The_pot_chart_shows_planned_against_booked_in_euro_without_an_alarm_colour()
    {
        var cut = Open(2026, 10, Overview(pots: [
            new PlanPotResponse(PotKeys.Needs, 1000m, 1200m),
            new PlanPotResponse(PotKeys.Wants, 100m, 40m),
            new PlanPotResponse(PotKeys.Savings, 200m, 200m)]));
        OpenReviewTab(cut);

        var rows = cut.FindAll("[data-testid='month-review-pots-chart'] .donut-legend tbody tr")
            .Select(r => string.Join(" ", r.Children.Take(3).Select(c => c.TextContent.Trim()))).ToList();
        Assert.Equal(["Grundbedarf 1.000,00 € 1.200,00 €", "Wünsche 100,00 € 40,00 €", "Sparen 200,00 € 200,00 €"], rows);
        var markup = cut.Find("[data-testid='month-review-pots-chart']").InnerHtml;
        Assert.DoesNotContain("over", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("error", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("danger", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("warning", markup, StringComparison.OrdinalIgnoreCase);
    }

    // Tom, 03.11.2026: In der Auswertung stehen alle Konten der Person, getrennt nach verfügbar und zurückgelegt, mit Summe.
    [Fact]
    public void The_review_lists_the_accounts_split_into_available_and_reserved_with_a_total()
    {
        var giro = new AccountResponse(Guid.NewGuid(), "Girokonto", "checking", 0m, new DateOnly(2026, 1, 1), true, null, true, true, 812.40m);
        var cash = new AccountResponse(Guid.NewGuid(), "Bargeld", "cash", 0m, new DateOnly(2026, 1, 1), true, null, false, true, 60m);
        var reserve = new AccountResponse(Guid.NewGuid(), "Rücklage", "savings", 0m, new DateOnly(2026, 1, 1), false, null, false, true, 2500m);
        AccountApiFake.Accounts.AddRange([giro, cash, reserve]);
        var cut = Open(2026, 10, Overview());
        OpenReviewTab(cut);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-accounts']")));

        var available = Text(cut, "month-accounts-group-available");
        Assert.Contains("Girokonto", available);
        Assert.Contains("812,40 €", available);
        Assert.Contains("Bargeld", available);
        Assert.Contains("872,40 €", available);
        var reserved = Text(cut, "month-accounts-group-reserved");
        Assert.Contains("Rücklage", reserved);
        Assert.Contains("Sparkonto", reserved);
        Assert.DoesNotContain("Girokonto", reserved);
        Assert.Contains("3.372,40 €", Text(cut, "month-accounts-total"));
    }

    // Toms Entscheidung vom 02.11.: Die Liste der fünf neuesten fällt mit der Übersicht weg, der Reiter Buchungen zeigt alle.
    [Fact]
    public void The_bookings_tab_lists_all_seven_bookings_newest_first_with_date_and_amount()
    {
        var bookings = Enumerable.Range(1, 7).Select(d => Booking(new DateOnly(2026, 10, d), d * 10m, 2026, 10)).Reverse().ToArray();
        var cut = Open(2026, 10, Overview(bookings: bookings));

        OpenBookingsTab(cut);

        var rows = cut.FindAll("[data-testid='month-bookings'] tr[data-testid^='month-booking-']");
        Assert.Equal(7, rows.Count);
        Assert.Contains("07.10.2026", rows[0].TextContent);
        Assert.Contains("70,00 €", rows[0].TextContent);
        Assert.Contains("01.10.2026", rows[6].TextContent);
        Assert.Contains("· 7", Text(cut, "month-tab-bookings"));
    }

    // A11 (R11)
    [Fact]
    public void Soon_due_and_overdue_payments_are_both_listed_and_marked_apart()
    {
        var soon = FakePlanApi.Position("Strom", 80m, day: 23);
        var late = FakePlanApi.Position("Miete", 890m, day: 18);
        var cut = Open(2026, 10, Overview(), soon, late);
        OpenReviewTab(cut);

        var due = Text(cut, "month-due");
        Assert.Contains("Strom", due);
        Assert.Contains("Miete", due);
        Assert.Single(cut.FindAll("[data-testid='month-overdue-badge']"));
        Assert.Single(cut.FindAll("[data-testid='month-upcoming-badge']"));
        Assert.Contains("überfällig", Text(cut, "month-due-item-overdue"));
        Assert.Contains("Miete", Text(cut, "month-due-item-overdue"));
        Assert.Contains("anstehend", Text(cut, "month-due-item-upcoming"));
    }

    // Toms Entscheidung vom 02.11.: Die Aktionen stehen im Seitenkopf. "Posten abhaken" entfällt, das Kästchen steht direkt am Posten.
    [Fact]
    public void The_page_head_offers_new_booking_and_new_position()
    {
        var cut = Open(2026, 10, Overview());

        Assert.Equal("Buchung anlegen", Text(cut, "month-action-booking").Trim());
        Assert.Equal("Posten anlegen", Text(cut, "plan-add").Trim());
        Assert.Empty(cut.FindAll("[data-testid='month-action-tick']"));
    }

    [Fact]
    public void New_booking_opens_its_dialog_and_cancelling_leaves_the_month_as_it_is()
    {
        var cut = Open(2026, 10, Overview());

        cut.Find("[data-testid='month-action-booking']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll(".step-dialog")));
        dialogs.Find("[data-testid='step-dialog-cancel']").Click();

        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindAll(".step-dialog")));
        Assert.True(Selected(cut, "plan"));
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

        cut.Find("[data-testid='plan-add']").Click();

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='position-label']")));
    }

    // A5 (R5), A10 (Spec 82), A20 (R17): Posten nach Töpfen mit Betrag und Kästchen; was ohne Posten gebucht ist, steht in der
    // eigenen Zeile Ungeplant außerhalb der Töpfe, und die Summe eines Topfs ist sein Geplantes wie in der Übersicht.
    [Fact]
    public void The_plan_tab_groups_positions_by_pot_with_amount_tick_box_and_unplanned_line()
    {
        var rent = FakePlanApi.Position("Miete", 890m);
        var fun = FakePlanApi.Position("Streaming", 12.99m, PotKeys.Wants);
        var loose = new BookingResponse(Guid.NewGuid(), BookingKinds.Expense, Guid.NewGuid(), null, new DateOnly(2026, 10, 3), 45m, null,
            [new BookingLineResponse(Guid.NewGuid(), 45m, PotKeys.Wants, FakeCategoryApi.Restaurant.Id)], 2026, 10);
        var cut = Open(2026, 10, Overview(pots: [new PlanPotResponse(PotKeys.Needs, 890m, 0m), new PlanPotResponse(PotKeys.Wants, 12.99m, 45m), new PlanPotResponse(PotKeys.Savings, 0m, 0m)], bookings: loose), rent, fun);

        Assert.Equal(["Grundbedarf", "Wünsche"], cut.FindAll("[data-testid^='plan-group-']").Select(g => g.TextContent.Trim()));
        var needs = cut.Find($"[data-testid='pot-section-{PotKeys.Needs}']");
        Assert.NotNull(needs.QuerySelector($"[data-testid='plan-tick-{rent.Id}']"));
        Assert.Contains("890,00 €", needs.QuerySelector($"[data-testid='plan-amounts-{rent.Id}']")!.TextContent);
        var wants = cut.Find($"[data-testid='pot-section-{PotKeys.Wants}']");
        Assert.Contains("12,99 €", wants.QuerySelector($"[data-testid='plan-amounts-{fun.Id}']")!.TextContent);
        var unplanned = wants.QuerySelector("[data-testid='plan-unplanned-wants']")!.TextContent;
        Assert.Contains("Ungeplant · 1 Buchung · 45,00 €", unplanned);
        Assert.Empty(cut.FindAll("[data-testid='plan-unplanned']"));
        Assert.Contains("12,99 €", Text(cut, $"pot-sum-{PotKeys.Wants}"));
        Assert.DoesNotContain("57,99", Text(cut, $"pot-sum-{PotKeys.Wants}"));

        // Tom, 03.11.2026: Den Sankey gibt es nirgends mehr, weder im Plan noch in der Auswertung.
        Assert.Empty(cut.FindAll(".flow-chart, [data-testid='month-flow']"));
        OpenReviewTab(cut);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-review']")));
        Assert.Empty(cut.FindAll(".flow-chart, [data-testid='month-flow']"));
    }

    // A8 (Issue #94, R8): je Topf eine Zeile Ungeplant am Ende, ohne Kästchen, aufklappbar zu den Buchungen
    [Fact]
    public void A8_unplanned_bookings_form_one_row_per_pot()
    {
        var groceries = FakePlanApi.Position("Lebensmittel", 400m);
        BookingResponse Loose(decimal amount, string pot = PotKeys.Needs, Guid? position = null) =>
            new(Guid.NewGuid(), BookingKinds.Expense, Guid.NewGuid(), null, new DateOnly(2026, 10, 3), amount, null,
                [new BookingLineResponse(Guid.NewGuid(), amount, pot, FakeCategoryApi.Groceries.Id, position)], 2026, 10);
        var bookings = new[] { Loose(30m), Loose(28.89m), Loose(30m), Loose(20m, position: groceries.Id) };
        var cut = Open(2026, 10, Overview(pots: [new PlanPotResponse(PotKeys.Needs, 400m, 108.89m), new PlanPotResponse(PotKeys.Wants, 0m, 0m), new PlanPotResponse(PotKeys.Savings, 0m, 0m)], bookings: bookings), groceries);

        var needs = cut.Find($"[data-testid='pot-section-{PotKeys.Needs}']");
        var row = needs.QuerySelector("[data-testid='plan-unplanned-needs']")!;
        Assert.Contains("Ungeplant · 3 Buchungen · 88,89 €", row.TextContent);
        Assert.Empty(row.QuerySelectorAll("[role='checkbox'], input[type='checkbox'], .tick-box"));
        Assert.Equal(row, needs.QuerySelectorAll("ul.pot-rows > li").Last());
        Assert.Empty(cut.FindAll("[data-testid='plan-unplanned-wants']"));

        var toggle = row.QuerySelector("[data-testid='plan-unplanned-needs-toggle']")!;
        var panel = row.QuerySelector("[data-testid='plan-unplanned-needs-bookings']")!;
        Assert.Equal("false", toggle.GetAttribute("aria-expanded"));
        Assert.NotNull(panel.GetAttribute("hidden"));
        toggle.Click();
        Assert.Equal("true", cut.Find("[data-testid='plan-unplanned-needs-toggle']").GetAttribute("aria-expanded"));
        panel = cut.Find("[data-testid='plan-unplanned-needs-bookings']");
        Assert.Null(panel.GetAttribute("hidden"));
        Assert.Equal(3, panel.QuerySelectorAll("[data-testid^='position-booking-']").Length);
    }

    // Review M5: Eine Umbuchung hat Topfzeilen ohne Posten, ist aber nie ungeplant (wie PlanFigures.Unplanned); weder eine Zeile
    // Ungeplant noch ein sichtbarer Topf entsteht daraus, und die Helfer stimmen mit dem gebuchten Topfwert überein.
    [Fact]
    public void M5_a_transfer_with_pot_lines_is_not_shown_as_unplanned()
    {
        var rent = FakePlanApi.Position("Miete", 400m);
        var transfer = new BookingResponse(Guid.NewGuid(), BookingKinds.Transfer, Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 4), 150m, null,
            [new BookingLineResponse(Guid.NewGuid(), 150m, PotKeys.Savings, null)], 2026, 10, PotKeys.Savings);
        var loose = new BookingResponse(Guid.NewGuid(), BookingKinds.Expense, Guid.NewGuid(), null, new DateOnly(2026, 10, 3), 30m, null,
            [new BookingLineResponse(Guid.NewGuid(), 30m, PotKeys.Needs, FakeCategoryApi.Groceries.Id)], 2026, 10);
        var cut = Open(2026, 10, Overview(pots: [new PlanPotResponse(PotKeys.Needs, 400m, 30m), new PlanPotResponse(PotKeys.Wants, 0m, 0m), new PlanPotResponse(PotKeys.Savings, 0m, 0m)], bookings: [transfer, loose]), rent);

        Assert.Empty(cut.FindAll("[data-testid='pot-section-savings']"));
        Assert.Empty(cut.FindAll("[data-testid='plan-unplanned-savings']"));
        Assert.Contains("Ungeplant · 1 Buchung · 30,00 €", cut.Find("[data-testid='plan-unplanned-needs']").TextContent);
    }

    // Review M5: dieselbe Regel im Helfer für die Summe und in dem für die Zeilen
    [Fact]
    public void M5_both_helpers_leave_transfers_out()
    {
        var transfer = new BookingResponse(Guid.NewGuid(), BookingKinds.Transfer, Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 4), 150m, null,
            [new BookingLineResponse(Guid.NewGuid(), 150m, PotKeys.Savings, null)], 2026, 10, PotKeys.Savings);
        var plan = FakePlanApi.Empty(2026, 10) with { Overview = new PlanOverviewResponse(0m, 0m, 0m, [], [transfer]) };

        Assert.Equal(0m, PlanView.Unplanned(plan, PotKeys.Savings));
        Assert.Empty(BookingPurpose.UnplannedLinesOf([transfer], PotKeys.Savings, _ => null, Services.GetRequiredService<IStringLocalizer<SharedResources>>()));
        Assert.Empty(PlanView.GroupsWithUnplanned(plan));
    }

    // A9 (Issue #94, R9): die Karte der geänderten Verträge ist eine Zeile; ein Tipp zeigt Liste und Knöpfe
    [Fact]
    public void A9_the_contract_change_card_is_one_line()
    {
        var rent = FakePlanApi.Position("Miete", 890m);
        var power = FakePlanApi.Position("Strom", 80m);
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with
        {
            Positions = [rent, power],
            Changes =
            [
                new PlanChangeResponse(PlanChangeKinds.Changed, rent.Id, rent.CommitmentId!.Value, "Miete", [new PlanFieldChangeResponse(PlanFields.Amount, "890.00", "950.00")], Protected: false),
                new PlanChangeResponse(PlanChangeKinds.Changed, power.Id, power.CommitmentId!.Value, "Strom", [new PlanFieldChangeResponse(PlanFields.Amount, "80.00", "90.00")], Protected: false),
            ],
        };
        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='plan-changes']")));

        var toggle = cut.Find("[data-testid='plan-changes-toggle']");
        Assert.Equal("2 Verträge geändert · Ansehen", toggle.TextContent.Trim());
        Assert.Equal("false", toggle.GetAttribute("aria-expanded"));
        Assert.NotNull(cut.Find("[data-testid='plan-changes-panel']").GetAttribute("hidden"));

        toggle.Click();

        Assert.Equal("true", cut.Find("[data-testid='plan-changes-toggle']").GetAttribute("aria-expanded"));
        var panel = cut.Find("[data-testid='plan-changes-panel']");
        Assert.Null(panel.GetAttribute("hidden"));
        Assert.Equal(2, panel.QuerySelectorAll("[data-testid='plan-change']").Length);
        Assert.Equal(2, panel.QuerySelectorAll("[data-testid='plan-change-apply']").Length);
        Assert.NotNull(panel.QuerySelector("[data-testid='plan-changes-apply-all']"));
    }

    [Fact]
    public void One_changed_contract_reads_in_the_singular()
    {
        var rent = FakePlanApi.Position("Miete", 890m);
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with
        {
            Positions = [rent],
            Changes = [new PlanChangeResponse(PlanChangeKinds.Changed, rent.Id, rent.CommitmentId!.Value, "Miete", [], Protected: false)],
        };
        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='plan-changes']")));

        Assert.Equal("1 Vertrag geändert · Ansehen", cut.Find("[data-testid='plan-changes-toggle']").TextContent.Trim());
    }

    // A11 (Spec 82): Das Kästchen öffnet den Dialog mit Datum und Betrag; erst die Bestätigung bucht, und zwar genau einmal.
    [Fact]
    public void The_tick_box_opens_a_dialog_with_date_and_amount_and_confirming_books_exactly_once()
    {
        var rent = FakePlanApi.Position("Miete", 890m);
        var cut = Open(2026, 10, Overview(), rent);

        cut.Find($"[data-testid='plan-tick-{rent.Id}']").Click();

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='tick-save']")));
        Assert.Empty(PlanApiFake.Ticks);
        Assert.Equal("2026-10-21", Field(dialogs, "tick-date").GetAttribute("value"));
        Assert.Equal("890,00", Field(dialogs, "tick-amount").GetAttribute("value"));

        dialogs.Find("[data-testid='tick-save']").Click();

        cut.WaitForAssertion(() => Assert.Equal(rent.Id, Assert.Single(PlanApiFake.Ticks).Id));
        Assert.Equal(890m, PlanApiFake.Ticks[0].Request.Amount);
    }

    // A11: Abbrechen bucht nichts, und das Kästchen bleibt leer.
    [Fact]
    public void Cancelling_the_tick_dialog_books_nothing_and_leaves_the_box_empty()
    {
        var rent = FakePlanApi.Position("Miete", 890m);
        var cut = Open(2026, 10, Overview(), rent);

        cut.Find($"[data-testid='plan-tick-{rent.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='tick-cancel']")));
        dialogs.Find("[data-testid='tick-cancel']").Click();

        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindAll("[data-testid='tick-cancel']")));
        Assert.Empty(PlanApiFake.Ticks);
        Assert.Equal("false", cut.Find($"[data-testid='plan-tick-{rent.Id}']").GetAttribute("aria-checked"));
    }

    // A9 (R8)
    [Fact]
    public void The_bookings_tab_of_december_holds_a_booking_of_november_20_with_a_note_and_november_does_not()
    {
        var assigned = Booking(new DateOnly(2026, 11, 20), 45m, 2026, 12);
        var cut = Open(2026, 12, Overview(bookings: assigned));

        OpenBookingsTab(cut);

        var row = Text(cut, $"month-booking-{assigned.Id}");
        Assert.Contains("20.11.2026", row);
        Assert.Contains("45,00 €", row);
        Assert.Contains("Datum im November", Text(cut, $"month-booking-other-{assigned.Id}"));

        PlanApiFake.Plans[(2026, 11)] = FakePlanApi.Empty(2026, 11) with { Overview = Overview() };
        cut.Render(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 11));
        cut.WaitForAssertion(() => Assert.Contains("November 2026", Text(cut, "page-title")));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-bookings-empty']")));
        Assert.Empty(cut.FindAll($"[data-testid='month-booking-{assigned.Id}']"));
    }

    [Fact]
    public void A_booking_dated_inside_its_plan_month_has_no_note_and_shows_the_account()
    {
        var plain = Booking(new DateOnly(2026, 10, 5), 12m, 2026, 10) with { AccountId = Accounts.Accounts[0].Id };
        var cut = Open(2026, 10, Overview(bookings: plain));

        OpenBookingsTab(cut);

        Assert.Empty(cut.FindAll($"[data-testid='month-booking-other-{plain.Id}']"));
        Assert.Contains("Girokonto", Text(cut, $"month-booking-{plain.Id}"));
    }

    // A8 (R7), seit Spec 101: Die Kennzahlen stehen in einer Reihe; unter 600 px zwei mal zwei, ohne Wischstreifen.
    [Fact]
    public void The_blocks_stand_in_one_row_and_as_two_by_two_on_phones()
    {
        var css = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "duofy.css")).Replace("\r\n", "\n");

        var basic = css[css.IndexOf(".metrics {", StringComparison.Ordinal)..];
        // A17 (R15): vier gleich breite Spalten; gleich hoch, weil jede Kennzahl ihre Zelle füllt.
        Assert.Contains("repeat(4, minmax(0, 1fr))", basic[..basic.IndexOf('}')]);
        Assert.Contains(".metrics > * > .metric { flex: 1 1 auto; }", css);

        var phone = css[css.IndexOf("@media (max-width: 599px) {\n    .narrow-only", StringComparison.Ordinal)..];
        var grid = phone[phone.IndexOf("    .metrics {", StringComparison.Ordinal)..];
        grid = grid[..grid.IndexOf('}')];
        Assert.Contains("repeat(2, minmax(0, 1fr))", grid);
        Assert.DoesNotContain("display: flex", grid);
        Assert.DoesNotContain("overflow-x", grid);
        Assert.DoesNotContain("scroll-snap-type", grid);    }

    // Spec 82, R12: Am Handy bleibt "Buchung anlegen" der Hauptknopf; Posten anlegen und Drucken stehen im Menü.
    [Fact]
    public void On_phones_the_month_has_one_main_button_and_a_menu()
    {
        var cut = Open(2026, 10, Overview());

        Assert.DoesNotContain("wide-only", cut.Find("[data-testid='month-action-booking']").ClassList);
        Assert.Contains("wide-only", cut.Find("[data-testid='plan-add']").ClassList);
        Assert.Contains("wide-only", cut.Find("[data-testid='plan-print']").ClassList);
        Assert.Equal("Weitere Aktionen", cut.Find("[data-testid='page-more']").GetAttribute("aria-label"));
        Assert.NotNull(cut.Find("[data-testid='page-more']").Closest(".narrow-only"));
    }

    // A13 (R9): Haushalt, nur lesend.
    [Fact]
    public async Task The_household_month_shows_blocks_gaps_bookings_and_due_payments_without_actions()
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
        OpenReviewTab(cut);

        Assert.Contains("3.000,00 €", Text(cut, "month-block-income"));
        Assert.Contains("1.800,00 €", Text(cut, "month-block-free"));
        Assert.Contains("1.200,00 €", Text(cut, "month-review-plan-vs-actual"));
        Assert.NotEmpty(cut.FindAll("[data-testid='month-curve']"));
        Assert.Contains("Miete", Text(cut, "month-due-item-overdue"));
        Assert.Contains("Strom", Text(cut, "month-due-item-upcoming"));
        // Spec 125: Die Zentrale hat dieselben Knöpfe wie der eigene Plan.
        Assert.NotEmpty(cut.FindAll("[data-testid='month-action-booking']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='plan-add']"));

        OpenBookingsTab(cut);

        Assert.Contains("Ben", Text(cut, $"month-booking-{fromBen.Id}"));
        Assert.Contains("Anna", Text(cut, $"month-booking-{fromAnna.Id}"));
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
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-metrics']")));

        Assert.Contains("Enthält Übertrag: 100,00 €", Text(cut, "month-block-free"));
        Assert.Contains("Ein möglicher Übertrag von Anna ist nicht enthalten.", Text(cut, "month-block-free"));
    }

    // Spec 66, R9: Der Übertrag ist keine Buchung, sondern eine eigene Zeile in den Reitern Plan und Buchungen.
    [Fact]
    public void Bookings_tab_and_plan_tab_show_the_carry_overs_as_their_own_lines()
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
        OpenBookingsTab(cut);

        Assert.Contains("Übertrag aus September", Text(cut, "month-carry-over-in"));
        Assert.Contains("230,00 €", Text(cut, "month-carry-over-in"));
        Assert.Contains("Übertrag nach November", Text(cut, "month-carry-over-out"));
        Assert.Contains("150,00 €", Text(cut, "month-carry-over-out"));
        Assert.Empty(cut.FindAll("[data-testid='month-bookings-empty']"));

        OpenPlanTab(cut);

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

        Assert.Contains("Übertrag nach November", Text(cut, "month-bookings"));
        Assert.Contains("Anna", Text(cut, "month-carry-over-out"));

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
        OpenBookingsTab(cut);
        return cut;
    }

    // Fund S6, Spec 46, A8: "noch keine Buchungen" wäre falsch, wenn Buchungen nur nicht freigegeben sind.
    [Fact]
    public async Task The_household_bookings_name_missing_members_instead_of_saying_there_are_no_bookings()
    {
        var cut = await OpenHouseholdAsync([], ["Ben"]);

        Assert.Contains("Nicht freigegebene Buchungen von: Ben", Text(cut, "month-bookings-missing"));
        Assert.Empty(cut.FindAll("[data-testid='month-bookings-empty']"));
    }

    // Spec 46, A7: Die sichtbaren Buchungen stehen da, und trotzdem ist genannt, wessen Buchungen fehlen.
    [Fact]
    public async Task The_household_bookings_show_the_visible_ones_and_still_name_missing_members()
    {
        var fromAnna = new HouseholdBookingResponse(Guid.NewGuid(), new DateOnly(2026, 10, 12), 40m, 2026, 10, FakeHouseholdApi.AnnaId, "Anna");

        var cut = await OpenHouseholdAsync([fromAnna], ["Ben"]);

        Assert.Contains("Anna", Text(cut, $"month-booking-{fromAnna.Id}"));
        Assert.Contains("Nicht freigegebene Buchungen von: Ben", Text(cut, "month-bookings-missing"));
    }

    [Fact]
    public async Task The_household_bookings_say_there_are_none_when_nobody_is_missing()
    {
        var cut = await OpenHouseholdAsync([], []);

        Assert.Contains("noch keine Buchungen", Text(cut, "month-bookings-empty"));
        Assert.Empty(cut.FindAll("[data-testid='month-bookings-missing']"));
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



    // Spec 46, A9: ohne fehlende Namen und bei einer Person gibt es den Hinweis nicht.
    [Fact]
    public async Task Without_missing_names_and_for_a_single_person_the_bookings_have_no_notice()
    {
        var fromAnna = new HouseholdBookingResponse(Guid.NewGuid(), new DateOnly(2026, 10, 12), 40m, 2026, 10, FakeHouseholdApi.AnnaId, "Anna");
        var household = await OpenHouseholdAsync([fromAnna], []);
        Assert.Empty(household.FindAll("[data-testid='month-bookings-missing']"));

        Member.Choose(null);
        var person = Open(2026, 10, Overview());
        OpenBookingsTab(person);

        Assert.Empty(person.FindAll("[data-testid='month-bookings-missing']"));
    }

    // Spec 51, A6: Die Adresse öffnet den Dialog einmal und räumt sich auf; Zurück öffnet ihn nicht wieder.
    [Fact]
    public void The_address_with_buchen_opens_the_dialog_once_and_clears_itself()
    {
        var dialogs = RenderDialogHost();
        Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true));
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with { Overview = Overview() };
        Navigation.NavigateTo("/plan/2026/10?buchen=1");

        Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='booking-plan-month']")));
        Assert.Equal("2026-10", dialogs.Find("[data-testid='booking-plan-month']").GetAttribute("value"));
        Assert.EndsWith("/plan/2026/10", Navigation.Uri);
    }

    // Spec 51, A10: am Posten die Buchungen, mit dem Betrag der Zeile und nicht der ganzen Buchung.
    [Fact]
    public void A_position_lists_the_bookings_that_belong_to_it()
    {
        var gez = FakePlanApi.Position("GEZ", 18.36m, booked: 23.36m);
        var first = new BookingResponse(Guid.NewGuid(), BookingKinds.Expense, Guid.NewGuid(), null, new DateOnly(2026, 10, 2), 18.36m, null,
            [new BookingLineResponse(Guid.NewGuid(), 18.36m, PotKeys.Needs, FakeCategoryApi.Groceries.Id, gez.Id)], 2026, 10);
        var fee = new BookingResponse(Guid.NewGuid(), BookingKinds.Expense, Guid.NewGuid(), null, new DateOnly(2026, 10, 9), 12m, null,
            [
                new BookingLineResponse(Guid.NewGuid(), 5m, PotKeys.Needs, FakeCategoryApi.Restaurant.Id, gez.Id),
                new BookingLineResponse(Guid.NewGuid(), 7m, PotKeys.Needs, FakeCategoryApi.Groceries.Id),
            ], 2026, 10);
        var other = new BookingResponse(Guid.NewGuid(), BookingKinds.Expense, Guid.NewGuid(), null, new DateOnly(2026, 10, 10), 3m, null,
            [new BookingLineResponse(Guid.NewGuid(), 3m, PotKeys.Needs, FakeCategoryApi.Groceries.Id)], 2026, 10);
        var cut = Open(2026, 10, Overview(bookings: [fee, first, other]), gez);

        // Spec 82, R15: Die Anzahl steht als Knopf in der Zeile; er klappt die Buchungen in voller Breite auf.
        var toggle = cut.Find($"[data-testid='position-bookings-{gez.Id}-toggle']");
        Assert.Contains("2 Buchungen", toggle.TextContent);
        Assert.Equal("false", toggle.GetAttribute("aria-expanded"));
        Assert.True(cut.Find($"[data-testid='position-bookings-{gez.Id}']").HasAttribute("hidden"));
        toggle.Click();
        Assert.Equal("true", cut.Find($"[data-testid='position-bookings-{gez.Id}-toggle']").GetAttribute("aria-expanded"));
        var list = cut.Find($"[data-testid='position-bookings-{gez.Id}']");
        Assert.False(list.HasAttribute("hidden"));
        Assert.Equal(list.Id, toggle.GetAttribute("aria-controls"));
        Assert.Null(list.Closest(".list-row"));
        Assert.Contains("18,36", list.QuerySelector($"[data-testid='position-booking-{first.Id}']")!.TextContent);
        var feeRow = list.QuerySelector($"[data-testid='position-booking-{fee.Id}']")!;
        Assert.Contains("5,00", feeRow.TextContent);
        Assert.Contains(FakeCategoryApi.Restaurant.Name, feeRow.TextContent);
        Assert.DoesNotContain("12,00", feeRow.TextContent);
        Assert.Null(list.QuerySelector($"[data-testid='position-booking-{other.Id}']"));
    }

    // Spec 51, A9: Der Weg zur nächsten Buchung auf diesem Posten öffnet den Dialog mit ihm.
    [Fact]
    public void A_position_opens_the_dialog_with_itself_chosen()
    {
        var dialogs = RenderDialogHost();
        Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true));
        var salary = FakePlanApi.Position("Gehalt", 2400m, PotKeys.Income);
        var cut = Open(2026, 10, Overview(), salary);

        cut.Find($"[data-testid='position-bookings-{salary.Id}-add']").Click();

        dialogs.WaitForAssertion(() => Assert.Equal(salary.Id.ToString(), BookingDialogSteps.Chosen(dialogs, "booking-line-position-0")));
        Assert.Equal("true", dialogs.Find("[data-testid='booking-kind-income']").GetAttribute("aria-pressed"));
    }

    // Spec 51, A11, A12: Jede Zeile sagt die Art als Wort und wofür; die Farbe ist nie allein.
    [Fact]
    public void Booking_lists_name_the_kind_as_a_word()
    {
        var giro = new AccountChoice(Guid.NewGuid(), "Giro", IsDefault: true, Active: true);
        var spar = new AccountChoice(Guid.NewGuid(), "Spar", IsDefault: false, Active: true);
        Accounts.Accounts.AddRange([giro, spar]);
        var lebensmittel = FakePlanApi.Position("Lebensmittel", 400m, booked: 50m);
        var expense = new BookingResponse(Guid.NewGuid(), BookingKinds.Expense, giro.Id, null, new DateOnly(2026, 10, 9), 50m, null,
            [new BookingLineResponse(Guid.NewGuid(), 50m, PotKeys.Needs, FakeCategoryApi.Groceries.Id, lebensmittel.Id)], 2026, 10);
        var income = new BookingResponse(Guid.NewGuid(), BookingKinds.Income, giro.Id, null, new DateOnly(2026, 10, 8), 2400m, null,
            [new BookingLineResponse(Guid.NewGuid(), 2400m, PotKeys.Income, FakeCategoryApi.Salary.Id)], 2026, 10);
        var transfer = new BookingResponse(Guid.NewGuid(), BookingKinds.Transfer, giro.Id, spar.Id, new DateOnly(2026, 10, 7), 100m, null, [], 2026, 10);
        var overview = Overview(bookings: [expense, income, transfer]) with
        {
            CarryOvers = [new PlanCarryOverLineResponse(CarryOverDirections.In, 2026, 9, 80m)],
        };

        var cut = Open(2026, 10, overview, lebensmittel);
        OpenBookingsTab(cut);

        cut.WaitForAssertion(() => Assert.Contains("Lebensmittel", cut.Find($"[data-testid='month-booking-{expense.Id}']").TextContent));
        var row = cut.Find($"[data-testid='month-booking-{expense.Id}']");
        Assert.Contains("Ausgabe", row.TextContent);
        Assert.Contains("−50,00", row.TextContent);
        Assert.Contains("Giro", row.TextContent);
        var plus = cut.Find($"[data-testid='month-booking-{income.Id}']");
        Assert.Contains("Einnahme", plus.TextContent);
        Assert.Contains(FakeCategoryApi.Salary.Name, plus.TextContent);
        // A17 (Spec 82, R15): Ausgaben rot mit Minus, Einnahmen ohne Vorzeichen; Spalten Datum, Name, Posten, Konto, Topf, Betrag.
        Assert.Contains("2.400,00", plus.TextContent);
        Assert.DoesNotContain("+", plus.TextContent);
        Assert.DoesNotContain("amount-expense", plus.QuerySelector(".bookings-amount")!.ClassName);
        Assert.StartsWith("−", row.QuerySelector(".bookings-amount")!.TextContent.Trim());
        Assert.Contains("amount-expense", row.QuerySelector(".bookings-amount")!.ClassName);
        Assert.Equal(["Datum", "Name", "Posten", "Konto", "Topf", "Betrag"], cut.FindAll(".bookings-table th").Select(th => th.TextContent.Trim()));
        Assert.Contains("Lebensmittel", row.QuerySelector(".bookings-position")!.TextContent);
        Assert.Contains("Ungeplant", plus.QuerySelector(".bookings-position")!.TextContent);
        var moved = cut.Find($"[data-testid='month-booking-{transfer.Id}']");
        Assert.Contains("Umbuchung", moved.TextContent);
        Assert.Contains("Giro → Spar", moved.TextContent);
        Assert.Contains("Übertrag", cut.Find("[data-testid='month-carry-over-in']").TextContent);
        Assert.Equal(
            [BookingKinds.Expense, BookingKinds.Income, BookingKinds.Transfer, "carryover"],
            new[] { row, plus, moved, cut.Find("[data-testid='month-carry-over-in']") }.Select(r => r.QuerySelector(".booking-kind")!.GetAttribute("data-kind")));
    }
}
