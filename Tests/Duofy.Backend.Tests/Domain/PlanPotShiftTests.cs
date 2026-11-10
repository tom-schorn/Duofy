using Duofy.Domain;

namespace Duofy.Domain.Tests;

/// <summary>Eine Umbuchung zwischen zwei Töpfen verschiebt ihren Betrag im gebuchten Topf und im Ist der Quote (Spec 112).</summary>
public class PlanPotShiftTests
{
    private static readonly Guid Giro = Guid.NewGuid();
    private static readonly Guid Sparkonto = Guid.NewGuid();

    // A1
    [Fact]
    public void A_transfer_from_needs_to_savings_lowers_booked_needs_and_raises_booked_savings()
    {
        var shift = PlanFigures.PotShift([Transfer(200.00m, Pot.Needs, Pot.Savings)]);
        var totals = PlanFigures.PotTotals([], new Dictionary<Guid, decimal>(), new Dictionary<Pot, decimal>(), shift);

        Assert.Equal(-200.00m, totals[Pot.Needs].Booked);
        Assert.Equal(200.00m, totals[Pot.Savings].Booked);
        Assert.Equal(0m, totals[Pot.Wants].Booked);
    }

    // A2
    [Fact]
    public void The_same_transfer_moves_the_quota_actual()
    {
        var plan = Plan.CreateFrom([], 2026, 11);
        var salary = Income(1000.00m);
        var withTransfer = PlanFigures.QuotaActual(plan, [salary, Transfer(200.00m, Pot.Needs, Pot.Savings)], null);
        var without = PlanFigures.QuotaActual(plan, [salary], null);

        Assert.Equal(without[Pot.Needs] - 20.0m, withTransfer[Pot.Needs]);
        Assert.Equal(without[Pot.Savings] + 20.0m, withTransfer[Pot.Savings]);
        Assert.Equal(without[Pot.Wants], withTransfer[Pot.Wants]);
    }

    // A3
    [Fact]
    public void A_transfer_within_one_pot_and_an_older_transfer_without_pots_change_nothing()
    {
        var plan = Plan.CreateFrom([], 2026, 11);
        var older = Transfer(300.00m, Pot.Savings, null);
        older.Lines.Clear();
        var bookings = new[] { Income(1000.00m), Transfer(100.00m, Pot.Savings, Pot.Savings), older, Transfer(50.00m, Pot.Savings, null) };

        var shift = PlanFigures.PotShift(bookings);

        Assert.All(shift.Values, value => Assert.Equal(0m, value));
        Assert.Equal(PlanFigures.QuotaActual(plan, [Income(1000.00m)], null), PlanFigures.QuotaActual(plan, bookings, null));
    }

    // A4: Betrag, Monat und Löschen wirken, weil der Monat nur seine eigenen Buchungen kennt und neu rechnet.
    [Fact]
    public void The_shift_follows_the_amount_and_only_the_bookings_of_the_month()
    {
        Assert.Equal(-50.00m, PlanFigures.PotShift([Transfer(50.00m, Pot.Needs, Pot.Savings)])[Pot.Needs]);
        Assert.Equal(50.00m, PlanFigures.PotShift([Transfer(50.00m, Pot.Needs, Pot.Savings)])[Pot.Savings]);
        Assert.All(PlanFigures.PotShift([]).Values, value => Assert.Equal(0m, value));
    }

    [Fact]
    public void A_shift_may_leave_the_source_pot_negative()
    {
        var shift = PlanFigures.PotShift([Transfer(80.00m, Pot.Wants, Pot.Needs), Transfer(20.00m, Pot.Wants, Pot.Savings)]);

        Assert.Equal(-100.00m, shift[Pot.Wants]);
        Assert.Equal(80.00m, shift[Pot.Needs]);
    }

    // A5
    [Fact]
    public void The_household_adds_the_shifts_of_its_members()
    {
        var anna = Month(Transfer(100.00m, Pot.Wants, Pot.Savings));
        var ben = Month(Transfer(100.00m, Pot.Wants, Pot.Savings));
        var base1 = HouseholdPlanFigures.Compute([Month(), Month()]);

        var figures = HouseholdPlanFigures.Compute([anna, ben]);

        Assert.Equal(base1.Pots[Pot.Wants].Booked - 200.00m, figures.Pots[Pot.Wants].Booked);
        Assert.Equal(base1.Pots[Pot.Savings].Booked + 200.00m, figures.Pots[Pot.Savings].Booked);
    }

    // A6: Frei-Betrag und Fest hängen nicht an der Verschiebung.
    [Fact]
    public void Free_and_fixed_do_not_depend_on_the_shift()
    {
        var rent = new PlanPosition { Id = Guid.NewGuid(), Label = "Miete", AmountPlanned = 500m, DueDay = 1, Pot = Pot.Needs, Type = CommitmentType.Contract };
        var unplanned = PlanFigures.Unplanned([Income(10.00m), Transfer(200.00m, Pot.Needs, Pot.Savings)], []);

        Assert.Equal(10.00m, unplanned[Pot.Income]);
        Assert.Equal(0m, unplanned[Pot.Needs]);
        Assert.Equal(0m, unplanned[Pot.Savings]);
        Assert.Equal(-490.00m, PlanFigures.Free([rent], new Dictionary<Guid, decimal>(), 0m, unplanned, 0m));
        Assert.Equal(500m, PlanFigures.Fixed([rent]));
    }

    private static MemberMonth Month(params Booking[] bookings)
    {
        var plan = Plan.CreateFrom([], 2026, 11);
        plan.OwnerId = Guid.NewGuid();
        return new MemberMonth(plan, bookings, []);
    }

    private static Booking Income(decimal amount) => new()
    {
        Id = Guid.NewGuid(), AccountId = Giro, PaymentDate = new DateOnly(2026, 11, 5), Amount = amount,
        Lines = [new BookingLine { Amount = amount, Pot = Pot.Income }],
    };

    private static Booking Transfer(decimal amount, Pot source, Pot? target) => new()
    {
        Id = Guid.NewGuid(), AccountId = Giro, CounterAccountId = Sparkonto, PaymentDate = new DateOnly(2026, 11, 5), Amount = amount,
        TargetPot = target,
        Lines = [new BookingLine { Amount = amount, Pot = source }],
    };
}
