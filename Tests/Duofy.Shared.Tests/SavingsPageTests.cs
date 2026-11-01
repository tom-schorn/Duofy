using AngleSharp.Dom;
using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Layout;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Duofy.Shared.Tests;

/// <summary>
/// Issue 29 und 30: Der Menüpunkt Sparen und seine Seite mit den drei Abschnitten, die Sparziele mit Stand, Anlegen,
/// Einzahlen, Entnehmen und Löschen. Die Rechte laufen über den Freigabebereich "savings".
/// </summary>
public class SavingsPageTests : PageTestContext
{
    private readonly IRenderedComponent<MudDialogProvider> dialogs;

    public SavingsPageTests()
    {
        Render<MudPopoverProvider>();
        dialogs = Render<MudDialogProvider>();
    }

    private ActiveMember Member => Services.GetRequiredService<ActiveMember>();

    private static IElement Input(IRenderedComponent<IComponent> cut, string testId)
    {
        var element = cut.Find($"[data-testid='{testId}']");
        return element.TagName is "INPUT" or "TEXTAREA" ? element : element.QuerySelector("input,textarea")!;
    }

    private static SavingsGoalResponse Goal(string name, decimal balance, decimal? target = null, Guid? account = null) =>
        new(Guid.NewGuid(), name, target, null, account, balance, target is { } t && balance >= t);

    private IRenderedComponent<Savings> OpenPage(params SavingsGoalResponse[] goals)
    {
        SavingsGoalApiFake.Goals.AddRange(goals);
        var cut = Render<Savings>();
        cut.WaitForAssertion(() => Assert.NotEmpty(SavingsGoalApiFake.ListOwners));
        return cut;
    }

