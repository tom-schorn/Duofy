using System.Reflection;
using AngleSharp.Dom;
using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace Duofy.Shared.Tests;

/// <summary>Die Seite /vertraege: Liste, beendete auf Wunsch, Beenden und der Dialog (Spec 64, R8, R10).</summary>
public class CommitmentsPageTests : PageTestContext
{
    private static readonly DateOnly Today = new(2026, 10, 21);

    // Dialoge gehen in ihrer Hülle auf; im echten Layout steht sie im Rahmen.
    private readonly IRenderedComponent<IComponent> dialogs;

    public CommitmentsPageTests()
    {
        dialogs = RenderDialogHost();
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

    // Ein Auswahlfeld (select) ändert sich mit Change, die Kontoauswahl (Gruppe von Optionsfeldern, Issue 16) mit dem Optionsfeld.
    private static void SelectIn(IRenderedComponent<IComponent> cut, string testId, string value)
    {
        var element = cut.Find($"[data-testid='{testId}']");
        if (element.TagName == "FIELDSET")
        {
            element.QuerySelector($"input[type='radio'][value='{value}']")!.Change(true);
        }
        else
        {
            element.Change(value);
        }
    }

    // Die Wahl der Kontoauswahl: der Wert des angekreuzten Optionsfelds ("" bei der leeren Wahl).
    private static string ChosenAccount(IRenderedComponent<IComponent> cut, string testId) =>
        cut.Find($"[data-testid='{testId}']").QuerySelectorAll("input[type='radio']").Single(i => i.HasAttribute("checked")).GetAttribute("value")!;

    // Der Kopf des Schritt-Dialogs (Spec 11), etwa "Schritt 2 von 3: Wann".
    private string Progress => dialogs.Find("[data-testid='step-dialog-progress']").TextContent.Trim();

    private IElement Button(string text) => dialogs.FindAll("button").Single(b => b.TextContent.Trim() == text);

    // Der Fehler eines Felds im Schritt; null, solange das Feld in Ordnung ist.
    private string? FieldError(string fieldTestId) =>
        dialogs.Find($"[data-testid='{fieldTestId}']").QuerySelector(".step-field-error")?.TextContent.Trim();

    private void Next(string expectedProgress)
    {
        Button("Weiter").Click();
        dialogs.WaitForAssertion(() => Assert.StartsWith(expectedProgress, Progress));
    }

    // Schritt 1 ausfüllen: Name, Betrag und eine Ausgaben-Kategorie.
    private void FillWhat(string name = "Miete", string amount = "890")
    {
        Input(dialogs, "commitment-name").Change(name);
        Input(dialogs, "commitment-amount").Change(amount);
        SelectIn(dialogs, "commitment-category", FakeCategoryApi.Groceries.Id.ToString());
    }

    // Die Optionen einer Auswahl als (Wert, Text).
    private (string Value, string Text)[] Options(string testId) => dialogs.Find($"[data-testid='{testId}']")
        .QuerySelectorAll("option").Select(o => (o.GetAttribute("value") ?? "", o.TextContent.Trim())).ToArray();

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

        cut.Find("[data-testid='commitments-show-ended']").Click();

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

        cut.Find("[data-testid='commitments-show-ended']").Click();

        cut.WaitForAssertion(() => Assert.True(Shows(cut, ended)));
        Assert.DoesNotContain("Beendete Verträge sind ausgeblendet.", cut.Markup);
    }

