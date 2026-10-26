using Duofy.Domain;

namespace Duofy.Domain.Tests;

/// <summary>
/// Der Kontostand (Spec 58, R3): Startbetrag plus alle Buchungen nach dem Starttag. Die Beträge
/// der Buchungen sind positiv; die Richtung kommt aus <see cref="Booking.EffectOn"/>.
/// </summary>
public class AccountBalanceTests
{
    // Lena fängt am 1. Oktober 2026 mit 1.240,50 Euro auf dem Girokonto an.
    private static readonly DateOnly Opening = new(2026, 10, 1);

    [Fact]
    public void Account_without_bookings_has_its_opening_balance()
    {
        var checking = CheckingAccount();

        Assert.Equal(1240.50m, checking.Balance(new DateOnly(2026, 10, 15)));
    }

    [Fact]
    public void Expense_lowers_the_balance()
    {
        var checking = CheckingAccount();
        checking.Bookings.Add(Expense(checking, new DateOnly(2026, 10, 3), 54.20m));

        Assert.Equal(1186.30m, checking.Balance(new DateOnly(2026, 10, 15)));
    }

    [Fact]
    public void Income_raises_the_balance()
    {
        var checking = CheckingAccount();
        checking.Bookings.Add(Income(checking, new DateOnly(2026, 10, 28), 2400.00m));

        Assert.Equal(3640.50m, checking.Balance(new DateOnly(2026, 10, 31)));
    }

    [Fact]
    public void Balance_on_a_day_ignores_bookings_after_that_day()
    {
        var checking = CheckingAccount();
        checking.Bookings.Add(Expense(checking, new DateOnly(2026, 10, 3), 54.20m));
        checking.Bookings.Add(Income(checking, new DateOnly(2026, 10, 28), 2400.00m));

        // Am 15. ist der Einkauf schon durch, das Gehalt noch nicht da.
        Assert.Equal(1186.30m, checking.Balance(new DateOnly(2026, 10, 15)));
        // Am 31. zählt beides.
        Assert.Equal(3586.30m, checking.Balance(new DateOnly(2026, 10, 31)));
    }

    [Fact]
    public void Booking_on_the_asked_day_counts()
    {
        var checking = CheckingAccount();
        checking.Bookings.Add(Income(checking, new DateOnly(2026, 10, 28), 2400.00m));

        Assert.Equal(3640.50m, checking.Balance(new DateOnly(2026, 10, 28)));
    }

    // Spec 58, A4
    [Fact]
    public void Booking_on_the_opening_date_is_contained_in_the_opening_balance()
    {
        // Der Startbetrag ist der Stand am Ende des Starttages. Was an diesem Tag gebucht
        // wurde, steckt schon darin und würde sonst doppelt zählen.
        var checking = new Account
        {
            Name = "Giro",
            Type = AccountType.Checking,
            OpeningBalance = 1250.00m,
            OpeningDate = Opening,
            Active = true,
        };
        checking.Bookings.Add(Expense(checking, Opening, 40.00m));
        checking.Bookings.Add(Expense(checking, new DateOnly(2026, 10, 2), 60.00m));

        Assert.Equal(1190.00m, checking.Balance(new DateOnly(2026, 10, 2)));
    }

    [Fact]
    public void Booking_before_the_opening_date_is_ignored()
    {
        var checking = CheckingAccount();
        checking.Bookings.Add(Expense(checking, new DateOnly(2026, 9, 20), 100.00m));

        Assert.Equal(1240.50m, checking.Balance(new DateOnly(2026, 10, 15)));
    }

    [Fact]
    public void Correcting_a_booking_changes_the_balance_without_anything_else()
    {
        var checking = CheckingAccount();
        var groceries = Expense(checking, new DateOnly(2026, 10, 3), 54.20m);
        checking.Bookings.Add(groceries);

        // Vertippt: Es waren 45,20 Euro.
        groceries.Amount = 45.20m;
        groceries.Lines[0].Amount = 45.20m;

        Assert.Equal(1195.30m, checking.Balance(new DateOnly(2026, 10, 15)));
    }

    [Fact]
    public void Transfer_lowers_the_from_account_and_raises_the_to_account()
    {
        var checking = CheckingAccount();
        var cash = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Bargeld",
            Type = AccountType.Cash,
            OpeningBalance = 0m,
            OpeningDate = Opening,
            Active = true,
        };
        var withdrawal = new Booking
        {
            AccountId = checking.Id,
            CounterAccountId = cash.Id,
            PaymentDate = new DateOnly(2026, 10, 5),
            Amount = 100.00m,
        };
        // Eine Umbuchung berührt beide Konten, also steht sie in beiden Listen.
        checking.Bookings.Add(withdrawal);
        cash.Bookings.Add(withdrawal);

        Assert.Equal(1140.50m, checking.Balance(new DateOnly(2026, 10, 15)));
        Assert.Equal(100.00m, cash.Balance(new DateOnly(2026, 10, 15)));
    }

    [Fact]
    public void Booking_of_another_account_does_not_count()
    {
        var checking = CheckingAccount();
        // Ein Fehler beim Laden soll den Stand nicht verfälschen: Die Buchung gehört einem anderen Konto.
        checking.Bookings.Add(new Booking
        {
            AccountId = Guid.NewGuid(),
            PaymentDate = new DateOnly(2026, 10, 3),
            Amount = 54.20m,
            Lines = [new BookingLine { Amount = 54.20m, Pot = Pot.Needs }],
        });

        Assert.Equal(1240.50m, checking.Balance(new DateOnly(2026, 10, 15)));
    }

    [Fact]
    public void Closed_account_keeps_its_bookings_and_its_balance()
    {
        var checking = CheckingAccount();
        checking.Bookings.Add(Expense(checking, new DateOnly(2026, 10, 3), 54.20m));

        checking.Active = false;

        Assert.Single(checking.Bookings);
        Assert.Equal(1186.30m, checking.Balance(new DateOnly(2026, 10, 15)));
    }

    // Lenas Girokonto zum Start, noch ohne Buchungen.
    private static Account CheckingAccount() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Girokonto",
        Type = AccountType.Checking,
        OpeningBalance = 1240.50m,
        OpeningDate = Opening,
        Active = true,
    };

    // Eine Ausgabe mit einer Zeile im Topf Grundbedarf; der Betrag ist positiv.
    private static Booking Expense(Account account, DateOnly on, decimal amount) =>
        WithLine(account, on, amount, Pot.Needs);

    private static Booking Income(Account account, DateOnly on, decimal amount) =>
        WithLine(account, on, amount, Pot.Income);

    private static Booking WithLine(Account account, DateOnly on, decimal amount, Pot pot) => new()
    {
        AccountId = account.Id,
        PaymentDate = on,
        Amount = amount,
        Lines = [new BookingLine { Amount = amount, Pot = pot }],
    };
}