    private async Task ChooseAnnaAsync(GrantLevels levels)
    {
        HouseholdApiFake.Household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
        [
            FakeHouseholdApi.Anna(isMe: false) with { GrantsToMe = levels },
            FakeHouseholdApi.Ben(isMe: true),
        ]);
        await Member.LoadAsync();
        Member.Choose(FakeHouseholdApi.AnnaId);
    }

    [Fact]
    public void The_address_sparen_belongs_to_its_own_destination_and_stays_out_of_more()
    {
        Assert.Equal(AppDestination.Savings, AppDestinations.For("/sparen"));
        Assert.Equal(AppDestination.Savings, AppDestinations.For("sparen/ziele"));
        Assert.Contains(AppDestinations.All, d => d.Destination == AppDestination.Savings && d.Href == "/sparen");
    }

    [Fact]
    public void The_page_shows_the_three_sections_with_an_empty_state_for_each()
    {
        var cut = OpenPage();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='savings-section-goals']")));
        Assert.Contains("Sparziele", cut.Find("[data-testid='savings-section-goals']").TextContent);
        Assert.Contains("Schulden", cut.Find("[data-testid='savings-section-debts']").TextContent);
        Assert.Contains("Kredite", cut.Find("[data-testid='savings-section-loans']").TextContent);
        Assert.NotEmpty(cut.Find("[data-testid='savings-empty-goals']").TextContent);
        Assert.NotEmpty(cut.Find("[data-testid='savings-empty-debts']").TextContent);
        Assert.NotEmpty(cut.Find("[data-testid='savings-empty-loans']").TextContent);
    }

    [Fact]
    public async Task Without_a_grant_for_savings_the_page_says_the_person_does_not_share_it()
    {
        await ChooseAnnaAsync(GrantLevels.None with { Plan = AccessLevelKeys.View });

        var cut = Render<Savings>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='not-shared']")));
        Assert.Empty(cut.FindAll("[data-testid='savings-section-goals']"));
        Assert.Empty(SavingsGoalApiFake.ListOwners);
    }

    [Fact]
    public async Task With_a_savings_grant_of_view_the_page_shows_the_sections()
    {
        await ChooseAnnaAsync(GrantLevels.None with { Savings = AccessLevelKeys.View });

        var cut = OpenPage();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='savings-section-goals']")));
    }

    [Fact]
    public void The_savings_area_is_part_of_the_presets_partner_and_read_but_not_kid()
    {
        Assert.Equal(AccessLevelKeys.Delete, GrantPresets.Levels(GrantPresets.Partner).Savings);
        Assert.Equal(AccessLevelKeys.View, GrantPresets.Levels(GrantPresets.Read).Savings);
        Assert.Equal(AccessLevelKeys.None, GrantPresets.Levels(GrantPresets.Kid).Savings);
        Assert.Contains(GrantAreas.Savings, GrantAreas.All);
    }

    // Issue 30, Kriterium 1: Ziele mit Stand ansehen.
    [Fact]
    public void Each_goal_shows_its_balance_of_the_target_and_a_progress_bar()
    {
        var holiday = Goal("Urlaub", 350m, 1200m);
        var buffer = Goal("Notgroschen", 20m);

        var cut = OpenPage(holiday, buffer);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"[data-testid='savings-goal-{holiday.Id}']")));
        Assert.Equal("350,00 € von 1.200,00 €", cut.Find($"[data-testid='savings-goal-balance-{holiday.Id}']").TextContent.Trim());
        Assert.NotEmpty(cut.FindAll($"[data-testid='savings-goal-progress-{holiday.Id}']"));
        Assert.Equal("Stand: 20,00 €", cut.Find($"[data-testid='savings-goal-balance-{buffer.Id}']").TextContent.Trim());
        Assert.Empty(cut.FindAll($"[data-testid='savings-goal-progress-{buffer.Id}']"));
        Assert.Empty(cut.FindAll("[data-testid='savings-empty-goals']"));
    }

    [Fact]
    public void A_reached_goal_says_so_calmly_and_an_open_one_does_not()
    {
        var reached = Goal("Laptop", 500m, 500m);
        var open = Goal("Urlaub", 100m, 1200m);

        var cut = OpenPage(reached, open);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"[data-testid='savings-goal-reached-{reached.Id}']")));
        Assert.Empty(cut.FindAll($"[data-testid='savings-goal-reached-{open.Id}']"));
    }

    [Fact]
    public void The_account_of_a_goal_is_shown_by_name()
    {
        var savings = Guid.NewGuid();
        Accounts.Accounts.Add(new AccountChoice(savings, "Tagesgeld", IsDefault: false, Active: true));

        var cut = OpenPage(Goal("Urlaub", 0m, 500m, savings));

        cut.WaitForAssertion(() => Assert.Contains("Liegt auf: Tagesgeld", cut.Markup));
    }

    [Fact]
    public void Saving_the_create_dialog_posts_the_goal_and_lists_it()
    {
        var cut = OpenPage();
        cut.Find("[data-testid='savings-goal-add']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Neues Sparziel", dialogs.Markup));

        Input(dialogs, "savings-goal-name").Change("Urlaub");
        Input(dialogs, "savings-goal-target").Change("1200");
        dialogs.Find("[data-testid='savings-goal-save']").Click();

        cut.WaitForAssertion(() => Assert.Single(SavingsGoalApiFake.Created));
        Assert.Equal(new CreateSavingsGoalRequest("Urlaub", 1200m, StartAmount: 0m), SavingsGoalApiFake.Created[0]);
        cut.WaitForAssertion(() => Assert.Contains("Urlaub", cut.Markup));
        Assert.Contains("Sparziel gespeichert.", cut.Markup);
    }

    // Tom 01.11.: Ein Sparziel kennt den einmaligen Startwert "Schon angespart".
    [Fact]
    public void The_start_value_is_sent_with_the_goal_and_prefilled_when_changing_it()
    {
        var cut = OpenPage();
        cut.Find("[data-testid='savings-goal-add']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='savings-goal-start']")));
        Assert.Equal("0,00", Input(dialogs, "savings-goal-start").GetAttribute("value"));
        Input(dialogs, "savings-goal-name").Change("Urlaub");
        Input(dialogs, "savings-goal-start").Change("250");
        dialogs.Find("[data-testid='savings-goal-save']").Click();
        cut.WaitForAssertion(() => Assert.Single(SavingsGoalApiFake.Created));
        Assert.Equal(250m, SavingsGoalApiFake.Created[0].StartAmount);
        var goal = SavingsGoalApiFake.Goals[0];
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"[data-testid='savings-goal-edit-{goal.Id}']")));
        cut.Find($"[data-testid='savings-goal-edit-{goal.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.Equal("250,00", Input(dialogs, "savings-goal-start").GetAttribute("value")));
    }

    [Fact]
    public void An_error_of_the_api_stands_in_the_dialog()
    {
        var cut = OpenPage();
        SavingsGoalApiFake.Error = ErrorCodes.SavingsGoalNameRequired;
        cut.Find("[data-testid='savings-goal-add']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='savings-goal-save']")));

        dialogs.Find("[data-testid='savings-goal-save']").Click();

        dialogs.WaitForAssertion(() => Assert.Contains("Bitte gib dem Sparziel einen Namen.", dialogs.Find("[data-testid='savings-goal-error']").TextContent));
    }

    // Issue 30, Kriterium 2: Einzahlungen und Entnahmen werden einem Ziel zugeordnet.
    [Fact]
    public void A_deposit_books_a_transfer_whose_line_names_the_goal()
    {
        var giro = Guid.NewGuid();
        var savings = Guid.NewGuid();
        Accounts.Accounts.Add(new AccountChoice(giro, "Giro", IsDefault: true, Active: true));
        Accounts.Accounts.Add(new AccountChoice(savings, "Tagesgeld", IsDefault: false, Active: true));
        var goal = Goal("Urlaub", 0m, 1200m, savings);
        var cut = OpenPage(goal);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"[data-testid='savings-goal-deposit-{goal.Id}']")));

        cut.Find($"[data-testid='savings-goal-deposit-{goal.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='savings-move-save']")));
        Assert.Equal(savings.ToString(), dialogs.Find("[data-testid='savings-move-savings']").GetAttribute("value"));
        Input(dialogs, "savings-move-amount").Change("150");
        dialogs.Find("[data-testid='savings-move-other']").Change(giro.ToString());
        dialogs.Find("[data-testid='savings-move-save']").Click();

        cut.WaitForAssertion(() => Assert.Single(BookingApiFake.Created));
        var booking = BookingApiFake.Created[0];
        Assert.Equal((BookingKinds.Transfer, giro, savings, 150m), (booking.Kind, booking.AccountId, booking.CounterAccountId, booking.Amount));
        Assert.Equal(PotKeys.Savings, booking.TargetPot);
        Assert.Equal(goal.Id, Assert.Single(booking.Lines!).SavingsGoalId);
        cut.WaitForAssertion(() => Assert.Contains("Gebucht.", cut.Markup));
    }

    [Fact]
    public void A_withdrawal_without_a_second_account_is_not_sent()
    {
        var savings = Guid.NewGuid();
        Accounts.Accounts.Add(new AccountChoice(savings, "Tagesgeld", IsDefault: false, Active: true));
        var goal = Goal("Urlaub", 0m, 1200m, savings);
        var cut = OpenPage(goal);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"[data-testid='savings-goal-withdraw-{goal.Id}']")));

        cut.Find($"[data-testid='savings-goal-withdraw-{goal.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='savings-move-save']")));
        Input(dialogs, "savings-move-amount").Change("50");
        dialogs.Find("[data-testid='savings-move-save']").Click();

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='savings-move-error']")));
        Assert.Empty(BookingApiFake.Created);
    }

    [Fact]
    public void A_refused_delete_is_explained_calmly()
    {
        var goal = Goal("Urlaub", 0m);
        var cut = OpenPage(goal);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"[data-testid='savings-goal-delete-{goal.Id}']")));
        SavingsGoalApiFake.Error = ErrorCodes.SavingsGoalHasBookings;

        cut.Find($"[data-testid='savings-goal-delete-{goal.Id}']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Löse erst die Zuordnung", cut.Markup));
        Assert.Equal(goal.Id, Assert.Single(SavingsGoalApiFake.Deleted));
    }

    // Rechte: Mit nur Ansehen sind Ändern, Löschen und Einzahlen ausgegraut.
    [Fact]
    public async Task With_view_only_the_buttons_are_locked_and_the_goals_of_that_person_are_asked_for()
    {
        await ChooseAnnaAsync(GrantLevels.None with { Savings = AccessLevelKeys.View });
        var goal = Goal("Urlaub", 10m, 100m);

        var cut = OpenPage(goal);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"[data-testid='savings-goal-edit-{goal.Id}']")));
        Assert.Equal("true", cut.Find($"[data-testid='savings-goal-edit-{goal.Id}']").GetAttribute("aria-disabled"));
        Assert.Equal("true", cut.Find($"[data-testid='savings-goal-delete-{goal.Id}']").GetAttribute("aria-disabled"));
        Assert.Equal("true", cut.Find($"[data-testid='savings-goal-deposit-{goal.Id}']").GetAttribute("aria-disabled"));
        Assert.Equal(FakeHouseholdApi.AnnaId, SavingsGoalApiFake.ListOwners[0]);
    }

    // Issue 31: Schulden und Kredite.
    private static DebtResponse Debt(string kind, string name, decimal original, decimal paid, string creditor = "Stadtwerke") =>
        new(Guid.NewGuid(), kind, name, creditor, original, null, null, paid, Math.Max(0m, original - paid), paid >= original);

    [Fact]
    public void Claims_and_loans_stand_in_their_own_sections_with_creditor_and_what_is_open()
    {
        var claim = Debt(DebtKinds.Claim, "Stromrechnung", 300m, 100m);
        var loan = Debt(DebtKinds.Loan, "Autokredit", 5000m, 0m, "Sparkasse");
        DebtApiFake.Debts.AddRange(claim, loan);

        var cut = OpenPage();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"[data-testid='debt-{claim.Id}']")));
        Assert.NotEmpty(cut.Find("[data-testid='savings-section-debts']").QuerySelectorAll($"[data-testid='debt-{claim.Id}']"));
        Assert.NotEmpty(cut.Find("[data-testid='savings-section-loans']").QuerySelectorAll($"[data-testid='debt-{loan.Id}']"));
        Assert.Equal("An: Stadtwerke", cut.Find($"[data-testid='debt-creditor-{claim.Id}']").TextContent.Trim());
        Assert.Equal("Noch offen: 200,00 € von 300,00 €", cut.Find($"[data-testid='debt-open-{claim.Id}']").TextContent.Trim());
        Assert.Equal("Noch offen: 5.000,00 € von 5.000,00 €", cut.Find($"[data-testid='debt-open-{loan.Id}']").TextContent.Trim());
        Assert.Empty(cut.FindAll("[data-testid='savings-empty-debts']"));
        Assert.Empty(cut.FindAll("[data-testid='savings-empty-loans']"));
    }

    [Fact]
    public void A_settled_entry_says_so_calmly_and_the_page_points_to_real_help_without_legal_claims()
    {
        var settled = Debt(DebtKinds.Claim, "Rechnung", 50m, 50m);
        DebtApiFake.Debts.Add(settled);

        var cut = OpenPage();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"[data-testid='debt-settled-{settled.Id}']")));
        var help = cut.Find("[data-testid='savings-help']").TextContent;
        Assert.Contains("Schuldnerberatung", help);
        Assert.Contains("keine Rechtsberatung", help);
        // Kein Alarm: weder Fehler- noch Warnfarbe in den Abschnitten der Schulden.
        var sections = cut.Find("[data-testid='savings-section-debts']").InnerHtml + cut.Find("[data-testid='savings-section-loans']").InnerHtml;
        Assert.DoesNotContain("mud-error", sections);
        Assert.DoesNotContain("mud-warning", sections);
    }

    [Fact]
    public void Saving_the_dialog_creates_a_claim_or_a_loan_with_its_kind()
    {
        var cut = OpenPage();

        cut.Find($"[data-testid='debt-add-{DebtKinds.Loan}']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Kredit eintragen", dialogs.Markup));
        Input(dialogs, "debt-name").Change("Autokredit");
        Input(dialogs, "debt-creditor").Change("Sparkasse");
        Input(dialogs, "debt-amount").Change("5000");
        dialogs.Find("[data-testid='debt-save']").Click();

        cut.WaitForAssertion(() => Assert.Single(DebtApiFake.Created));
        Assert.Equal(new CreateDebtRequest(DebtKinds.Loan, "Autokredit", "Sparkasse", 5000m), DebtApiFake.Created[0]);
        cut.WaitForAssertion(() => Assert.Contains("Autokredit", cut.Markup));
    }

    [Fact]
    public void A_payment_books_an_expense_whose_line_names_the_debt()
    {
        var giro = new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true);
        Accounts.Accounts.Add(giro);
        var debt = Debt(DebtKinds.Claim, "Stromrechnung", 300m, 0m);
        DebtApiFake.Debts.Add(debt);
        var debtCategory = new CategoryResponse(Guid.NewGuid(), "finance.debt", "Schulden", "finance", PotKeys.Savings, true);
        Categories.All.Add(debtCategory);
        var cut = OpenPage();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"[data-testid='debt-pay-{debt.Id}']")));

        cut.Find($"[data-testid='debt-pay-{debt.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='debt-payment-save']")));
        Input(dialogs, "debt-payment-amount").Change("120");
        dialogs.Find("[data-testid='debt-payment-save']").Click();

        cut.WaitForAssertion(() => Assert.Single(BookingApiFake.Created));
        var booking = BookingApiFake.Created[0];
        Assert.Equal((BookingKinds.Expense, giro.Id, 120m), (booking.Kind, booking.AccountId, booking.Amount));
        var line = Assert.Single(booking.Lines!);
        Assert.Equal((debt.Id, debtCategory.Id, PotKeys.Needs), (line.DebtId, line.CategoryId, line.Pot));
        cut.WaitForAssertion(() => Assert.Contains("Zahlung gebucht.", cut.Markup));
    }

    [Fact]
    public void A_refused_delete_of_an_entry_is_explained_calmly()
    {
        var debt = Debt(DebtKinds.Claim, "Rechnung", 50m, 10m);
        DebtApiFake.Debts.Add(debt);
        var cut = OpenPage();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"[data-testid='debt-delete-{debt.Id}']")));
        DebtApiFake.Error = ErrorCodes.DebtHasBookings;

        cut.Find($"[data-testid='debt-delete-{debt.Id}']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Löse erst die Zuordnung", cut.Markup));
        Assert.Equal(debt.Id, Assert.Single(DebtApiFake.Deleted));
    }

    [Fact]
    public async Task With_view_only_the_debt_buttons_are_locked_and_the_entries_of_that_person_are_asked_for()
    {
        await ChooseAnnaAsync(GrantLevels.None with { Savings = AccessLevelKeys.View });
        var debt = Debt(DebtKinds.Claim, "Rechnung", 50m, 10m);
        DebtApiFake.Debts.Add(debt);

        var cut = OpenPage();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"[data-testid='debt-edit-{debt.Id}']")));
        Assert.Equal("true", cut.Find($"[data-testid='debt-edit-{debt.Id}']").GetAttribute("aria-disabled"));
        Assert.Equal("true", cut.Find($"[data-testid='debt-delete-{debt.Id}']").GetAttribute("aria-disabled"));
        Assert.Equal("true", cut.Find($"[data-testid='debt-pay-{debt.Id}']").GetAttribute("aria-disabled"));
        Assert.Equal(FakeHouseholdApi.AnnaId, DebtApiFake.ListOwners[0]);
    }
}
