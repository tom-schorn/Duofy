using Duofy.Schemas;
using Duofy.Shared.Services;

namespace Duofy.Shared.Tests;

/// <summary>Planmonat und Posten je Zeile im Buchungsentwurf (Spec 66, R6, R7).</summary>
public class BookingDraftPlanTests
{
    private static readonly DateOnly October21 = new(2026, 10, 21);
    private static readonly Guid Giro = Guid.NewGuid();

    private static PlanPositionResponse Position(string pot, Guid? category = null) => new(
        Guid.NewGuid(), null, "Posten", 100m, 1, CommitmentTypes.Contract, category ?? Guid.NewGuid(), pot,
        null, null, false, PaymentMethods.Other, 0m, null, false, null);

    [Fact]
    public void A_new_booking_counts_in_the_month_of_its_date_and_offers_the_months_around_it()
    {
        var draft = BookingDraft.ForNew(October21, Giro);

        Assert.Equal(new DateOnly(2026, 10, 1), draft.PlanMonth);
        Assert.Equal([new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1)], draft.PlanMonthOptions);
        var request = draft.ToRequest();
        Assert.Equal((2026, 10), (request.PlanYear, request.PlanMonth));
    }

    [Fact]
    public void A_new_booking_can_start_in_a_given_month_far_from_its_date()
    {
        var draft = BookingDraft.ForNew(October21, Giro, new DateOnly(2026, 12, 15));

        Assert.Equal(new DateOnly(2026, 12, 1), draft.PlanMonth);
        Assert.Equal(October21, draft.Date);
        var request = draft.ToRequest();
        Assert.Equal((2026, 12), (request.PlanYear, request.PlanMonth));
    }

    [Fact]
    public void A_salary_on_the_25th_may_count_in_the_next_month()
    {
        var draft = BookingDraft.ForNew(new DateOnly(2026, 9, 25), Giro);

        draft.SetPlanMonth(new DateOnly(2026, 10, 1));

        Assert.Equal((2026, 10), (draft.ToRequest().PlanYear, draft.ToRequest().PlanMonth));
    }

    [Fact]
    public void Moving_the_date_too_far_takes_the_month_of_the_new_date()
    {
        var draft = BookingDraft.ForNew(October21, Giro);
        draft.SetPlanMonth(new DateOnly(2026, 11, 1));

        draft.Date = new DateOnly(2026, 10, 30);
        Assert.Equal(new DateOnly(2026, 11, 1), draft.PlanMonth);

        draft.Date = new DateOnly(2027, 1, 5);
        Assert.Equal(new DateOnly(2027, 1, 1), draft.PlanMonth);
    }

    [Fact]
    public void Each_line_carries_its_position_into_the_request()
    {
        var groceries = Position(PotKeys.Needs);
        var drugstore = Position(PotKeys.Needs);
        var draft = BookingDraft.ForNew(October21, Giro);
        draft.SetAmount(80m);
        draft.AddLine();
        draft.SetLineAmount(1, 20m);

        draft.ChoosePosition(0, groceries);
        draft.ChoosePosition(1, drugstore);

        var lines = draft.ToRequest().Lines!;
        Assert.Equal(groceries.Id, lines[0].PositionId);
        Assert.Equal(drugstore.Id, lines[1].PositionId);
    }

    [Fact]
    public void Choosing_a_position_fills_an_empty_category_and_its_pot()
    {
        var category = Guid.NewGuid();
        var draft = BookingDraft.ForNew(October21, Giro);

        draft.ChoosePosition(0, Position(PotKeys.Wants, category));

        Assert.Equal(category, draft.Lines[0].CategoryId);
        Assert.Equal(PotKeys.Wants, draft.Lines[0].Pot);
    }

    [Fact]
    public void Another_plan_month_or_kind_clears_the_positions()
    {
        var draft = BookingDraft.ForNew(October21, Giro);
        draft.ChoosePosition(0, Position(PotKeys.Needs));

        draft.SetPlanMonth(new DateOnly(2026, 11, 1));
        Assert.Null(draft.Lines[0].PositionId);

        draft.ChoosePosition(0, Position(PotKeys.Needs));
        draft.SetKind(BookingKinds.Income);
        Assert.Null(draft.Lines[0].PositionId);
    }

    [Fact]
    public void A_savings_transfer_may_fill_a_savings_position()
    {
        var goal = Position(PotKeys.Savings);
        var draft = BookingDraft.ForNew(October21, Giro);
        draft.SetKind(BookingKinds.Transfer);
        draft.SetAmount(100m);
        draft.TransferSourcePot = PotKeys.Savings;
        draft.TransferTargetPot = PotKeys.Savings;

        draft.TransferPositionId = goal.Id;

        Assert.Equal(goal.Id, Assert.Single(draft.ToRequest().Lines!).PositionId);
    }

    [Fact]
    public void Editing_keeps_the_stored_plan_month_and_positions()
    {
        var position = Guid.NewGuid();
        var stored = new BookingResponse(Guid.NewGuid(), BookingKinds.Income, Giro, null, new DateOnly(2026, 9, 25), 2400m, null,
            [new BookingLineResponse(Guid.NewGuid(), 2400m, PotKeys.Income, Guid.NewGuid(), position)], 2026, 10);

        var draft = BookingDraft.From(stored);

        Assert.Equal(new DateOnly(2026, 10, 1), draft.PlanMonth);
        Assert.Equal(position, draft.Lines[0].PositionId);
    }

    [Fact]
    public void The_picker_offers_open_positions_of_the_fitting_kind()
    {
        var rent = Position(PotKeys.Needs);
        var salary = Position(PotKeys.Income);
        var goal = Position(PotKeys.Savings);
        var done = Position(PotKeys.Needs) with { DoneAt = DateTimeOffset.UnixEpoch };
        var plan = new PlanResponse(Guid.NewGuid(), 2026, 10, [rent, salary, goal, done], 0m, 0m, 0m, 0m, [], [],
            new PlanCarryOverResponse(0m, false, null, []));

        Assert.Equal([rent, goal], PlanView.OfferedPositions(plan, BookingKinds.Expense, null));
        Assert.Equal([salary], PlanView.OfferedPositions(plan, BookingKinds.Income, null));
        Assert.Equal([goal], PlanView.OfferedPositions(plan, BookingKinds.Transfer, null));
        Assert.Contains(done, PlanView.OfferedPositions(plan, BookingKinds.Expense, done.Id));
    }

    // A19
    [Fact]
    public void A_transfer_line_may_fill_any_position_with_its_own_target_account()
    {
        var goal = Position(PotKeys.Savings);
        var envelope = Position(PotKeys.Needs) with { CounterAccountId = Guid.NewGuid() };
        var ticked = Position(PotKeys.Needs) with { CounterAccountId = Guid.NewGuid(), DoneAt = DateTimeOffset.UnixEpoch };
        var rent = Position(PotKeys.Needs);
        var plan = new PlanResponse(Guid.NewGuid(), 2026, 10, [goal, envelope, ticked, rent], 0m, 0m, 0m, 0m, [], [],
            new PlanCarryOverResponse(0m, false, null, []));

        Assert.Equal([goal, envelope], PlanView.OfferedPositions(plan, BookingKinds.Transfer, null));
        Assert.Equal([goal, envelope, ticked], PlanView.OfferedPositions(plan, BookingKinds.Transfer, ticked.Id));
    }
}