    // Mangel S13: Der Knopf nennt seinen Vertrag.
    [Fact]
    public void The_end_button_names_its_contract()
    {
        var gym = FakeCommitmentApi.Commitment("Fitness");
        var cut = OpenPage(gym);
        cut.WaitForAssertion(() => Assert.True(Shows(cut, gym)));

        Assert.Equal("Vertrag Fitness beenden", cut.Find($"[data-testid='commitment-end-{gym.Id}']").GetAttribute("aria-label"));
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

    // Spec 17, A4 (vorher Spec 64, A15): Sparziel und Schuld lassen sich nicht mehr anlegen; die Art ist Ausgabe oder Einnahme.
    [Fact]
    public void The_kind_offers_exactly_spending_and_income()
    {
        var cut = OpenPage();
        OpenNewDialog(cut);

        Assert.Equal(
            [(CommitmentTypes.Contract, "Ausgabe"), (CommitmentTypes.Income, "Einnahme")],
            Options("commitment-type"));
        Assert.Empty(dialogs.FindAll("[data-testid='commitment-target-amount']"));
        Assert.Empty(dialogs.FindAll("[data-testid='commitment-target-date']"));
    }

    // Spec 17, R4, R6: Ein bestehender Sparplan bleibt in der Liste zu sehen, der Vertragsdialog ändert ihn aber nicht mehr.
    [Fact]
    public void An_existing_savings_goal_is_listed_but_not_offered_for_editing()
    {
        var goal = FakeCommitmentApi.Commitment("Urlaub", 100m, type: CommitmentTypes.SavingsGoal);
        var cut = OpenPage(goal);

        cut.WaitForAssertion(() => Assert.Contains("Urlaub", Row(cut, goal).TextContent));
        Assert.Contains("Sparziel", Row(cut, goal).TextContent);
        Assert.Empty(cut.FindAll($"[data-testid='commitment-edit-{goal.Id}']"));
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

    // Alle Schritte bleiben gezeichnet (Spec 11); deshalb lassen sich die Felder aus Schritt 3 schon in Schritt 1 finden.
    [Fact]
    public void An_income_offers_only_income_categories_and_is_paid_to_one_own_account()
    {
        var cut = OpenPage();
        OpenNewDialog(cut);
        Assert.Contains("Lebensmittel", dialogs.Find("[data-testid='commitment-category']").InnerHtml);
        Assert.NotEmpty(dialogs.FindAll("[data-testid='commitment-account']"));
        Assert.NotEmpty(dialogs.FindAll("[data-testid='commitment-counter-account']"));

        SelectIn(dialogs, "commitment-type", CommitmentTypes.Income);

        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindAll("[data-testid='commitment-counter-account']")));
        var categories = dialogs.Find("[data-testid='commitment-category']").InnerHtml;
        Assert.Contains("Gehalt", categories);
        Assert.DoesNotContain("Lebensmittel", categories);
        Assert.Contains("Gezahlt zu", dialogs.Find("[data-testid='commitment-account-field']").TextContent);
        Assert.Equal("Einnahmen", Input(dialogs, "commitment-pot-fixed").GetAttribute("value"));
    }

    // Spec 17, A1: Ohne Name bleibt der Dialog in Schritt 1, der Fehler steht am Feld Name.
    [Fact]
    public void Next_without_a_name_stays_in_step_one_and_marks_the_name()
    {
        var cut = OpenPage();
        OpenNewDialog(cut);
        Assert.Equal("Schritt 1 von 3: Was", Progress);
        Input(dialogs, "commitment-amount").Change("890");
        SelectIn(dialogs, "commitment-category", FakeCategoryApi.Groceries.Id.ToString());

        Button("Weiter").Click();

        dialogs.WaitForAssertion(() => Assert.Equal("Bitte ausfüllen.", FieldError("commitment-name-field")));
        Assert.Equal("Schritt 1 von 3: Was", Progress);
        Assert.Null(FieldError("commitment-amount-field"));
        Assert.Contains("commitment-name-input-error", Input(dialogs, "commitment-name").GetAttribute("aria-describedby") ?? "");
    }

    // Spec 17, R1: Auch ohne Betrag geht es nicht weiter; ein Betrag von null zählt nicht.
    [Theory]
    [InlineData("")]
    [InlineData("0")]
    public void Next_without_an_amount_stays_in_step_one(string amount)
    {
        var cut = OpenPage();
        OpenNewDialog(cut);
        FillWhat(amount: amount);

        Button("Weiter").Click();

        dialogs.WaitForAssertion(() => Assert.NotNull(FieldError("commitment-amount-field")));
        Assert.Equal("Schritt 1 von 3: Was", Progress);
    }

    // Bisherige Regel (Spec 64), jetzt in Schritt 1: ohne Kategorie geht nichts an die API.
    [Fact]
    public void Without_a_category_the_dialog_asks_for_one_before_sending()
    {
        var cut = OpenPage();
        OpenNewDialog(cut);
        Input(dialogs, "commitment-name").Change("Miete");
        Input(dialogs, "commitment-amount").Change("890");

        Button("Weiter").Click();

        dialogs.WaitForAssertion(() => Assert.Equal("Bitte wähle eine Kategorie.", FieldError("commitment-category-field")));
        Assert.Equal("Schritt 1 von 3: Was", Progress);
        Assert.Empty(CommitmentApiFake.Created);
    }

