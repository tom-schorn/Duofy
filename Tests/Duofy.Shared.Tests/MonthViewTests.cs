using Duofy.Schemas;
using Duofy.Shared.Services;

namespace Duofy.Shared.Tests;

/// <summary>
/// Die Regeln hinter der Monatsübersicht (Spec 24), ohne Oberfläche: die fünf neuesten Buchungen (A10), anstehend und
/// überfällig (A11), der Hinweis auf ein Datum in einem anderen Monat (A9) und die Lücke je Topf (A4).
/// "Heute" ist der 21. Oktober 2026.
/// </summary>
public class MonthViewTests
{
    private static readonly DateOnly Today = new(2026, 10, 21);

    private static BookingResponse Booking(DateOnly date, decimal amount, int planYear = 2026, int planMonth = 10) =>
        new(Guid.NewGuid(), BookingKinds.Expense, Guid.NewGuid(), null, date, amount, null, [], planYear, planMonth);

    private static PlanResponse PlanWith(PlanOverviewResponse? overview, params PlanPositionResponse[] positions) =>
        FakePlanApi.Empty(2026, 10) with { Positions = positions, Overview = overview };

    private static PlanOverviewResponse Overview(params BookingResponse[] bookings) =>
        new(3000m, 1200m, 1800m, [new PlanPotResponse(PotKeys.Needs, 1000m, 1200m)], bookings);

    // A10 (R10)
    [Fact]
    public void The_overview_lists_the_five_newest_bookings_with_date_and_amount()
    {
        var bookings = Enumerable.Range(1, 7).Select(day => Booking(new DateOnly(2026, 10, day), day * 10m)).Reverse().ToArray();

        var model = MonthView.ForPerson(PlanWith(Overview(bookings)), Today, _ => null);

        Assert.Equal(5, model.Bookings.Count);
        Assert.Equal([7, 6, 5, 4, 3], model.Bookings.Select(b => b.Date.Day));
        Assert.Equal(70m, model.Bookings[0].Amount);
    }

    // Spec 66, R9: Die Zeilen des Übertrags nehmen keiner Buchung den Platz unter den fünf neuesten.
    [Fact]
    public void The_carry_overs_come_as_their_own_rows_beside_the_five_newest_bookings()
    {
        var bookings = Enumerable.Range(1, 7).Select(day => Booking(new DateOnly(2026, 10, day), day * 10m)).Reverse().ToArray();
        var overview = Overview(bookings) with
        {
            CarryOvers =
            [
                new PlanCarryOverLineResponse(CarryOverDirections.In, 2026, 9, 230m),
                new PlanCarryOverLineResponse(CarryOverDirections.Out, 2026, 11, -150m),
            ],
        };

        var model = MonthView.ForPerson(PlanWith(overview), Today, _ => null);

        Assert.Equal(5, model.Bookings.Count);
        Assert.Equal(
            [new MonthCarryOverRow(true, new DateOnly(2026, 9, 1), 230m, null), new MonthCarryOverRow(false, new DateOnly(2026, 11, 1), -150m, null)],
            model.CarryOvers);
    }

    // A9 (R8)
    [Fact]
    public void A_booking_dated_in_another_month_than_its_plan_month_is_marked()
    {
        var december = Booking(new DateOnly(2026, 11, 20), 45m, 2026, 12);
        var plain = Booking(new DateOnly(2026, 12, 2), 5m, 2026, 12);

        var rows = MonthView.BookingRows([december, plain], _ => "Girokonto");

        Assert.True(rows.Single(r => r.Id == december.Id).OtherMonth);
        Assert.False(rows.Single(r => r.Id == plain.Id).OtherMonth);
        Assert.Equal("Girokonto", rows[0].Detail);
    }

    // A11 (R11)
    [Fact]
    public void Due_in_two_days_and_overdue_since_three_days_are_both_listed_and_only_the_overdue_one_is_marked()
    {
        var soon = FakePlanApi.Position("Strom", 80m, day: 23);
        var late = FakePlanApi.Position("Miete", 890m, day: 18);
        var far = FakePlanApi.Position("Versicherung", 50m, day: 30);
        var done = FakePlanApi.Position("Internet", 40m, day: 19, done: true);
        var salary = FakePlanApi.Position("Gehalt", 2400m, PotKeys.Income, day: 20);

        var due = MonthView.ForPerson(PlanWith(Overview(), soon, late, far, done, salary), Today, _ => null).Due;

        Assert.Equal(["Miete", "Strom"], due.Select(d => d.Label));
        Assert.True(due[0].Overdue);
        Assert.False(due[1].Overdue);
        Assert.Equal(new DateOnly(2026, 10, 18), due[0].Due);
    }

