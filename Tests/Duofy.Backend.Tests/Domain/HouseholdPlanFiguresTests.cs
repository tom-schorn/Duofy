using Duofy.Domain;

namespace Duofy.Domain.Tests;

/// <summary>Die Zahlen des zusammengesetzten Haushaltsplans (Spec 69, R4): dieselben Regeln wie beim Plan einer Person.</summary>
public class HouseholdPlanFiguresTests
{
    private static readonly Guid Giro = Guid.NewGuid();

    // A3
    [Fact]
    public void Income_and_quota_actual_add_up_over_the_members_and_leave_pass_through_out()
    {
        var annasSalary = Position("Gehalt Anna", 3000.00m, Pot.Income);
        var rent = Position("Miete", 1500.00m, Pot.Needs);
        var forwarding = Position("Weiterleitung", 200.00m, Pot.Needs, passThrough: true);
        var bensSalary = Position("Gehalt Ben", 2000.00m, Pot.Income);
        var power = Position("Strom", 1000.00m, Pot.Needs);
        var anna = Month([annasSalary, rent, forwarding],
            Booked(3000.00m, Pot.Income, annasSalary.Id), Booked(1500.00m, Pot.Needs, rent.Id), Booked(200.00m, Pot.Needs, forwarding.Id));
        var ben = Month([bensSalary, power], Booked(2000.00m, Pot.Income, bensSalary.Id), Booked(1000.00m, Pot.Needs, power.Id));

        var figures = HouseholdPlanFigures.Compute([anna, ben]);

        Assert.Equal(5000.00m, figures.Income);
        Assert.Equal(50.0m, figures.QuotaActual[Pot.Needs]);
        Assert.Equal(0.0m, figures.QuotaActual[Pot.Wants]);
        Assert.Equal(new PotTotal(2500.00m, 2500.00m), figures.Pots[Pot.Needs]);
    }

    [Fact]
    public void The_carry_over_counts_as_no_income()
    {
        var salary = Position("Gehalt", 3000.00m, Pot.Income);
        var rent = Position("Miete", 1500.00m, Pot.Needs);
        var carryOver = Booked(500.00m, Pot.Income, null);
        var month = Month([salary, rent], Booked(3000.00m, Pot.Income, salary.Id), carryOver, Booked(1500.00m, Pot.Needs, rent.Id));
        month.Plan.CarryOverIncomeBookingId = carryOver.Id;

        var figures = HouseholdPlanFigures.Compute([month]);

        Assert.Equal(50.0m, figures.QuotaActual[Pot.Needs]);
    }

    // A13: Bei einem negativen Rest steht die Einnahme "Übertrag nach …" in diesem Monat; sie gehört zum
    // Übertrag des nächsten Monats und zählt weder bei den Einnahmen der Quoten noch als nicht zugeordnet.
    [Fact]
    public void The_income_of_a_negative_carry_over_into_the_next_month_counts_nowhere()
    {
        var salary = Position("Gehalt", 3000.00m, Pot.Income);
        var rent = Position("Miete", 1500.00m, Pot.Needs);
        var carryOutflow = Booked(500.00m, Pot.Income, null);
        var month = Month([salary, rent], [Booked(3000.00m, Pot.Income, salary.Id), carryOutflow, Booked(1500.00m, Pot.Needs, rent.Id)],
            [carryOutflow.Id]);

        var figures = HouseholdPlanFigures.Compute([month]);

        Assert.Equal(50.0m, figures.QuotaActual[Pot.Needs]);
        Assert.Equal(0m, figures.Unplanned[Pot.Income]);
    }

    [Fact]
    public void Without_income_every_actual_is_zero()
    {
        var rent = Position("Miete", 1500.00m, Pot.Needs);

        var figures = HouseholdPlanFigures.Compute([Month([rent], Booked(1500.00m, Pot.Needs, rent.Id))]);

        Assert.Equal(0m, figures.QuotaActual[Pot.Needs]);
    }

    [Fact]
    public void Unpaid_is_what_is_left_of_open_positions_and_never_negative()
    {
        var groceries = Position("Lebensmittel", 600.00m, Pot.Needs);
        var ticked = Position("Strom", 80.00m, Pot.Needs);
        ticked.DoneAt = DateTimeOffset.Parse("2026-11-03T10:00:00Z");
        var overspent = Position("Kino", 50.00m, Pot.Wants);
        var forwarding = Position("Weiterleitung", 200.00m, Pot.Needs, passThrough: true);
        var salary = Position("Gehalt", 3000.00m, Pot.Income);
        var month = Month([groceries, ticked, overspent, forwarding, salary],
            Booked(127.50m, Pot.Needs, groceries.Id), Booked(70.00m, Pot.Wants, overspent.Id));

        var figures = HouseholdPlanFigures.Compute([month]);

        // 600 - 127.50 offen; abgehakt, durchlaufend, überzogen und Einnahme zählen nichts.
        Assert.Equal(472.50m, figures.Unpaid);
    }

    // A4
    [Fact]
    public void Unplanned_sums_bookings_without_position_by_pot_and_skips_transfers_and_the_carry_over()
    {
        var carryOutflow = Booked(500.00m, Pot.Needs, null);
        var transfer = new Booking { Id = Guid.NewGuid(), AccountId = Giro, CounterAccountId = Guid.NewGuid(), PaymentDate = new DateOnly(2026, 11, 3), Amount = 100.00m };
        var month = Month(
            [],
            [Booked(40.00m, Pot.Wants, null), Booked(15.00m, Pot.Wants, null), Booked(25.00m, Pot.Income, null), carryOutflow, transfer],
            [carryOutflow.Id]);

        var figures = HouseholdPlanFigures.Compute([month]);

        Assert.Equal(55.00m, figures.Unplanned[Pot.Wants]);
        Assert.Equal(25.00m, figures.Unplanned[Pot.Income]);
        Assert.Equal(0m, figures.Unplanned[Pot.Needs]);
        Assert.Equal(0m, figures.Unplanned[Pot.Savings]);
    }

    [Fact]
    public void Planned_sums_leave_out_pass_through_positions()
    {
        var wants = Position("Kino", 50.00m, Pot.Wants);
        var forwarding = Position("Weiterleitung", 200.00m, Pot.Wants, passThrough: true);

        var figures = HouseholdPlanFigures.Compute([Month([wants, forwarding])]);

        Assert.Equal(50.00m, figures.Pots[Pot.Wants].Planned);
    }

    private static MemberMonth Month(List<PlanPosition> positions, params Booking[] bookings) =>
        Month(positions, bookings, []);

    private static MemberMonth Month(List<PlanPosition> positions, Booking[] bookings, Guid[] carryOverBookingIds)
    {
        var plan = Plan.CreateFrom([], 2026, 11);
        plan.OwnerId = Guid.NewGuid();
        plan.Positions.AddRange(positions);
        return new MemberMonth(plan, bookings, carryOverBookingIds);
    }

    private static PlanPosition Position(string label, decimal amount, Pot pot, bool passThrough = false) => new()
    {
        Id = Guid.NewGuid(),
        Label = label,
        AmountPlanned = amount,
        DueDay = 1,
        Type = pot == Pot.Income ? CommitmentType.Income : CommitmentType.Contract,
        Pot = pot,
        PassThrough = passThrough,
    };

    private static Booking Booked(decimal amount, Pot pot, Guid? positionId) => new()
    {
        Id = Guid.NewGuid(),
        AccountId = Giro,
        PaymentDate = new DateOnly(2026, 11, 5),
        Amount = amount,
        Lines = [new BookingLine { Amount = amount, Pot = pot, PositionId = positionId }],
    };
}
