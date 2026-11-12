using Duofy.Schemas;
using Duofy.Shared.Services;

namespace Duofy.Shared.Tests;

/// <summary>
/// Ein verschobener Posten (Spec 136) zählt in seinem Monat auch in den Zahlen der App nirgends: Anstehend, Töpfe, Kategorien,
/// Fälligkeiten, Kurve, Haushalt und Buchungsvorschläge. Je Stelle ein Test. "Heute" ist der 10. November 2026.
/// </summary>
public class PostponeFiguresTests
{
    private static readonly DateOnly Today = new(2026, 11, 10);
    private static readonly PlanMonthRef December = new(2026, 12);

    private static PlanPositionResponse Moved(string label, decimal planned, string pot = PotKeys.Wants) =>
        FakePlanApi.Position(label, planned, pot, day: 20) with { MovedToMonth = December };

    private static PlanResponse PlanWith(params PlanPositionResponse[] positions) =>
        FakePlanApi.Empty(2026, 11) with { Positions = positions, Overview = new PlanOverviewResponse(0m, 0m, 0m, [], []) };

    [Fact]
    public void Unpaid_leaves_a_moved_position_out()
    {
        Assert.Equal(10m, MonthView.Unpaid([FakePlanApi.Position("Strom", 10m), Moved("GitHub", 4m)]));
    }

    [Fact]
    public void The_planned_amount_of_a_pot_without_overview_leaves_a_moved_position_out()
    {
        var plan = PlanWith(FakePlanApi.Position("Streaming", 12m, PotKeys.Wants), Moved("GitHub", 4m)) with { Overview = null };

        Assert.Equal(12m, PlanView.PotPlanned(plan, PotKeys.Wants));
    }

    [Fact]
    public void The_category_rows_leave_a_moved_position_out()
    {
        var rows = MonthView.Categories(PlanWith(FakePlanApi.Position("Strom", 10m), Moved("GitHub", 4m)), _ => "Haushalt");

        Assert.Equal(10m, Assert.Single(rows).Planned);
    }

    [Fact]
    public void The_due_list_and_the_pot_rows_of_a_person_leave_a_moved_position_out()
    {
        var model = MonthView.ForPerson(PlanWith(FakePlanApi.Position("Strom", 10m, day: 15), Moved("GitHub", 4m)), Today, _ => null);

        Assert.Equal("Strom", Assert.Single(model.Due).Label);
    }

    [Fact]
    public void The_curve_of_a_person_leaves_a_moved_position_out()
    {
        var withMoved = MonthCurve.ForPerson(PlanWith(Moved("GitHub", 400m)), Today);
        var without = MonthCurve.ForPerson(PlanWith(), Today);

        Assert.Equal(without.Lowest.Balance, withMoved.Lowest.Balance);
    }

    [Fact]
    public void The_household_due_list_and_curve_leave_a_moved_position_out()
    {
        var anna = Guid.NewGuid();
        var moved = FakeHouseholdApi.OwnedPosition("GitHub", 400m, anna, "Anna", PotKeys.Wants) with { MovedToMonth = December };
        var plan = FakeHouseholdApi.EmptyPlan(Guid.NewGuid(), "Haus", 2026, 11) with { Positions = [moved] };

        var model = MonthView.ForHousehold(plan, Today);

        Assert.Empty(model.Due);
        Assert.Equal(MonthCurve.ForHousehold(plan with { Positions = [] }, Today).Lowest.Balance, model.Curve!.Lowest.Balance);
    }

    [Fact]
    public void A_moved_position_is_not_offered_for_a_booking_line()
    {
        var plan = PlanWith(FakePlanApi.Position("Strom", 10m), Moved("GitHub", 4m));

        Assert.Equal(["Strom"], PlanView.OfferedPositions(plan, BookingKinds.Expense).Select(p => p.Label));
    }
}