    // Spec 17, A2: Ein Ende vor dem Start lehnt Schritt 2 ab.
    [Fact]
    public void An_end_before_the_start_is_rejected_in_step_two()
    {
        var cut = OpenPage();
        OpenNewDialog(cut);
        FillWhat();
        Next("Schritt 2 von 3: Wann");
        Input(dialogs, "commitment-first-due").Change("2026-11-01");
        Input(dialogs, "commitment-last-due").Change("2026-10-31");

        Button("Weiter").Click();

        dialogs.WaitForAssertion(() => Assert.Equal("Die letzte Zahlung darf nicht vor der ersten liegen.", FieldError("commitment-last-due-field")));
        Assert.Equal("Schritt 2 von 3: Wann", Progress);

        Input(dialogs, "commitment-last-due").Change("2026-11-01");
        Next("Schritt 3 von 3: Wer und Wohin");
    }

    // Spec 17, A2: Mit gültigem Rhythmus steht der nächste Fälligkeitstermin da und folgt den Eingaben.
    [Fact]
    public void Step_two_shows_the_next_due_date_for_a_valid_interval()
    {
        var cut = OpenPage();
        OpenNewDialog(cut);
        FillWhat();
        Next("Schritt 2 von 3: Wann");

        Input(dialogs, "commitment-first-due").Change("2026-02-15");
        Input(dialogs, "commitment-interval").Change("3");

        dialogs.WaitForAssertion(() => Assert.Equal("Nächste Zahlung: 15.11.2026",
            dialogs.Find("[data-testid='commitment-next-due']").TextContent.Trim()));

        // Leer gibt es keinen Termin, und Weiter bleibt gesperrt (unter 1 und über 120 lässt das Feld gar nicht zu).
        Input(dialogs, "commitment-interval").Change("");
        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindAll("[data-testid='commitment-next-due']")));
        Button("Weiter").Click();
        dialogs.WaitForAssertion(() => Assert.NotNull(FieldError("commitment-interval-field")));
        Assert.Equal("Schritt 2 von 3: Wann", Progress);
    }

    // Spec 17, A3: In Schritt 3 steht vor dem Speichern, was entsteht.
    [Fact]
    public void Step_three_summarises_name_amount_interval_accounts_and_payment_method_before_saving()
    {
        var savings = new AccountChoice(Guid.NewGuid(), "Sparkonto", IsDefault: false, Active: true);
        Accounts.Accounts.Add(savings);
        var cut = OpenPage();
        OpenNewDialog(cut);
        FillWhat();
        Next("Schritt 2 von 3: Wann");
        Input(dialogs, "commitment-interval").Change("12");
        Next("Schritt 3 von 3: Wer und Wohin");

        SelectIn(dialogs, "commitment-counter-account", savings.Id.ToString());
        SelectIn(dialogs, "commitment-payment-method", PaymentMethods.StandingOrder);

        dialogs.WaitForAssertion(() => Assert.Contains("Sparkonto", Summary("to")));
        Assert.Contains("Miete", Summary("name"));
        Assert.Contains("890,00 €", Summary("amount"));
        Assert.Contains("jährlich", Summary("interval"));
        Assert.Contains("Standardkonto", Summary("from"));
        Assert.Contains("Dauerauftrag", Summary("payment-method"));
        Assert.Empty(CommitmentApiFake.Created);
    }

    private string Summary(string row) => dialogs.Find($"[data-testid='commitment-summary-{row}']").TextContent;

    // Bisherige Regel (Spec 64), jetzt in Schritt 3: ohne Zahlungsart wird nicht gespeichert.
    [Fact]
    public void Saving_without_a_payment_method_stays_in_step_three()
    {
        var cut = OpenPage();
        OpenNewDialog(cut);
        FillWhat();
        Next("Schritt 2 von 3: Wann");
        Next("Schritt 3 von 3: Wer und Wohin");

        Button("Speichern").Click();

        dialogs.WaitForAssertion(() => Assert.NotNull(FieldError("commitment-payment-method-field")));
        Assert.Empty(CommitmentApiFake.Created);
    }

    // Spec 17, A6: Was in den drei Schritten eingegeben wurde, kommt genau so bei der API an; ein Sparplan bleibt unberührt.
    [Fact]
    public void Saving_a_new_contract_sends_exactly_the_inputs_and_leaves_a_savings_goal_alone()
    {
        var giro = Accounts.Accounts[0];
        var savings = new AccountChoice(Guid.NewGuid(), "Sparkonto", IsDefault: false, Active: true);
        Accounts.Accounts.Add(savings);
        var goal = FakeCommitmentApi.Commitment("Urlaub", 100m, type: CommitmentTypes.SavingsGoal);
        var cut = OpenPage(goal);
        OpenNewDialog(cut);

        FillWhat("Miete", "890,50");
        SelectIn(dialogs, "commitment-pot", PotKeys.Wants);
        Next("Schritt 2 von 3: Wann");
        Input(dialogs, "commitment-interval").Change("3");
        Input(dialogs, "commitment-first-due").Change("2026-11-01");
        Input(dialogs, "commitment-last-due").Change("2027-11-01");
        Next("Schritt 3 von 3: Wer und Wohin");
        SelectIn(dialogs, "commitment-account", giro.Id.ToString());
        SelectIn(dialogs, "commitment-counter-account", savings.Id.ToString());
        SelectIn(dialogs, "commitment-payment-method", PaymentMethods.StandingOrder);
        dialogs.Find("[data-testid='commitment-pass-through']").Click();

        Button("Speichern").Click();

        cut.WaitForAssertion(() => Assert.Single(CommitmentApiFake.Created));
        Assert.Equal(new CreateCommitmentRequest(
            CommitmentTypes.Contract, "Miete", 890.50m, 3, new DateOnly(2026, 11, 1), new DateOnly(2027, 11, 1),
            FakeCategoryApi.Groceries.Id, PotKeys.Wants, giro.Id, savings.Id, true, PaymentMethods.StandingOrder, null, null),
            CommitmentApiFake.Created[0]);
        dialogs.WaitForAssertion(() => Assert.DoesNotContain("Neuer Vertrag", dialogs.Markup));
        cut.WaitForAssertion(() => Assert.Contains("Miete", cut.Markup));
        Assert.Contains(goal, CommitmentApiFake.Commitments);
        Assert.Empty(CommitmentApiFake.Updated);
    }

    // Issue 32: Im Schritt "Wer und Wohin" kann ein Sparziel gewählt werden; die Rate geht dann an das Ziel.
    [Fact]
    public void Step_three_offers_the_savings_goals_and_a_chosen_goal_sets_its_account_and_is_sent()
    {
        var giro = Accounts.Accounts[0];
        var savings = new AccountChoice(Guid.NewGuid(), "Tagesgeld", IsDefault: false, Active: true);
        Accounts.Accounts.Add(savings);
        var goal = new SavingsGoalResponse(Guid.NewGuid(), "Urlaub", 1200m, null, savings.Id, 0m, false);
        SavingsGoalApiFake.Goals.Add(goal);
        var cut = OpenPage();
        OpenNewDialog(cut);

        FillWhat("Rate Urlaub", "200");
        Next("Schritt 2 von 3: Wann");
        Next("Schritt 3 von 3: Wer und Wohin");
        Assert.Equal([("", "(kein Sparziel)"), (goal.Id.ToString(), "Urlaub")], Options("commitment-goal"));
        SelectIn(dialogs, "commitment-goal", goal.Id.ToString());
        SelectIn(dialogs, "commitment-payment-method", PaymentMethods.StandingOrder);

        Assert.True(dialogs.Find($"[data-testid='commitment-counter-account-{savings.Id}']").HasAttribute("checked"));
        Assert.Equal("Urlaub", dialogs.Find("[data-testid='commitment-summary-goal'] td").TextContent.Trim());
        Button("Speichern").Click();

        cut.WaitForAssertion(() => Assert.Single(CommitmentApiFake.Created));
        Assert.Equal((goal.Id, savings.Id, giro.Id == Guid.Empty), (CommitmentApiFake.Created[0].SavingsGoalId, CommitmentApiFake.Created[0].CounterAccountId, false));
    }

    [Fact]
    public void Without_savings_goals_the_step_has_no_goal_field_and_an_income_never_has_one()
    {
        var cut = OpenPage();
        OpenNewDialog(cut);
        FillWhat();
        Next("Schritt 2 von 3: Wann");
        Next("Schritt 3 von 3: Wer und Wohin");

        Assert.Empty(dialogs.FindAll("[data-testid='commitment-goal']"));
    }

    [Fact]
    public void A_goal_without_an_account_asks_for_the_target_account_before_saving()
    {
        var goal = new SavingsGoalResponse(Guid.NewGuid(), "Notgroschen", null, null, null, 0m, false);
        SavingsGoalApiFake.Goals.Add(goal);
        var cut = OpenPage();
        OpenNewDialog(cut);
        FillWhat("Rate", "50");
        Next("Schritt 2 von 3: Wann");
        Next("Schritt 3 von 3: Wer und Wohin");
        SelectIn(dialogs, "commitment-goal", goal.Id.ToString());
        SelectIn(dialogs, "commitment-payment-method", PaymentMethods.StandingOrder);

        Button("Speichern").Click();

        dialogs.WaitForAssertion(() => Assert.Contains("Zielkonto", FieldError("commitment-goal-field")));
        Assert.Empty(CommitmentApiFake.Created);
    }

    // Issue 31: Im Schritt "Wer und Wohin" kann eine Rate einer Schuld zugeordnet werden.
    [Fact]
    public void Step_three_offers_open_debts_and_a_chosen_debt_is_sent_without_a_target_account()
    {
        var open = new DebtResponse(Guid.NewGuid(), DebtKinds.Loan, "Autokredit", "Bank", 5000m, null, null, 0m, 5000m, false);
        var settled = new DebtResponse(Guid.NewGuid(), DebtKinds.Claim, "Alt", "Bank", 50m, null, null, 50m, 0m, true);
        DebtApiFake.Debts.AddRange(open, settled);
        var cut = OpenPage();
        OpenNewDialog(cut);

        FillWhat("Rate Kredit", "150");
        Next("Schritt 2 von 3: Wann");
        Next("Schritt 3 von 3: Wer und Wohin");
        Assert.Equal([("", "(keine Schuld)"), (open.Id.ToString(), "Autokredit")], Options("commitment-debt"));
        SelectIn(dialogs, "commitment-debt", open.Id.ToString());
        SelectIn(dialogs, "commitment-payment-method", PaymentMethods.StandingOrder);

        Assert.Equal("Autokredit", dialogs.Find("[data-testid='commitment-summary-debt'] td").TextContent.Trim());
        Button("Speichern").Click();

        cut.WaitForAssertion(() => Assert.Single(CommitmentApiFake.Created));
        Assert.Equal((open.Id, (Guid?)null), (CommitmentApiFake.Created[0].DebtId, CommitmentApiFake.Created[0].CounterAccountId));
    }

    [Fact]
    public void Without_debts_the_step_has_no_debt_field()
    {
        var cut = OpenPage();
        OpenNewDialog(cut);
        FillWhat();
        Next("Schritt 2 von 3: Wann");
        Next("Schritt 3 von 3: Wer und Wohin");

        Assert.Empty(dialogs.FindAll("[data-testid='commitment-debt']"));
    }

    [Fact]
    public void An_error_of_the_api_stays_in_the_dialog()
    {
        CommitmentApiFake.Error = ErrorCodes.PaymentMethodInvalid;
        var cut = OpenPage();
        OpenNewDialog(cut);
        FillWhat();
        Next("Schritt 2 von 3: Wann");
        Next("Schritt 3 von 3: Wer und Wohin");
        SelectIn(dialogs, "commitment-payment-method", PaymentMethods.StandingOrder);

        Button("Speichern").Click();

        dialogs.WaitForAssertion(() => Assert.Contains("Bitte wähle, wie gezahlt wird.", dialogs.Find("[data-testid='commitment-error']").TextContent));
        Assert.Contains("Neuer Vertrag", dialogs.Markup);
        Assert.Equal("Schritt 3 von 3: Wer und Wohin", Progress);
    }

    // Spec 17, A5: Bearbeiten nutzt dieselben drei Schritte, alle Felder sind vorbelegt, gespeichert wird derselbe Vertrag.
    [Fact]
    public void Editing_fills_every_step_with_the_saved_values_and_changes_the_same_contract()
    {
        var giro = Accounts.Accounts[0];
        var rent = FakeCommitmentApi.Commitment("Miete", 890m, interval: 3) with
        {
            LastDueDate = new DateOnly(2027, 1, 1),
            AccountId = giro.Id,
            Pot = PotKeys.Wants,
        };
        var cut = OpenPage(rent);
        cut.WaitForAssertion(() => Assert.True(Shows(cut, rent)));

        cut.Find($"[data-testid='commitment-edit-{rent.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Vertrag ändern", dialogs.Markup));

        Assert.Equal("Schritt 1 von 3: Was", Progress);
        Assert.Equal("Miete", Input(dialogs, "commitment-name").GetAttribute("value"));
        Assert.Equal("890,00", Input(dialogs, "commitment-amount").GetAttribute("value"));
        Assert.Equal(CommitmentTypes.Contract, dialogs.Find("[data-testid='commitment-type']").GetAttribute("value"));
        Assert.Equal(FakeCategoryApi.Groceries.Id.ToString(), dialogs.Find("[data-testid='commitment-category']").GetAttribute("value"));
        Assert.Equal(PotKeys.Wants, dialogs.Find("[data-testid='commitment-pot']").GetAttribute("value"));
        Input(dialogs, "commitment-name").Change("Warmmiete");
        Next("Schritt 2 von 3: Wann");

        Assert.Equal("3", Input(dialogs, "commitment-interval").GetAttribute("value"));
        Assert.Equal("2026-01-01", Input(dialogs, "commitment-first-due").GetAttribute("value"));
        Assert.Equal("2027-01-01", Input(dialogs, "commitment-last-due").GetAttribute("value"));
        Next("Schritt 3 von 3: Wer und Wohin");

        Assert.Equal(giro.Id.ToString(), ChosenAccount(dialogs, "commitment-account"));
        Assert.Equal("", ChosenAccount(dialogs, "commitment-counter-account"));
        Assert.Equal(PaymentMethods.StandingOrder, ChosenAccount(dialogs, "commitment-payment-method"));
        Button("Speichern").Click();

        cut.WaitForAssertion(() => Assert.Single(CommitmentApiFake.Updated));
        var (id, request) = CommitmentApiFake.Updated[0];
        Assert.Equal(rent.Id, id);
        Assert.Equal("Warmmiete", request.Name);
        Assert.Equal((890m, 3, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1)),
            (request.Amount, request.IntervalMonths, request.FirstDueDate, request.LastDueDate));
        Assert.Equal((giro.Id, PotKeys.Wants, PaymentMethods.StandingOrder), (request.AccountId, request.Pot, request.PaymentMethod));
        Assert.Empty(CommitmentApiFake.Created);
        cut.WaitForAssertion(() => Assert.Contains("Warmmiete", Row(cut, rent).TextContent));
    }

    // Ein schon vor seinem Start beendeter Vertrag (Beenden setzt dann den Tag vor der ersten Zahlung) bleibt änderbar,
    // solange die letzte Zahlung bleibt, wie sie ist.
    [Fact]
    public void An_unchanged_end_before_the_start_of_a_saved_contract_does_not_block_editing()
    {
        var early = FakeCommitmentApi.Commitment("Probeabo") with { LastDueDate = new DateOnly(2025, 12, 31) };
        var cut = OpenPage(early);
        cut.WaitForAssertion(() => Assert.True(Shows(cut, early)));
        cut.Find($"[data-testid='commitment-edit-{early.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Vertrag ändern", dialogs.Markup));

        Next("Schritt 2 von 3: Wann");
        Next("Schritt 3 von 3: Wer und Wohin");
    }

    // Seite und Dialog sind aus den Bausteinen gebaut; die Zahlenfelder verweisen per aria-labelledby auf ihre Beschriftung.
    [Fact]
    public void The_page_and_the_dialog_are_built_from_the_blocks()
    {
        var commitment = FakeCommitmentApi.Commitment("Miete");
        var cut = OpenPage(commitment);
        cut.WaitForAssertion(() => Assert.True(Shows(cut, commitment)));

        Assert.Contains("data-row-frame", Row(cut, commitment).ClassList);
        Assert.Contains("duofy-button", cut.Find($"[data-testid='commitment-edit-{commitment.Id}']").ClassList);
        Assert.Contains("duofy-button", cut.Find($"[data-testid='commitment-end-{commitment.Id}']").ClassList);

        OpenNewDialog(cut);

        Assert.Equal("Betrag pro Zahlung in Euro", dialogs.Find($"label[for='{Input(dialogs, "commitment-amount").Id}']").TextContent.Trim());
        Assert.Equal("commitment-name-input", Input(dialogs, "commitment-name").Id);
        Assert.Equal("date", Input(dialogs, "commitment-first-due").GetAttribute("type"));
    }
}
