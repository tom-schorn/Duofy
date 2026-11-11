using System.Reflection;
using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Die Seite /plan (Spec 66, A17 und die Bedienung zu R2 bis R5, R9, R10): der laufende Monat mit
/// Posten nach Topf, geplant und gebucht, Blättern, Abhaken, Hinweis auf geänderte Verträge,
/// einmalige Posten und Übertrag. "Heute" ist der 21. Oktober 2026.
/// </summary>
public class PlanPageTests : PageTestContext
{
    private readonly IRenderedComponent<IComponent> dialogs;

    public PlanPageTests()
    {
        dialogs = RenderDialogHost();
        Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true));
    }

    private IRenderedComponent<Plan> OpenPage(params PlanPositionResponse[] positions)
    {
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with { Positions = positions };
        var cut = Render<Plan>();
        OpenPlanTab(cut);
        return cut;
    }

    private static string Text(IRenderedComponent<Plan> cut, string testId) => cut.Find($"[data-testid='{testId}']").TextContent;

    [Fact]
    public void The_page_is_reachable_at_plan_year_month_and_needs_a_sign_in()
    {
        // Seit Spec 23 liegt der Monatsplan unter /plan/{Jahr}/{Monat}; /plan ist die Übersicht (PlanOverviewPageTests).
        Assert.Equal("/plan/{Year:int}/{Month:int}", typeof(Plan).GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.NotNull(typeof(Plan).GetCustomAttribute<AuthorizeAttribute>());
    }

    // Spec 118, R6: Der Plan zeigt, wie oft die Erinnerung verschoben wurde, und markiert den Posten nach dem dritten Mal als überfällig.
    [Fact]
    public void A_postponed_position_shows_its_count_and_after_the_third_time_is_marked_overdue()
    {
        var once = FakePlanApi.Position("Strom", 60m, day: 28) with { PostponeCount = 1 };
        var third = FakePlanApi.Position("Miete", 890m, day: 28) with { PostponeCount = 3 };
        var never = FakePlanApi.Position("Wasser", 20m, day: 28);
        var doneAfterThree = FakePlanApi.Position("Gas", 40m, day: 28, done: true) with { PostponeCount = 3 };

        var cut = OpenPage(once, third, never, doneAfterThree);

        Assert.Contains("1× verschoben", Text(cut, $"plan-edit-{once.Id}"));
        Assert.DoesNotContain("überfällig", Text(cut, $"plan-edit-{once.Id}"));
        Assert.Contains("3× verschoben", Text(cut, $"plan-edit-{third.Id}"));
        Assert.Contains("überfällig", Text(cut, $"plan-edit-{third.Id}"));
        Assert.DoesNotContain("verschoben", Text(cut, $"plan-edit-{never.Id}"));
        Assert.DoesNotContain("überfällig", Text(cut, $"plan-edit-{doneAfterThree.Id}"));
    }

    // A17
    [Fact]
    public void It_opens_the_current_month_with_positions_grouped_by_pot_planned_and_booked()
    {
        var salary = FakePlanApi.Position("Gehalt", 2400m, PotKeys.Income, day: 28);
        var rent = FakePlanApi.Position("Miete", 890m, booked: 890m, done: true);
        var groceries = FakePlanApi.Position("Lebensmittel", 600m, booked: 250m, day: 5);
        var streaming = FakePlanApi.Position("Streaming", 12.99m, PotKeys.Wants);

        var cut = OpenPage(rent, groceries, streaming, salary);

        Assert.Equal((2026, 10), Assert.Single(PlanApiFake.GetCalls));
        Assert.Equal("Oktober 2026", Text(cut, "page-title").Trim());
        var groups = cut.FindAll("[data-testid^='plan-group-']").Select(g => g.TextContent.Trim()).ToList();
        Assert.Equal(["Einnahmen", "Grundbedarf", "Wünsche"], groups);
        Assert.Contains("600,00 €", Text(cut, $"plan-amounts-{groceries.Id}"));
        Assert.Contains("250,00 € gebucht", Text(cut, $"plan-amounts-{groceries.Id}"));
        // Gebucht wie geplant: Der Betrag steht nur einmal da.
        Assert.Contains("890,00 €", Text(cut, $"plan-amounts-{rent.Id}"));
        Assert.DoesNotContain("gebucht", Text(cut, $"plan-amounts-{rent.Id}"));
        Assert.Contains("5.", Text(cut, $"plan-position-{groceries.Id}"));
        Assert.Empty(cut.FindAll("[data-testid='plan-note']"));

    }

    // A17
    [Fact]
    public void Paging_shows_the_next_and_the_previous_month()
    {
        var cut = OpenPage();

        // Spec 82, R15: Der Monat wechselt über die Adresse.
        cut.Render(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 11));
        cut.WaitForAssertion(() => Assert.Equal("November 2026", Text(cut, "page-title").Trim()));
        cut.Render(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        cut.Render(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 9));

        cut.WaitForAssertion(() => Assert.Equal("September 2026", Text(cut, "page-title").Trim()));
        Assert.Equal([(2026, 10), (2026, 11), (2026, 10), (2026, 9)], PlanApiFake.GetCalls);
    }

    // Spec 82: Hat ein Posten schon Buchungen, entsteht keine weitere; das Kästchen setzt ihn ohne Dialog auf erledigt.
    [Fact]
    public void Ticking_a_position_with_bookings_creates_no_further_booking()
    {
        var groceries = FakePlanApi.Position("Lebensmittel", 600m, booked: 550m);
        var cut = OpenPage(groceries);

        cut.Find($"[data-testid='plan-tick-{groceries.Id}']").Click();

        cut.WaitForAssertion(() => Assert.Equal(0m, Assert.Single(PlanApiFake.Ticks).Request.Amount));
        Assert.Empty(dialogs.FindAll("[data-testid='tick-amount']"));
    }

    [Fact]
    public void Removing_the_tick_unticks_the_position()
    {
        var rent = FakePlanApi.Position("Miete", 890m, booked: 890m, done: true);
        var cut = OpenPage(rent);

        cut.Find($"[data-testid='plan-tick-{rent.Id}']").Click();

        cut.WaitForAssertion(() => Assert.Equal(rent.Id, Assert.Single(PlanApiFake.Unticks)));
    }

    [Fact]
    public void Only_positions_without_bookings_offer_delete()
    {
        var rent = FakePlanApi.Position("Miete", 890m, booked: 890m);
        var gift = FakePlanApi.Position("Geschenk", 50m, PotKeys.Wants);
        var cut = OpenPage(rent, gift);

        cut.Find($"[data-testid='plan-edit-{rent.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='position-save']")));
        Assert.Empty(dialogs.FindAll("[data-testid='plan-delete']"));
        dialogs.Find("[data-testid='step-dialog-cancel']").Click();
        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindAll("[data-testid='position-save']")));

        OpenDelete(cut, gift);
        dialogs.WaitForAssertion(() => Assert.Contains("Posten löschen?", dialogs.Markup));
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Löschen").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll($"[data-testid='plan-position-{gift.Id}']")));
        Assert.Equal(gift.Id, Assert.Single(PlanApiFake.DeletedPositions));
    }

    // N7: Wer die Seite verlässt, während die Anfrage läuft, löst danach nichts mehr aus: kein Neuladen, keine Ausnahme.
    [Fact]
    public async Task Leaving_the_page_while_a_request_runs_does_not_reload_or_throw_afterwards()
    {
        var gift = FakePlanApi.Position("Geschenk", 50m, PotKeys.Wants);
        var cut = OpenPage(gift);
        var gate = new TaskCompletionSource();
        PlanApiFake.DeleteGate = gate.Task;
        OpenDelete(cut, gift);
        dialogs.WaitForAssertion(() => Assert.Contains("Posten löschen?", dialogs.Markup));
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Löschen").Click();
        var loadsBefore = PlanApiFake.GetCalls.Count;

        ((IDisposable)cut.Instance).Dispose();
        gate.SetResult();
        await Task.Delay(100);

        Assert.Equal(loadsBefore, PlanApiFake.GetCalls.Count);
    }

    // Löschen steht im Dialog des Postens (Spec 82); der fragt dann noch einmal nach.
    private void OpenDelete(IRenderedComponent<Plan> cut, PlanPositionResponse position)
    {
        cut.Find($"[data-testid='plan-edit-{position.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='plan-delete']")));
        dialogs.Find("[data-testid='plan-delete']").Click();
    }

    // Mangel S1: Löschen und Entfernen lassen sich nicht zurücknehmen, deshalb fragt die Seite erst nach.
    [Fact]
    public void Deleting_a_position_asks_first_and_keeps_it_when_cancelled()
    {
        var gift = FakePlanApi.Position("Geschenk", 50m, PotKeys.Wants);
        var cut = OpenPage(gift);

        OpenDelete(cut, gift);

        dialogs.WaitForAssertion(() => Assert.Contains("Posten löschen?", dialogs.Markup));
        Assert.Empty(PlanApiFake.DeletedPositions);
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Abbrechen").Click();
        dialogs.WaitForAssertion(() => Assert.DoesNotContain("Posten löschen?", dialogs.Markup));
        Assert.Empty(PlanApiFake.DeletedPositions);
        Assert.NotEmpty(cut.FindAll($"[data-testid='plan-position-{gift.Id}']"));
    }

    [Fact]
    public void Removing_the_carry_over_asks_first_and_removes_it_only_after_confirming()
    {
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with { CarryOver = new PlanCarryOverResponse(230m, true, 230m, []) };
        var cut = Render<Plan>();
        OpenPlanTab(cut);

        cut.WaitForAssertion(() => cut.Find("[data-testid='plan-carry-over-remove']").Click());

        dialogs.WaitForAssertion(() => Assert.Contains("Übertrag entfernen?", dialogs.Markup));
        Assert.Empty(PlanApiFake.DeletedCarryOvers);
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Entfernen").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(PlanApiFake.DeletedCarryOvers));
    }

    [Fact]
    public void Changed_contracts_are_shown_with_before_and_after_and_can_be_applied_all_or_one()
    {
        var rent = FakePlanApi.Position("Miete", 890m);
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with
        {
            Positions = [rent],
            Changes = [new PlanChangeResponse(PlanChangeKinds.Changed, rent.Id, rent.CommitmentId!.Value, "Miete",
                [new PlanFieldChangeResponse(PlanFields.Amount, "890.00", "950.00")], Protected: true)],
        };
        var cut = Render<Plan>();
        OpenPlanTab(cut);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='plan-changes']")));

        Assert.Contains("Miete: geändert", Text(cut, "plan-changes"));
        Assert.Contains("Betrag: 890,00 € → 950,00 €", Text(cut, "plan-changes"));
        Assert.Contains("von Hand geändert", Text(cut, "plan-changes"));

        cut.Find("[data-testid='plan-change-apply']").Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='plan-changes']")));
        Assert.Equal(rent.Id, Assert.Single(Assert.Single(PlanApiFake.AppliedChanges).Items!).PositionId);
    }

    [Fact]
    public void Applying_all_changes_sends_no_list()
    {
        var gymId = Guid.NewGuid();
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with
        {
            Changes = [new PlanChangeResponse(PlanChangeKinds.New, null, gymId, "Fitness", [], Protected: false)],
        };
        var cut = Render<Plan>();
        OpenPlanTab(cut);
        cut.WaitForAssertion(() => Assert.Contains("Fitness: neu in diesem Monat", Text(cut, "plan-changes")));

        cut.Find("[data-testid='plan-changes-apply-all']").Click();

        cut.WaitForAssertion(() => Assert.Null(Assert.Single(PlanApiFake.AppliedChanges).Items));
    }

    [Fact]
    public void The_add_button_creates_a_one_off_position()
    {
        var cut = OpenPage();

        cut.Find("[data-testid='plan-add']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='position-label']")));
        Field(dialogs, "position-label").Change("Geschenk");
        Field(dialogs, "position-amount").Change("50,00");
        Field(dialogs, "position-day").Change("20");
        dialogs.Find("[data-testid='position-category']").Change(FakeCategoryApi.Restaurant.Id.ToString());
        dialogs.Find("[data-testid='position-save']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Geschenk", cut.Markup));
        var request = Assert.Single(PlanApiFake.CreatedPositions);
        Assert.Equal(("Geschenk", 50m, 20, FakeCategoryApi.Restaurant.Id, PotKeys.Wants),
            (request.Label, request.Amount, request.DueDay, request.CategoryId, request.Pot));
        Assert.Contains("einmalig", cut.Markup);
    }

    private IRenderedComponent<Plan> OpenCarryOver(Guid giro)
    {
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with
        {
            CarryOver = new PlanCarryOverResponse(230m, false, null, [new PlanAccountBalanceResponse(giro, 1230m)]),
        };
        var cut = Render<Plan>();
        OpenPlanTab(cut);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='plan-carry-over-open']")));
        cut.Find("[data-testid='plan-carry-over-open']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='carry-over-amount']")));
        return cut;
    }

    [Fact]
    public void A_real_balance_equal_to_the_computed_one_books_without_asking()
    {
        var giro = Accounts.Accounts[0].Id;
        var cut = OpenCarryOver(giro);

        Field(dialogs, $"carry-over-balance-{giro}").Change("1230,00");
        dialogs.Find("[data-testid='carry-over-save']").Click();

        cut.WaitForAssertion(() => Assert.Single(PlanApiFake.CarryOvers));
        Assert.Empty(dialogs.FindAll("[data-testid='carry-over-differences']"));
    }

    [Fact]
    public void A_different_real_balance_shows_the_difference_and_backfilling_closes_without_booking()
    {
        var giro = Accounts.Accounts[0].Id;
        var cut = OpenCarryOver(giro);

        Field(dialogs, $"carry-over-balance-{giro}").Change("1220,00");
        dialogs.Find("[data-testid='carry-over-save']").Click();

        dialogs.WaitForAssertion(() => Assert.Equal(
            "Girokonto: gerechnet 1.230,00 €, eingetragen 1.220,00 €, Unterschied -10,00 €",
            dialogs.Find($"[data-testid='carry-over-difference-{giro}']").TextContent.Trim()));
        Assert.Empty(PlanApiFake.CarryOvers);
        Assert.Empty(dialogs.FindAll("[data-testid='carry-over-save']"));

        dialogs.Find("[data-testid='carry-over-backfill']").Click();

        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindAll("[data-testid='carry-over-amount']")));
        Assert.Empty(PlanApiFake.CarryOvers);
    }

    // Die Schrift in der Warnung ist die der Meldung; eine eigene Schriftfarbe hätte auf der Warnfläche zu wenig Kontrast.
    [Fact]
    public void The_difference_warning_keeps_the_text_colour_of_the_alert()
    {
        var giro = Accounts.Accounts[0].Id;
        OpenCarryOver(giro);
        Field(dialogs, $"carry-over-balance-{giro}").Change("1220,00");
        dialogs.Find("[data-testid='carry-over-save']").Click();

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='carry-over-differences']")));
        Assert.Empty(dialogs.Find("[data-testid='carry-over-differences']").QuerySelectorAll("p"));
    }

    [Fact]
    public void Correcting_the_real_balance_brings_the_normal_dialog_back()
    {
        var giro = Accounts.Accounts[0].Id;
        OpenCarryOver(giro);
        Field(dialogs, $"carry-over-balance-{giro}").Change("1220,00");
        dialogs.Find("[data-testid='carry-over-save']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='carry-over-differences']")));

        Field(dialogs, $"carry-over-balance-{giro}").Change("1230,00");

        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindAll("[data-testid='carry-over-differences']")));
        Assert.NotEmpty(dialogs.FindAll("[data-testid='carry-over-save']"));
        Assert.Empty(PlanApiFake.CarryOvers);
    }

    [Fact]
    public void The_carry_over_dialog_suggests_the_rest_and_sends_real_balances_when_the_difference_is_adopted()
    {
        var giro = Accounts.Accounts[0].Id;
        var cut = OpenCarryOver(giro);

        Assert.Equal("230,00", Field(dialogs, "carry-over-amount").GetAttribute("value"));
        Assert.Contains("Gerechnet: 1.230,00 €", dialogs.Markup);
        Field(dialogs, $"carry-over-balance-{giro}").Change("1220,00");
        dialogs.Find("[data-testid='carry-over-save']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='carry-over-adopt']")));
        dialogs.Find("[data-testid='carry-over-adopt']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Übertrag aus dem Vormonat: 230,00 €", Text(cut, "plan-carry-over")));
        var request = Assert.Single(PlanApiFake.CarryOvers);
        Assert.Equal(230m, request.Amount);
        Assert.Equal(new RealBalance(giro, 1220m), Assert.Single(request.Balances!));
    }

    // A20
    [Fact]
    public void The_carry_over_dialog_takes_a_negative_rest()
    {
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with
        {
            CarryOver = new PlanCarryOverResponse(-150m, false, null, []),
        };
        var cut = Render<Plan>();
        OpenPlanTab(cut);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='plan-carry-over-open']")));

        cut.Find("[data-testid='plan-carry-over-open']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='carry-over-amount']")));
        Assert.Equal("-150,00", Field(dialogs, "carry-over-amount").GetAttribute("value"));
        Field(dialogs, "carry-over-amount").Change("-80,00");
        dialogs.Find("[data-testid='carry-over-save']").Click();

        cut.WaitForAssertion(() => Assert.Single(PlanApiFake.CarryOvers));
        Assert.Equal(-80m, PlanApiFake.CarryOvers[0].Amount);
    }
}

