using Duofy.Domain;

namespace Duofy.Domain.Tests;

/// <summary>
/// Die Zahlen eines Monats: gebucht je Posten (R4), Quoten-Ist je Topf (R8), Vorschlag für den
/// Übertrag (R9) und welche Planmonate eine Buchung haben darf (R7).
/// </summary>
public class PlanFiguresTests
{
    private static readonly Guid Giro = Guid.NewGuid();

    [Fact]
    public void Booked_is_the_sum_of_the_lines_assigned_to_a_position()
    {
        var groceries = Position("Lebensmittel", 600.00m, Pot.Needs);
        var bookings = new[]
        {
            Expense(250.00m, Pot.Needs, groceries.Id),
            Expense(300.00m, Pot.Needs, groceries.Id),
            Expense(20.00m, Pot.Needs, null),
        };

        var booked = PlanFigures.BookedPerPosition(bookings);

        Assert.Equal(550.00m, booked[groceries.Id]);
    }

    [Fact]
    public void Quota_actual_ignores_pass_through_positions_and_the_carry_over()
    {
        // A13: Einnahmen 3000, Grundbedarf gebucht 1500, dazu ein durchlaufender Posten über 200.
        var plan = Plan.CreateFrom([], 2026, 10);
        var rent = Position("Miete", 1500.00m, Pot.Needs);
        var passThrough = Position("Weiterleitung", 200.00m, Pot.Needs);
        passThrough.PassThrough = true;
        plan.Positions.AddRange([rent, passThrough]);
        var carryOver = Income(500.00m, null);
        var bookings = new[]
        {
            Income(3000.00m, null),
            carryOver,
            Expense(1500.00m, Pot.Needs, rent.Id),
            Expense(200.00m, Pot.Needs, passThrough.Id),
        };

        var actual = PlanFigures.QuotaActual(plan, bookings, carryOver.Id);

        Assert.Equal(50.0m, actual[Pot.Needs]);
        Assert.Equal(0.0m, actual[Pot.Wants]);
    }

    // A25
    [Fact]
    public void Quota_actual_rounds_half_away_from_zero()
    {
        // 49 von 400 sind genau 12,25 Prozent; kaufmännisch gerundet 12,3, nicht 12,2.
        var plan = Plan.CreateFrom([], 2026, 10);
        var groceries = Position("Lebensmittel", 49.00m, Pot.Needs);
        plan.Positions.Add(groceries);
        var bookings = new[] { Income(400.00m, null), Expense(49.00m, Pot.Needs, groceries.Id) };

        var actual = PlanFigures.QuotaActual(plan, bookings, null);

        Assert.Equal(12.3m, actual[Pot.Needs]);
    }

    [Fact]
    public void Without_income_the_quota_actual_is_zero()
    {
        var plan = Plan.CreateFrom([], 2026, 10);

        var actual = PlanFigures.QuotaActual(plan, [], null);

        Assert.Equal(0m, actual[Pot.Needs]);
    }

    [Fact]
    public void Carry_over_suggestion_is_income_minus_expenses_without_transfers()
    {
        var bookings = new[]
        {
            Income(3000.00m, null),
            Expense(2770.00m, Pot.Needs, null),
            new Booking { AccountId = Giro, CounterAccountId = Guid.NewGuid(), PaymentDate = new DateOnly(2026, 10, 3), Amount = 100.00m },
        };

        Assert.Equal(230.00m, PlanFigures.CarryOverSuggestion(bookings));
    }

    [Theory]
    [InlineData(2026, 8, false)]
    [InlineData(2026, 9, true)]
    [InlineData(2026, 10, true)]
    [InlineData(2026, 11, true)]
    [InlineData(2026, 12, false)]
    public void A_booking_may_count_in_its_month_or_the_one_before_or_after(int year, int month, bool allowed)
    {
        Assert.Equal(allowed, Booking.IsPlanMonthAllowed(new DateOnly(2026, 10, 25), year, month));
    }

    [Fact]
    public void Plan_month_may_cross_the_turn_of_the_year()
    {
        Assert.True(Booking.IsPlanMonthAllowed(new DateOnly(2026, 12, 28), 2027, 1));
        Assert.True(Booking.IsPlanMonthAllowed(new DateOnly(2027, 1, 2), 2026, 12));
    }

    private static PlanPosition Position(string label, decimal amount, Pot pot) => new()
    {
        Id = Guid.NewGuid(),
        Label = label,
        AmountPlanned = amount,
        DueDay = 1,
        Type = pot == Pot.Income ? CommitmentType.Income : CommitmentType.Contract,
        Pot = pot,
    };

    private static Booking Expense(decimal amount, Pot pot, Guid? positionId) => new()
    {
        Id = Guid.NewGuid(),
        AccountId = Giro,
        PaymentDate = new DateOnly(2026, 10, 5),
        Amount = amount,
        Lines = [new BookingLine { Amount = amount, Pot = pot, PositionId = positionId }],
    };

    private static Booking Income(decimal amount, Guid? positionId) => Expense(amount, Pot.Income, positionId);
}