    [Fact]
    public void A_partly_booked_position_is_due_with_the_open_rest_and_a_fully_booked_one_is_not_due()
    {
        var partly = FakePlanApi.Position("Lebensmittel", 600m, booked: 250m, day: 22);
        var paid = FakePlanApi.Position("Telefon", 30m, booked: 30m, day: 22);

        var due = MonthView.ForPerson(PlanWith(Overview(), partly, paid), Today, _ => null).Due;

        var row = Assert.Single(due);
        Assert.Equal(350m, row.Open);
    }

    // A4 (R4)
    [Fact]
    public void The_gap_is_booked_minus_planned_per_pot_and_the_quota_comes_from_the_plan()
    {
        var plan = PlanWith(Overview()) with { Quotas = [new PlanQuotaResponse(PotKeys.Needs, 50, 60m)], BookedIncome = 2000m };

        var pot = MonthView.ForPerson(plan, Today, _ => null).Pots.Single(p => p.Pot == PotKeys.Needs);

        Assert.Equal(200m, pot.Gap);
        Assert.Equal(50, pot.Target);
        Assert.Equal(60m, pot.Actual);
        Assert.False(pot.OnPlannedIncome);
    }

    // A20 (Spec 82, R17): Solange keine Einnahme gebucht ist, bezieht sich Ist auf die geplanten Einnahmen statt überall 0 %.
    [Fact]
    public void Without_booked_income_the_actual_refers_to_the_planned_income()
    {
        var rent = FakePlanApi.Position("Miete", 900m, booked: 900m);
        var food = FakePlanApi.Position("Essen", 400m, booked: 300m);
        var through = FakePlanApi.Position("Auslage", 100m, booked: 100m) with { PassThrough = true };
        var fun = FakePlanApi.Position("Kino", 50m, PotKeys.Wants, booked: 30m);
        var plan = PlanWith(Overview(), rent, food, through, fun);

        var pots = MonthView.ForPerson(plan, Today, _ => null).Pots;

        // Geplante Einnahmen 3000 (Übersicht); Grundbedarf 900 + 300 gebucht, die Auslage zählt nicht: 40 %. Wünsche 30: 1 %.
        Assert.Equal(40m, pots.Single(p => p.Pot == PotKeys.Needs).Actual);
        Assert.Equal(1m, pots.Single(p => p.Pot == PotKeys.Wants).Actual);
        Assert.All(pots, p => Assert.True(p.OnPlannedIncome));
    }

    // Spec 112, R2: Die Umbuchung zwischen Töpfen steckt schon im gebuchten Betrag des Topfs, den die API liefert; das Ist ohne gebuchte Einnahme nimmt diesen Betrag.
    [Fact]
    public void Without_booked_income_the_actual_includes_the_pot_shift_of_transfers()
    {
        var rent = FakePlanApi.Position("Miete", 900m, booked: 900m);
        var overview = new PlanOverviewResponse(3000m, 900m, 2100m,
            [new PlanPotResponse(PotKeys.Needs, 900m, 700m), new PlanPotResponse(PotKeys.Savings, 0m, 200m)], []);
        var plan = PlanWith(overview, rent);

        var pots = MonthView.ForPerson(plan, Today, _ => null).Pots;

        // Die Miete 900 allein gäbe 30 %; mit der Verschiebung von 200 an das Sparen: Grundbedarf 700 = 23,3 %, Sparen 200 = 6,7 %.
        Assert.Equal(23.3m, pots.Single(p => p.Pot == PotKeys.Needs).Actual);
        Assert.Equal(6.7m, pots.Single(p => p.Pot == PotKeys.Savings).Actual);
    }

    [Fact]
    public void Without_planned_income_the_actual_stays_the_one_of_the_api()
    {
        var (actual, onPlanned) = MonthView.QuotaActual(PotKeys.Needs, 0m, incomeBooked: false, plannedIncome: 0m,
            [new MonthView.QuotaSource(PotKeys.Needs, false, 100m)]);

        Assert.Equal(0m, actual);
        Assert.False(onPlanned);
    }

    [Fact]
    public void Without_overview_data_the_model_is_empty_instead_of_failing()
    {
        var model = MonthView.ForPerson(PlanWith(null), Today, _ => null);

        Assert.Equal(0m, model.Free);
        Assert.Empty(model.Bookings);
        Assert.All(model.Pots, pot => Assert.Equal(0m, pot.Gap));
    }
}
