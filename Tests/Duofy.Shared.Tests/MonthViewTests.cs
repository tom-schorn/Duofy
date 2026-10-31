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
        var plan = PlanWith(Overview()) with { Quotas = [new PlanQuotaResponse(PotKeys.Needs, 50, 60m)] };

        var pot = MonthView.ForPerson(plan, Today, _ => null).Pots.Single(p => p.Pot == PotKeys.Needs);

        Assert.Equal(200m, pot.Gap);
        Assert.Equal(50, pot.Target);
        Assert.Equal(60m, pot.Actual);
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
