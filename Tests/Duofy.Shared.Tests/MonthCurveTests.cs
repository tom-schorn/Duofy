using Duofy.Schemas;
using Duofy.Shared.Services;

namespace Duofy.Shared.Tests;

/// <summary>Die Tageskurve des Monats (Spec 82, R14, A16) und ihr Beginn (Korrektur vom 03.11.2026).</summary>
public class MonthCurveTests
{
    private static readonly DateOnly Today = new(2026, 11, 10);

    // Korrektur 03.11.2026: Die Kurve beginnt mit dem Übertrag und den Einnahmen, die per Planzuordnung schon zu diesem Monat gehören.
    [Fact]
    public void The_curve_starts_with_the_carry_over_and_income_assigned_before_the_month()
    {
        var salary = new CurveMove(new DateOnly(2026, 10, 29), 1735.20m);

        var curve = MonthCurve.Build(2026, 11, Today, 120m, [salary], []);

        Assert.Equal(1855.20m, curve.Start);
        Assert.Equal(1855.20m, curve.Points[0].Balance);
        Assert.Equal(30, curve.Points.Count);
    }

    // R14: bis heute aus den echten Buchungen, ab morgen aus den offenen Posten an ihrem Fälligkeitstag.
    [Fact]
    public void Until_today_bookings_count_and_from_tomorrow_the_open_positions()
    {
        var curve = MonthCurve.Build(2026, 11, Today, 1000m,
            [new CurveMove(new DateOnly(2026, 11, 2), -900m), new CurveMove(new DateOnly(2026, 11, 5), -50m)],
            [new CurveOpen(15, -300m), new CurveOpen(27, 347m, Income: true)]);

        Assert.Equal(1347m, curve.Points[0].Balance);
        Assert.Equal(447m, curve.Points[1].Balance);
        Assert.Equal(397m, curve.Points[9].Balance);
        Assert.False(curve.Points[9].Forecast);
        Assert.True(curve.Points[10].Forecast);
        Assert.Equal(97m, curve.Points[14].Balance);
        Assert.Equal(97m, curve.Points[26].Balance);
    }

    // A16: Eine offene Ausgabe drückt den Stand unter null; die Kurve kennt den Tag und den Betrag der Lücke.
    [Fact]
    public void A16_an_open_expense_below_zero_names_the_day_and_the_gap()
    {
        var curve = MonthCurve.Build(2026, 11, Today, 200m, [], [new CurveOpen(15, -300m), new CurveOpen(20, -80m)]);

        Assert.Equal(new DateOnly(2026, 11, 15), curve.FirstBelowZero);
        Assert.Equal(new DateOnly(2026, 11, 20), curve.Lowest.Day);
        Assert.Equal(180m, curve.Gap);
    }

    [Fact]
    public void Without_falling_below_zero_there_is_no_gap()
    {
        var curve = MonthCurve.Build(2026, 11, Today, 500m, [], [new CurveOpen(15, -300m)]);

        Assert.Null(curve.FirstBelowZero);
        Assert.Equal(0m, curve.Gap);
        Assert.Equal(200m, curve.Lowest.Balance);
    }

    // Ein offener Posten, dessen Tag vorbei ist, ist noch zu bezahlen und zählt morgen.
    [Fact]
    public void An_overdue_open_position_counts_tomorrow()
    {
        var curve = MonthCurve.Build(2026, 11, Today, 100m, [], [new CurveOpen(3, -40m)]);

        Assert.Equal(100m, curve.Points[9].Balance);
        Assert.Equal(60m, curve.Points[10].Balance);
    }

    // Ein vergangener Monat zählt nur seine Buchungen; eine Buchung nach dem Monat zählt am letzten Tag.
    [Fact]
    public void A_past_month_counts_only_bookings_and_a_late_booking_on_the_last_day()
    {
        var curve = MonthCurve.Build(2026, 9, Today, 0m, [new CurveMove(new DateOnly(2026, 10, 2), -20m)], [new CurveOpen(15, -300m)]);

        Assert.All(curve.Points.Take(29), p => Assert.Equal(0m, p.Balance));
        Assert.Equal(-20m, curve.Points[29].Balance);
        Assert.All(curve.Points, p => Assert.False(p.Forecast));
    }

    // Tom, 03.11.2026: "Am 1. des Monats". Offene Einnahmen des Plans zählen mit ihrem Rest am Monatsanfang, egal welcher Tag;
    // der Lohn vom 29. für den nächsten Plan reißt sonst eine Lücke, die es nicht gibt.
    [Fact]
    public void Open_income_of_the_plan_counts_on_the_first_so_equal_wage_and_rent_leave_no_gap()
    {
        var plan = FakePlanApi.Empty(2026, 11) with
        {
            Positions =
            [
                FakePlanApi.Position("Lohn", 900m, PotKeys.Income, day: 29),
                FakePlanApi.Position("Miete", 900m, day: 2),
            ],
            Overview = new PlanOverviewResponse(900m, 900m, 0m, [], []),
        };

        var curve = MonthCurve.ForPerson(plan, new DateOnly(2026, 11, 1));

        Assert.Equal(900m, curve.Points[0].Balance);
        Assert.Equal(0m, curve.Points[1].Balance);
        Assert.Null(curve.FirstBelowZero);
        Assert.Equal(0m, curve.Gap);
    }

    [Fact]
    public void Open_income_counts_on_the_first_even_when_today_is_later_and_only_its_open_rest()
    {
        var curve = MonthCurve.Build(2026, 11, Today, 0m, [new CurveMove(new DateOnly(2026, 11, 5), 300m)],
            [new CurveOpen(29, 700m, Income: true)]);

        Assert.Equal(700m, curve.Points[0].Balance);
        Assert.Equal(1000m, curve.Points[4].Balance);
        Assert.Equal(1000m, curve.Points[29].Balance);
    }

    [Fact]
    public void From_the_plan_transfers_pass_through_and_ticked_positions_do_not_count()
    {
        var giro = Guid.NewGuid();
        var plan = Duofy.Shared.Tests.FakePlanApi.Empty(2026, 11) with
        {
            Positions =
            [
                FakePlanApi.Position("Miete", 900m, booked: 900m, done: true, day: 2),
                FakePlanApi.Position("Auslage", 50m, day: 20) with { PassThrough = true },
                FakePlanApi.Position("Strom", 123m, booked: 23m, day: 20),
                FakePlanApi.Position("Lohn", 1735.20m, PotKeys.Income, day: 29),
            ],
            Overview = new PlanOverviewResponse(1735.20m, 1073m, 662.20m, [],
                [
                    new BookingResponse(Guid.NewGuid(), BookingKinds.Expense, giro, null, new DateOnly(2026, 11, 2), 900m, null, [], 2026, 11),
                    new BookingResponse(Guid.NewGuid(), BookingKinds.Transfer, giro, Guid.NewGuid(), new DateOnly(2026, 11, 3), 200m, null, [], 2026, 11),
                ],
                [new PlanCarryOverLineResponse(CarryOverDirections.In, 2026, 10, 1000m), new PlanCarryOverLineResponse(CarryOverDirections.Out, 2026, 12, 5m)]),
        };

        var curve = MonthCurve.ForPerson(plan, Today);

        Assert.Equal(1000m, curve.Start);
        Assert.Equal(1835.20m, curve.Points[2].Balance);
        Assert.Equal(1735.20m, curve.Points[19].Balance);
        Assert.Equal(1735.20m, curve.Points[28].Balance);
    }
}
