using Duofy.Schemas;
using Duofy.Shared.Services;

namespace Duofy.Shared.Tests;

/// <summary>Der Entwurf einer Buchung im Dialog: Art, Zeilen, Aufteilen (R1, R3, R9, R13).</summary>
public class BookingDraftTests
{
    private static readonly DateOnly Today = new(2026, 10, 21);
    private static readonly Guid Giro = Guid.NewGuid();

    private static BookingDraft NewDraft() => BookingDraft.ForNew(Today, Giro);

    // A16 (Teil: Vorbelegung)
    [Fact]
    public void A_new_draft_is_an_expense_dated_today_on_the_default_account_with_one_line()
    {
        var draft = NewDraft();

        Assert.Equal(BookingKinds.Expense, draft.Kind);
        Assert.Equal(Today, draft.Date);
        Assert.Equal(Giro, draft.AccountId);
        Assert.Single(draft.Lines);
    }

    // Spec 72, A6, A7: Niemand soll still im Grundbedarf landen; ohne Wahl geht kein Topf an die API.
    [Fact]
    public void A_new_expense_has_no_pot_until_one_is_chosen_and_sends_none()
    {
        var draft = NewDraft();
        draft.SetAmount(80m);

        Assert.Equal("", draft.Lines[0].Pot);
        Assert.Null(draft.ToRequest().Lines![0].Pot);
    }

    [Fact]
    public void A_split_line_of_an_expense_has_no_pot_either()
    {
        var draft = NewDraft();

        draft.AddLine();

        Assert.Equal("", draft.Lines[1].Pot);
    }

    // R9: ohne Standardkonto bleibt das Feld leer
    [Fact]
    public void Without_a_default_account_the_account_is_empty()
    {
        Assert.Null(BookingDraft.ForNew(Today, defaultAccountId: null).AccountId);
    }

    [Fact]
    public void The_single_line_always_holds_the_whole_amount()
    {
        var draft = NewDraft();

        draft.SetAmount(80m);

        Assert.Equal(80m, draft.Lines[0].Amount);
    }

    // A21
    [Fact]
    public void Adding_a_line_and_giving_it_an_amount_lets_the_first_line_take_the_rest()
    {
        var draft = NewDraft();
        draft.SetAmount(80m);

        draft.AddLine();
        draft.SetLineAmount(1, 20m);

        Assert.Equal(60m, draft.Lines[0].Amount);
        Assert.Equal(20m, draft.Lines[1].Amount);
    }

    [Fact]
    public void A_new_line_starts_without_an_amount_and_changes_nothing()
    {
        var draft = NewDraft();
        draft.SetAmount(80m);

        draft.AddLine();

        Assert.Null(draft.Lines[1].Amount);
        Assert.Equal(80m, draft.Lines[0].Amount);
    }

    [Fact]
    public void Changing_the_total_changes_only_the_first_line()
    {
        var draft = NewDraft();
        draft.SetAmount(80m);
        draft.AddLine();
        draft.SetLineAmount(1, 20m);

        draft.SetAmount(100m);

        Assert.Equal(80m, draft.Lines[0].Amount);
        Assert.Equal(20m, draft.Lines[1].Amount);
    }

    [Fact]
    public void Removing_a_line_gives_its_amount_back_to_the_first_line()
    {
        var draft = NewDraft();
        draft.SetAmount(80m);
        draft.AddLine();
        draft.SetLineAmount(1, 20m);

        draft.RemoveLine(1);

        Assert.Single(draft.Lines);
        Assert.Equal(80m, draft.Lines[0].Amount);
    }

    [Fact]
    public void The_first_line_cannot_be_removed()
    {
        var draft = NewDraft();

        draft.RemoveLine(0);

        Assert.Single(draft.Lines);
    }

    [Fact]
    public void A_booking_has_at_most_20_lines()
    {
        var draft = NewDraft();

        for (var i = 0; i < 30; i++)
        {
            draft.AddLine();
        }

        Assert.Equal(BookingRules.MaxLines, draft.Lines.Count);
        Assert.False(draft.CanAddLine);
    }

    [Fact]
    public void Lines_that_take_more_than_the_total_leave_a_negative_first_line_for_the_api_to_refuse()
    {
        var draft = NewDraft();
        draft.SetAmount(80m);
        draft.AddLine();

        draft.SetLineAmount(1, 100m);

        Assert.Equal(-20m, draft.Lines[0].Amount);
    }

    // A5
    [Fact]
    public void Choosing_a_category_preselects_its_suggested_pot_and_the_pot_can_be_changed()
    {
        var restaurant = new CategoryResponse(Guid.NewGuid(), "leisure.dining", "Essen gehen", "leisure", PotKeys.Wants, true);
        var draft = NewDraft();

        draft.ChooseCategory(0, restaurant);
        Assert.Equal(PotKeys.Wants, draft.Lines[0].Pot);
        Assert.Equal(restaurant.Id, draft.Lines[0].CategoryId);

        draft.Lines[0].Pot = PotKeys.Needs;
        Assert.Equal(PotKeys.Needs, draft.Lines[0].Pot);
    }

    [Fact]
    public void Switching_to_income_sets_every_line_to_the_pot_income_and_drops_the_categories()
    {
        var draft = NewDraft();
        draft.Lines[0].CategoryId = Guid.NewGuid();

        draft.SetKind(BookingKinds.Income);

        Assert.Equal(PotKeys.Income, draft.Lines[0].Pot);
        Assert.Null(draft.Lines[0].CategoryId);
    }

