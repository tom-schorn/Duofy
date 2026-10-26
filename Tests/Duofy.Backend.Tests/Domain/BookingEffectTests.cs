using Duofy.Domain;

namespace Duofy.Domain.Tests;

/// <summary>Wie eine Buchung ein Konto verändert (R1): Der Betrag ist positiv, das Vorzeichen folgt aus dem Topf.</summary>
public class BookingEffectTests
{
    private static readonly Guid Giro = Guid.NewGuid();
    private static readonly Guid Cash = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    [Theory]
    [InlineData(Pot.Needs)]
    [InlineData(Pot.Wants)]
    [InlineData(Pot.Savings)]
    public void Expense_lowers_its_account(Pot pot)
    {
        var booking = Booking(Giro, pot: pot, amount: 42.10m);

        Assert.Equal(-42.10m, booking.EffectOn(Giro));
    }

    [Fact]
    public void Income_raises_its_account()
    {
        var booking = Booking(Giro, pot: Pot.Income, amount: 2450.00m);

        Assert.Equal(2450.00m, booking.EffectOn(Giro));
    }

    [Fact]
    public void Transfer_lowers_the_from_account_and_raises_the_to_account()
    {
        var booking = Booking(Giro, counter: Cash, amount: 100.00m);

        Assert.Equal(-100.00m, booking.EffectOn(Giro));
        Assert.Equal(100.00m, booking.EffectOn(Cash));
    }

    [Fact]
    public void Savings_transfer_has_the_same_effect_as_any_transfer()
    {
        var booking = Booking(Giro, counter: Cash, pot: Pot.Savings, amount: 50.00m);

        Assert.Equal(-50.00m, booking.EffectOn(Giro));
        Assert.Equal(50.00m, booking.EffectOn(Cash));
    }

    [Fact]
    public void Booking_does_not_touch_an_unrelated_account()
    {
        Assert.Equal(0m, Booking(Giro, pot: Pot.Needs, amount: 10m).EffectOn(Other));
        Assert.Equal(0m, Booking(Giro, counter: Cash, amount: 10m).EffectOn(Other));
    }

    [Fact]
    public void Expense_without_a_line_has_no_defined_effect()
    {
        // Eine solche Buchung darf es nach der Prüfung nie geben (R2); rechnet
        // trotzdem jemand damit, soll es laut scheitern und nicht still falsch rechnen.
        var booking = Booking(Giro, amount: 10m);

        Assert.Throws<InvalidOperationException>(() => booking.EffectOn(Giro));
    }

    [Fact]
    public void Split_booking_uses_its_total_not_a_single_line()
    {
        var booking = Booking(Giro, amount: 80.00m);
        booking.Lines.Add(new BookingLine { Amount = 60.00m, Pot = Pot.Needs });
        booking.Lines.Add(new BookingLine { Amount = 20.00m, Pot = Pot.Needs });

        Assert.Equal(-80.00m, booking.EffectOn(Giro));
    }

    private static Booking Booking(Guid account, Guid? counter = null, Pot? pot = null, decimal amount = 1m)
    {
        var booking = new Booking
        {
            AccountId = account,
            CounterAccountId = counter,
            PaymentDate = new DateOnly(2026, 10, 21),
            Amount = amount,
        };
        if (pot is { } line)
        {
            booking.Lines.Add(new BookingLine { Amount = amount, Pot = line });
        }
        return booking;
    }
}
