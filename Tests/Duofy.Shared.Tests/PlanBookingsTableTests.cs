using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Duofy.Shared.Pages.Month;
using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 123: Die Buchungstabelle des persönlichen Plans und des Haushaltsplans ist eine Komponente (MonthBookings), sie
/// scrollt waagerecht in ihrem eigenen Bereich, wo sie nicht passt, und die Buchungen stehen strikt neueste zuerst,
/// der eingehende Übertrag als am 1. des Planmonats gebucht am Ende, der ausgehende mit seinem Datum oben.
/// </summary>
public class PlanBookingsTableTests : PageTestContext
{
    private static string Css => File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "duofy.css")).Replace("\r\n", "\n");

    private static MonthBookingRow Row(int day, string name) =>
        new(Guid.NewGuid(), new DateOnly(2026, 10, day), 10m, "Girokonto", false, BookingKinds.Expense, null, name, [], [PotKeys.Needs]);

    private static IReadOnlyList<string> Order(IRenderedComponent<MonthBookings> cut) =>
        cut.FindAll("tbody tr").Select(tr => tr.QuerySelector(".bookings-title")!.TextContent.Trim()).ToList();

    // A2 (R2): 10., 5. und der Übertrag aus dem Vormonat; die Zeilen kommen absichtlich in falscher Reihenfolge an.
    [Fact]
    public void A2_the_list_runs_newest_first_and_the_carry_over_stands_at_the_end()
    {
        var cut = Render<MonthBookings>(p => p
            .Add(x => x.Rows, [Row(5, "Fünfter"), Row(10, "Zehnter")])
            .Add(x => x.CarryOvers, [new MonthCarryOverRow(true, new DateOnly(2026, 9, 1), 230m, null)]));

        Assert.Equal(["Zehnter", "Fünfter", "Übertrag aus September"], Order(cut));
    }

    // A2 (R2): auch eine Buchung am 1. steht vor dem eingehenden Übertrag, der als am 1. des Planmonats gebucht gilt.
    [Fact]
    public void A2_a_booking_on_the_first_stands_above_the_incoming_carry_over_which_carries_the_first_of_the_plan_month()
    {
        var cut = Render<MonthBookings>(p => p
            .Add(x => x.Rows, [Row(1, "Erster"), Row(3, "Dritter")])
            .Add(x => x.CarryOvers, [new MonthCarryOverRow(true, new DateOnly(2026, 9, 1), 230m, null)]));

        Assert.Equal(["Dritter", "Erster", "Übertrag aus September"], Order(cut));
        Assert.Equal("01.10.2026", cut.Find("tr[data-testid='month-carry-over-in'] .bookings-date").TextContent.Trim());
    }

    // A2 (R2, Ergänzung 2026-11-11): der ausgehende Übertrag behält sein Buchungsdatum, den letzten Tag des Planmonats, und steht oben.
    [Fact]
    public void A2_the_outgoing_carry_over_keeps_the_end_of_the_month_and_stands_on_top()
    {
        var cut = Render<MonthBookings>(p => p
            .Add(x => x.Rows, [Row(1, "Erster"), Row(30, "Dreißigster")])
            .Add(x => x.CarryOvers,
            [
                new MonthCarryOverRow(true, new DateOnly(2026, 9, 1), 230m, null),
                new MonthCarryOverRow(false, new DateOnly(2026, 11, 1), 40m, null),
            ]));

        Assert.Equal(["Übertrag nach November", "Dreißigster", "Erster", "Übertrag aus September"], Order(cut));
        Assert.Equal("31.10.2026", cut.Find("tr[data-testid='month-carry-over-out'] .bookings-date").TextContent.Trim());
    }

    // A2 (R2), Haushaltsplan: dieselbe Reihenfolge, auch wenn die Buchungen ungeordnet ankommen.
    [Fact]
    public void A2_the_household_plan_lists_in_the_same_order()
    {
        RenderDialogHost();
        var household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20, [FakeHouseholdApi.Anna(isMe: false), FakeHouseholdApi.Ben(isMe: true)]);
        HouseholdApiFake.Household = household;
        var empty = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10);
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = empty with
        {
            Bookings = [HouseholdBooking(5, "Fünfter"), HouseholdBooking(10, "Zehnter")],
            CarryOvers = [new PlanCarryOverLineResponse(CarryOverDirections.In, 2026, 9, 100m, "Ben")],
        };

        var cut = Render<HouseholdPlanView>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-tab-bookings']")));
        cut.Find("[data-testid='month-tab-bookings']").Click();

        var titles = cut.FindAll("[data-testid='month-bookings'] tbody tr").Select(tr => tr.QuerySelector(".bookings-title")!.TextContent.Trim()).ToList();
        Assert.Equal(["Zehnter", "Fünfter", "Übertrag aus September"], titles);
    }

    private static HouseholdBookingResponse HouseholdBooking(int day, string purpose) =>
        new(Guid.NewGuid(), new DateOnly(2026, 10, day), 10m, 2026, 10, Guid.NewGuid(), "Ben", BookingKinds.Expense, purpose, []);

    // A3 (R3): beide Pläne zeichnen ihre Buchungen mit derselben Komponente.
    [Fact]
    public void A3_the_personal_and_the_household_plan_use_the_same_bookings_component()
    {
        var plan = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "Pages", "Plan.razor"));
        var household = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "Pages", "HouseholdPlanView.razor"));

        Assert.Contains("<MonthBookings ", plan);
        Assert.Contains("<MonthBookings ", household);
        Assert.DoesNotContain("<table", plan);
        Assert.DoesNotContain("<table", household);
    }

    // A1 (R1): Die Tabelle sitzt in einem eigenen Bereich, der waagerecht scrollt; der Bereich ist per Tastatur erreichbar und benannt.
    [Fact]
    public void A1_the_table_sits_in_its_own_region_that_scrolls_sideways()
    {
        var rows = Enumerable.Range(1, 10).Select(d => Row(d, $"Buchung {d}")).ToList();

        var cut = Render<MonthBookings>(p => p.Add(x => x.Rows, rows));

        var scroll = cut.Find(".bookings-scroll");
        Assert.NotNull(scroll.QuerySelector("table.bookings-table"));
        Assert.Equal("region", scroll.GetAttribute("role"));
        Assert.Equal("0", scroll.GetAttribute("tabindex"));
        Assert.False(string.IsNullOrWhiteSpace(scroll.GetAttribute("aria-label")));
        Assert.Matches(@"\.bookings-scroll\s*\{[^}]*overflow-x:\s*auto", Css);
        Assert.Matches(@"\.bookings-scroll\s*\{[^}]*max-width:\s*100%", Css);
    }

    // A1 (R1): Der Karten-Rahmen schneidet nicht mehr ab, sonst gäbe es nichts zu wischen.
    [Fact]
    public void A1_the_card_frame_no_longer_clips_the_table()
    {
        Assert.DoesNotMatch(@"\.month-bookings\s*\{[^}]*overflow:\s*hidden", Css);
        Assert.Matches(@"\.month-bookings\s*\{[^}]*min-width:\s*0", Css);
    }

    // A1 (R1, Review F2): Wie die Tabelle sich zeigt, folgt der Breite ihres Bereichs und nicht der des Fensters; bei 840 px lässt die
    // volle Seitenleiste weniger Platz als bei 800 px. Der Bereich ist der Container, enger Platz macht das Polster enger und
    // sehr enger Platz aus jeder Zeile eine Karte, der Betrag bleibt so ohne Wischen sichtbar.
    [Fact]
    public void A1_the_table_layout_follows_the_width_of_its_own_area_and_not_the_viewport()
    {
        Assert.Matches(@"\.bookings-scroll\s*\{[^}]*container-type:\s*inline-size", Css);
        Assert.Matches(@"@container \(min-width: 720px\) and \(max-width: 779px\)\s*\{\s*\.bookings-table th, \.bookings-table td\s*\{\s*padding-left: 12px", Css);
        Assert.Matches(@"@container \(max-width: 719px\)\s*\{\s*\.bookings-table thead", Css);
        Assert.DoesNotMatch(@"@media[^{]*\{\s*\.bookings-table", Css);
    }

    // A1 (R1, Review F2): Wo die Tabelle trotzdem überläuft, zeigt ein Schatten am rechten Rand, dass dort noch etwas verborgen ist.
    [Fact]
    public void A1_an_overflowing_table_shows_a_shadow_at_the_right_edge()
    {
        Assert.Matches(@"\.bookings-scroll\s*\{[^}]*local right center[^}]*scroll right center", Css);
    }

    // A1 (R1, Review 3): In der Kartenansicht zeigt ein Übertrag keine Striche für Posten und Topf, damit er wie eine Buchungskarte aussieht.
    [Fact]
    public void A1_a_carry_over_card_hides_the_dashes_of_position_and_pot()
    {
        Assert.Matches(@"@container \(max-width: 719px\)[^@]*\.bookings-row--carry \.bookings-position, \.bookings-table \.bookings-row--carry \.bookings-pot\s*\{\s*display:\s*none", Css);
    }

    // A1 (R1, Review 4): Auf der Übertragskarte erzwingt ein leeres Element mit voller Breite den Umbruch, unabhängig von der Länge des
    // Vornamens ("Jo", "Li", "Ida"); Person und Betrag stehen dahinter (Order 2) gemeinsam in der zweiten Zeile.
    // Die gemessene Prüfung dazu: Tests/responsive-widths/carry-card.mjs.
    [Fact]
    public void A1_a_carry_over_card_forces_the_break_so_owner_and_amount_share_line_two_for_every_name_length()
    {
        Assert.Matches(@"@container \(max-width: 719px\)[^@]*\.bookings-row--carry::after\s*\{[^}]*content:\s*""""[^}]*order:\s*1[^}]*flex:\s*0 0 100%", Css);
        Assert.Matches(@"@container \(max-width: 719px\)[^@]*\.bookings-row--carry \.bookings-account, \.bookings-table \.bookings-row--carry \.bookings-amount\s*\{\s*order:\s*2; margin-top:\s*-2px", Css);
        Assert.DoesNotMatch(@"\.bookings-row--carry \.bookings-name\s*\{[^}]*calc\(", Css);
    }
}
