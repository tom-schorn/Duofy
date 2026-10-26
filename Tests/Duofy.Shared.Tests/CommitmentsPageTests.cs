using System.Reflection;
using AngleSharp.Dom;
using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Duofy.Shared.Tests;

/// <summary>Die Seite /vertraege: Liste, beendete auf Wunsch, Beenden und der Dialog (Spec 64, R8, R10).</summary>
public class CommitmentsPageTests : PageTestContext
{
    private static readonly DateOnly Today = new(2026, 10, 21);

    // MudBlazor zeichnet Dialoge und Popups in diese beiden Bausteine (im echten Layout stehen sie im MainLayout).
    private readonly IRenderedComponent<MudDialogProvider> dialogs;

    public CommitmentsPageTests()
    {
        Render<MudPopoverProvider>();
        dialogs = Render<MudDialogProvider>();
        Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true));
    }

    private IRenderedComponent<Commitments> OpenPage(params CommitmentResponse[] commitments)
    {
        CommitmentApiFake.Commitments.AddRange(commitments);
        var cut = Render<Commitments>();
        cut.WaitForAssertion(() => Assert.NotEmpty(CommitmentApiFake.ListCalls));
        return cut;
    }

    private static IElement Row(IRenderedComponent<Commitments> cut, CommitmentResponse commitment) =>
        cut.Find($"[data-testid='commitment-{commitment.Id}']");

    private static bool Shows(IRenderedComponent<Commitments> cut, CommitmentResponse commitment) =>
        cut.FindAll($"[data-testid='commitment-{commitment.Id}']").Count > 0;

    private void OpenNewDialog(IRenderedComponent<Commitments> cut)
    {
        cut.Find("[data-testid='commitments-add']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Neuer Vertrag", dialogs.Markup));
    }

    private static IElement Input(IRenderedComponent<IComponent> cut, string testId)
    {
        var element = cut.Find($"[data-testid='{testId}']");
        return element.TagName is "INPUT" or "TEXTAREA" ? element : element.QuerySelector("input,textarea")!;
    }

    private static void SelectIn(IRenderedComponent<IComponent> cut, string testId, string value) =>
        cut.Find($"[data-testid='{testId}']").Change(value);

    [Fact]
    public void The_page_is_reachable_at_vertraege_and_needs_a_sign_in()
    {
        Assert.Equal("/vertraege", typeof(Commitments).GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.NotNull(typeof(Commitments).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void A_row_shows_name_type_amount_interval_in_words_and_next_due_date()
    {
        var rent = FakeCommitmentApi.Commitment("Miete", 890m, nextDue: new DateOnly(2026, 11, 1));
        var insurance = FakeCommitmentApi.Commitment("Hausrat", 120.5m, interval: 12);
        var quarterly = FakeCommitmentApi.Commitment("Wasser", 60m, interval: 3);
        var odd = FakeCommitmentApi.Commitment("Schornsteinfeger", 80m, interval: 18);

        var cut = OpenPage(rent, insurance, quarterly, odd);

        cut.WaitForAssertion(() => Assert.Contains("Miete", Row(cut, rent).TextContent));
        Assert.Contains("Vertrag", Row(cut, rent).TextContent);
        Assert.Contains("890,00 €", Row(cut, rent).TextContent);
        Assert.Contains("monatlich", Row(cut, rent).TextContent);
        Assert.Contains("01.11.2026", Row(cut, rent).TextContent);
        Assert.Contains("120,50 €", Row(cut, insurance).TextContent);
        Assert.Contains("jährlich", Row(cut, insurance).TextContent);
        Assert.Contains("vierteljährlich", Row(cut, quarterly).TextContent);
        Assert.Contains("alle 18 Monate", Row(cut, odd).TextContent);
    }

    [Fact]
    public void Without_contracts_the_page_invites_to_create_one()
    {
        var cut = OpenPage();

        cut.WaitForAssertion(() => Assert.Contains("Du hast noch keinen Vertrag.", cut.Markup));
    }

    // A14
    [Fact]
    public void Only_running_contracts_are_shown_until_ended_ones_are_switched_on()
    {
        var running = FakeCommitmentApi.Commitment("Miete");
        var ended = FakeCommitmentApi.Commitment("Altes Fitnessstudio", running: false);
        var cut = OpenPage(running, ended);
        cut.WaitForAssertion(() => Assert.True(Shows(cut, running)));
        Assert.False(Shows(cut, ended));

        Input(cut, "commitments-show-ended").Change(true);

        cut.WaitForAssertion(() => Assert.True(Shows(cut, ended)));
        Assert.True(Shows(cut, running));
        Assert.Contains("beendet", Row(cut, ended).TextContent);
    }

    [Fact]
    public void With_only_ended_contracts_the_page_says_they_are_hidden()
    {
        var ended = FakeCommitmentApi.Commitment("Altes Fitnessstudio", running: false);
        var cut = OpenPage(ended);

        cut.WaitForAssertion(() => Assert.Contains("Beendete Verträge sind ausgeblendet.", cut.Markup));
        Assert.DoesNotContain("Du hast noch keinen Vertrag.", cut.Markup);

        Input(cut, "commitments-show-ended").Change(true);

        cut.WaitForAssertion(() => Assert.True(Shows(cut, ended)));
        Assert.DoesNotContain("Beendete Verträge sind ausgeblendet.", cut.Markup);
    }

    // R8 (Seite), A16
    [Fact]
    public void Ending_a_contract_asks_once_and_then_moves_it_out_of_the_list_of_running_ones()
    {
        var gym = FakeCommitmentApi.Commitment("Fitness");
        var cut = OpenPage(gym);
        cut.WaitForAssertion(() => Assert.True(Shows(cut, gym)));

        cut.Find($"[data-testid='commitment-end-{gym.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Vertrag beenden?", dialogs.Markup));
        Assert.Empty(CommitmentApiFake.Ended);
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Beenden").Click();

        cut.WaitForAssertion(() => Assert.False(Shows(cut, gym)));
        Assert.Equal([gym.Id], CommitmentApiFake.Ended);
        Assert.Contains("Der Vertrag ist beendet.", cut.Markup);
    }

    // A16
    [Fact]
    public void Cancelling_the_question_leaves_the_contract_running()
    {
        var gym = FakeCommitmentApi.Commitment("Fitness");
        var cut = OpenPage(gym);
        cut.WaitForAssertion(() => Assert.True(Shows(cut, gym)));

        cut.Find($"[data-testid='commitment-end-{gym.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Vertrag beenden?", dialogs.Markup));
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Abbrechen").Click();

        dialogs.WaitForAssertion(() => Assert.DoesNotContain("Vertrag beenden?", dialogs.Markup));
        Assert.Empty(CommitmentApiFake.Ended);
        Assert.True(Shows(cut, gym));
        Assert.NotEmpty(cut.FindAll($"[data-testid='commitment-end-{gym.Id}']"));
        Assert.DoesNotContain("Der Vertrag ist beendet.", cut.Markup);
    }

    // A15
    [Fact]
    public void A_savings_goal_fixes_the_pot_savings_and_shows_target_amount_and_date()
    {
        var cut = OpenPage();
        OpenNewDialog(cut);

        SelectIn(dialogs, "commitment-type", CommitmentTypes.SavingsGoal);

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='commitment-target-amount']")));
        Assert.NotEmpty(dialogs.FindAll("[data-testid='commitment-target-date']"));
        Assert.Empty(dialogs.FindAll("[data-testid='commitment-pot']"));
        var pot = Input(dialogs, "commitment-pot-fixed");
        Assert.Equal("Sparen", pot.GetAttribute("value"));
        Assert.True(pot.HasAttribute("readonly"));
    }

    // A15
    [Fact]
    public void A_contract_lets_the_pot_be_chosen_and_hides_the_target_fields()
    {
        var cut = OpenPage();
        OpenNewDialog(cut);

        var pot = dialogs.Find("[data-testid='commitment-pot']");

        Assert.Contains("Grundbedarf", pot.InnerHtml);
        Assert.Contains("Wünsche", pot.InnerHtml);
        Assert.DoesNotContain("Sparen", pot.InnerHtml);
        Assert.Empty(dialogs.FindAll("[data-testid='commitment-pot-fixed']"));
        Assert.Empty(dialogs.FindAll("[data-testid='commitment-target-amount']"));
        Assert.Empty(dialogs.FindAll("[data-testid='commitment-target-date']"));
    }

    [Fact]
    public void An_income_offers_only_income_categories_and_no_target_account()
    {
        var cut = OpenPage();
        OpenNewDialog(cut);
        Assert.Contains("Lebensmittel", dialogs.Find("[data-testid='commitment-category']").InnerHtml);
        Assert.NotEmpty(dialogs.FindAll("[data-testid='commitment-counter-account']"));

        SelectIn(dialogs, "commitment-type", CommitmentTypes.Income);

        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindAll("[data-testid='commitment-counter-account']")));
        var categories = dialogs.Find("[data-testid='commitment-category']").InnerHtml;
        Assert.Contains("Gehalt", categories);
        Assert.DoesNotContain("Lebensmittel", categories);
    }

    [Fact]
    public void Saving_a_new_contract_sends_it_and_shows_it_in_the_list()
    {
        var cut = OpenPage();
        OpenNewDialog(cut);
        Input(dialogs, "commitment-name").Change("Miete");
        Input(dialogs, "commitment-amount").Change("890");
        SelectIn(dialogs, "commitment-category", FakeCategoryApi.Groceries.Id.ToString());
        SelectIn(dialogs, "commitment-payment-method", PaymentMethods.StandingOrder);

        dialogs.Find("[data-testid='commitment-save']").Click();

        cut.WaitForAssertion(() => Assert.Single(CommitmentApiFake.Created));
        var request = CommitmentApiFake.Created[0];
        Assert.Equal(CommitmentTypes.Contract, request.Type);
        Assert.Equal("Miete", request.Name);
        Assert.Equal(890m, request.Amount);
        Assert.Equal(1, request.IntervalMonths);
        Assert.Equal(Today, request.FirstDueDate);
        Assert.Equal(PotKeys.Needs, request.Pot);
        Assert.Null(request.AccountId);
        Assert.Equal(PaymentMethods.StandingOrder, request.PaymentMethod);
        dialogs.WaitForAssertion(() => Assert.DoesNotContain("Neuer Vertrag", dialogs.Markup));
        cut.WaitForAssertion(() => Assert.Contains("Miete", cut.Markup));
    }

    [Fact]
    public void An_error_of_the_api_stays_in_the_dialog()
    {
        CommitmentApiFake.Error = ErrorCodes.PaymentMethodInvalid;
        var cut = OpenPage();
        OpenNewDialog(cut);
        SelectIn(dialogs, "commitment-category", FakeCategoryApi.Groceries.Id.ToString());

        dialogs.Find("[data-testid='commitment-save']").Click();

        dialogs.WaitForAssertion(() => Assert.Contains("Bitte wähle, wie gezahlt wird.", dialogs.Find("[data-testid='commitment-error']").TextContent));
        Assert.Contains("Neuer Vertrag", dialogs.Markup);
    }

    [Fact]
    public void Without_a_category_the_dialog_asks_for_one_before_sending()
    {
        var cut = OpenPage();
        OpenNewDialog(cut);

        dialogs.Find("[data-testid='commitment-save']").Click();

        dialogs.WaitForAssertion(() => Assert.Contains("Bitte wähle eine Kategorie.", dialogs.Find("[data-testid='commitment-error']").TextContent));
        Assert.Empty(CommitmentApiFake.Created);
    }

    [Fact]
    public void Editing_opens_the_saved_values_and_sends_a_change()
    {
        var rent = FakeCommitmentApi.Commitment("Miete");
        var cut = OpenPage(rent);
        cut.WaitForAssertion(() => Assert.True(Shows(cut, rent)));

        cut.Find($"[data-testid='commitment-edit-{rent.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Vertrag ändern", dialogs.Markup));
        Assert.Equal("Miete", Input(dialogs, "commitment-name").GetAttribute("value"));
        Input(dialogs, "commitment-name").Change("Warmmiete");
        dialogs.Find("[data-testid='commitment-save']").Click();

        cut.WaitForAssertion(() => Assert.Single(CommitmentApiFake.Updated));
        Assert.Equal((rent.Id, "Warmmiete"), (CommitmentApiFake.Updated[0].Id, CommitmentApiFake.Updated[0].Request.Name));
        cut.WaitForAssertion(() => Assert.Contains("Warmmiete", Row(cut, rent).TextContent));
    }
}