    [Fact]
    public void Switching_back_to_expense_clears_the_pot()
    {
        var draft = NewDraft();
        draft.SetKind(BookingKinds.Income);

        draft.SetKind(BookingKinds.Expense);

        Assert.Equal("", draft.Lines[0].Pot);
    }

    // R1: der Topf-Wechsel behält gültige Eingaben
    [Fact]
    public void Staying_on_the_same_kind_changes_nothing()
    {
        var draft = NewDraft();
        draft.Lines[0].Pot = PotKeys.Wants;

        draft.SetKind(BookingKinds.Expense);

        Assert.Equal(PotKeys.Wants, draft.Lines[0].Pot);
    }

    [Fact]
    public void Request_of_an_expense_carries_all_lines()
    {
        var groceries = Guid.NewGuid();
        var care = Guid.NewGuid();
        var draft = NewDraft();
        draft.Note = " Wocheneinkauf ";
        draft.SetAmount(80m);
        draft.AddLine();
        draft.SetLineAmount(1, 20m);
        draft.Lines[0].CategoryId = groceries;
        draft.Lines[1].CategoryId = care;
        draft.Lines[0].Pot = PotKeys.Needs;
        draft.Lines[1].Pot = PotKeys.Needs;

        var request = draft.ToRequest();

        Assert.Equal(BookingKinds.Expense, request.Kind);
        Assert.Equal(Giro, request.AccountId);
        Assert.Null(request.CounterAccountId);
        Assert.Equal(80m, request.Amount);
        Assert.Equal("Wocheneinkauf", request.Note);
        Assert.Equal(
            [new SaveBookingLine(60m, PotKeys.Needs, groceries), new SaveBookingLine(20m, PotKeys.Needs, care)],
            request.Lines);
    }

    // Spec 72, R9: Quelle und Ziel starten leer und gehen als Topf der Zeile und als Zieltopf an die API.
    [Fact]
    public void A_new_transfer_has_no_pots_until_both_are_chosen_and_sends_them_as_line_and_target()
    {
        var draft = NewDraft();
        draft.SetKind(BookingKinds.Transfer);
        draft.CounterAccountId = Guid.NewGuid();
        draft.SetAmount(100m);

        Assert.Equal("", draft.TransferSourcePot);
        Assert.Equal("", draft.TransferTargetPot);
        Assert.Null(Assert.Single(draft.ToRequest().Lines!).Pot);
        Assert.Null(draft.ToRequest().TargetPot);

        draft.TransferSourcePot = PotKeys.Savings;
        draft.TransferTargetPot = PotKeys.Wants;
        var request = draft.ToRequest();
        Assert.Equal([new SaveBookingLine(100m, PotKeys.Savings, null)], request.Lines);
        Assert.Equal(PotKeys.Wants, request.TargetPot);
    }

    [Fact]
    public void Request_without_an_amount_sends_zero_so_the_api_refuses_it()
    {
        Assert.Equal(0m, NewDraft().ToRequest().Amount);
    }

    [Fact]
    public void A_saved_booking_becomes_a_draft_with_its_lines()
    {
        var category = Guid.NewGuid();
        var saved = new BookingResponse(
            Guid.NewGuid(), BookingKinds.Expense, Giro, null, new DateOnly(2026, 10, 3), 80m, "Notiz",
            [
                new BookingLineResponse(Guid.NewGuid(), 60m, PotKeys.Needs, category),
                new BookingLineResponse(Guid.NewGuid(), 20m, PotKeys.Wants, null),
            ]);

        var draft = BookingDraft.From(saved);

        Assert.Equal(saved.Date, draft.Date);
        Assert.Equal(80m, draft.Amount);
        Assert.Equal("Notiz", draft.Note);
        Assert.Equal(2, draft.Lines.Count);
        Assert.Equal(60m, draft.Lines[0].Amount);
        Assert.Equal(PotKeys.Wants, draft.Lines[1].Pot);
        Assert.Equal(category, draft.Lines[0].CategoryId);
    }

    [Fact]
    public void A_saved_transfer_becomes_a_draft_with_both_pots()
    {
        var saved = new BookingResponse(
            Guid.NewGuid(), BookingKinds.Transfer, Giro, Guid.NewGuid(), Today, 100m, null,
            [new BookingLineResponse(Guid.NewGuid(), 100m, PotKeys.Savings, null)], TargetPot: PotKeys.Wants);

        var draft = BookingDraft.From(saved);

        Assert.Equal((PotKeys.Savings, PotKeys.Wants), (draft.TransferSourcePot, draft.TransferTargetPot));
    }

    // Spec 72, R9: Eine ältere Umbuchung ohne Zieltopf (oder ganz ohne Zeile) lässt sich öffnen; das Ziel muss gewählt werden.
    [Fact]
    public void An_older_transfer_without_pots_opens_with_empty_fields_for_what_is_missing()
    {
        var withLine = new BookingResponse(
            Guid.NewGuid(), BookingKinds.Transfer, Giro, Guid.NewGuid(), Today, 100m, null,
            [new BookingLineResponse(Guid.NewGuid(), 100m, PotKeys.Savings, null)]);
        var withoutLine = withLine with { Lines = [] };

        Assert.Equal((PotKeys.Savings, ""), (BookingDraft.From(withLine).TransferSourcePot, BookingDraft.From(withLine).TransferTargetPot));
        Assert.Equal(("", ""), (BookingDraft.From(withoutLine).TransferSourcePot, BookingDraft.From(withoutLine).TransferTargetPot));
    }
}