/// <summary>
/// Der Übertrag in der App: Dort sitzt Speichern in der Kopfzeile. Bei einer Abweichung verschwindet es, damit nur die
/// beiden Wege bleiben.
/// </summary>
public class PlanPageInAppTests : PageTestContext
{
    private readonly IRenderedComponent<IComponent> dialogs;

    public PlanPageInAppTests()
    {
        Services.AddSingleton<IAppHost, AppHost>();
        dialogs = RenderDialogHost();
        Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true));
    }

    [Fact]
    public void A_difference_replaces_the_save_button_of_the_header_with_the_two_ways()
    {
        var giro = Accounts.Accounts[0].Id;
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with
        {
            CarryOver = new PlanCarryOverResponse(230m, false, null, [new PlanAccountBalanceResponse(giro, 1230m)]),
        };
        var cut = Render<Plan>();
        OpenPlanTab(cut);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='plan-carry-over-open']")));
        cut.Find("[data-testid='plan-carry-over-open']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='carry-over-amount']")));

        Field(dialogs, $"carry-over-balance-{giro}").Change("1250,00");
        dialogs.Find("[data-testid='carry-over-save']").Click();

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='carry-over-adopt']")));
        Assert.Empty(dialogs.FindAll("[data-testid='carry-over-save']"));
        Assert.Contains("Unterschied +20,00 €", dialogs.Find($"[data-testid='carry-over-difference-{giro}']").TextContent);
        Assert.NotEmpty(dialogs.FindAll("[data-testid='carry-over-backfill']"));

        dialogs.Find("[data-testid='carry-over-adopt']").Click();

        cut.WaitForAssertion(() => Assert.Equal(new RealBalance(giro, 1250m), Assert.Single(Assert.Single(PlanApiFake.CarryOvers).Balances!)));
    }
}
