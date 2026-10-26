using Duofy.Schemas;
using Duofy.Shared.Services;

namespace Duofy.Shared.Tests;

/// <summary>Der Entwurf eines Kontos im Dialog: Vorbelegung und der Schalter "zählt als verfügbar" (Spec 58, R2).</summary>
public class AccountDraftTests
{
    private static readonly DateOnly Today = new(2026, 10, 21);

    [Fact]
    public void A_new_draft_is_a_checking_account_opened_today_that_counts_as_available()
    {
        var draft = AccountDraft.ForNew(Today);

        Assert.Equal(AccountTypes.Checking, draft.Type);
        Assert.Equal(Today, draft.OpeningDate);
        Assert.True(draft.CountsAsAvailable);
    }

    // Spec 58, A3
    [Theory]
    [InlineData(AccountTypes.Savings, false)]
    [InlineData(AccountTypes.Checking, true)]
    [InlineData(AccountTypes.Cash, true)]
    public void Choosing_a_type_presets_the_switch(string type, bool expected)
    {
        var draft = AccountDraft.ForNew(Today);
        draft.SetType(AccountTypes.Savings);

        draft.SetType(type);

        Assert.Equal(expected, draft.CountsAsAvailable);
    }

    // Spec 58, R2: Eine spätere Wahl der Art lässt einen selbst gestellten Schalter in Ruhe.
    [Fact]
    public void A_switch_the_person_changed_stays_when_the_type_changes()
    {
        var draft = AccountDraft.ForNew(Today);
        draft.SetType(AccountTypes.Savings);
        draft.SetCountsAsAvailable(true);

        draft.SetType(AccountTypes.Checking);
        draft.SetType(AccountTypes.Savings);

        Assert.True(draft.CountsAsAvailable);
    }

    [Fact]
    public void Editing_keeps_the_saved_switch_when_the_type_changes()
    {
        var saved = new AccountResponse(Guid.NewGuid(), "Tagesgeld", AccountTypes.Savings, 0m, Today, true, null, false, true, 0m);
        var draft = AccountDraft.From(saved);

        draft.SetType(AccountTypes.Savings);

        Assert.True(draft.CountsAsAvailable);
    }

    [Fact]
    public void The_create_request_carries_all_fields()
    {
        var draft = AccountDraft.ForNew(Today);
        draft.Name = "Giro";
        draft.OpeningBalance = 1250m;
        draft.Iban = "DE89 3704";

        var request = draft.ToCreateRequest();

        Assert.Equal(new CreateAccountRequest("Giro", AccountTypes.Checking, 1250m, Today, true, "DE89 3704"), request);
    }

    [Fact]
    public void An_empty_opening_balance_counts_as_zero()
    {
        Assert.Equal(0m, AccountDraft.ForNew(Today).ToCreateRequest().OpeningBalance);
    }

    [Fact]
    public void The_update_request_sends_an_empty_iban_so_a_removed_iban_is_removed()
    {
        var saved = new AccountResponse(Guid.NewGuid(), "Giro", AccountTypes.Checking, 10m, Today, true, "DE89370400440532013000", true, true, 10m);
        var draft = AccountDraft.From(saved);
        draft.Iban = "";

        var request = draft.ToUpdateRequest();

        Assert.Equal(new UpdateAccountRequest("Giro", AccountTypes.Checking, 10m, Today, true, ""), request);
    }
}
