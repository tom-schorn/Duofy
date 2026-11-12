using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components;

namespace Duofy.Shared.Tests;

/// <summary>Einen Posten in den nächsten Monat verschieben, in der Planseite (Spec 136, R7, A8). "Heute" ist der 21. Oktober 2026.</summary>
public class PlanPostponePageTests : PageTestContext
{
    private readonly IRenderedComponent<IComponent> dialogs;

    public PlanPostponePageTests()
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

    private static string Row(IRenderedComponent<Plan> cut, Guid id) => cut.Find($"[data-testid='plan-position-{id}']").TextContent;

    [Fact]
    public void The_menu_entry_names_the_next_month_and_one_confirmation_moves_the_position()
    {
        var github = FakePlanApi.Position("GitHub", 4m, PotKeys.Wants);
        var cut = OpenPage(github);

        Assert.Contains("In den November verschieben", cut.Find($"[data-testid='plan-postpone-{github.Id}']").GetAttribute("aria-label"));
        cut.Find($"[data-testid='plan-postpone-{github.Id}']").Click();

        dialogs.WaitForAssertion(() => Assert.Contains("November", dialogs.Markup));
        Assert.Empty(PlanApiFake.Postponed);
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Verschieben").Click();
        cut.WaitForAssertion(() => Assert.Equal(github.Id, Assert.Single(PlanApiFake.Postponed)));
        cut.WaitForAssertion(() => Assert.Contains("in den November verschoben", Row(cut, github.Id)));
    }

    [Fact]
    public void Cancelling_the_confirmation_moves_nothing()
    {
        var github = FakePlanApi.Position("GitHub", 4m, PotKeys.Wants);
        var cut = OpenPage(github);

        cut.Find($"[data-testid='plan-postpone-{github.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("November", dialogs.Markup));
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Abbrechen").Click();

        dialogs.WaitForAssertion(() => Assert.DoesNotContain("November", dialogs.Markup));
        Assert.Empty(PlanApiFake.Postponed);
    }

    [Fact]
    public void A_moved_position_is_greyed_says_where_it_went_and_offers_undo_and_no_postponing_or_ticking()
    {
        var moved = FakePlanApi.Position("GitHub", 4m, PotKeys.Wants) with { MovedToMonth = new PlanMonthRef(2026, 11) };
        var cut = OpenPage(moved);

        var row = cut.Find($"[data-testid='plan-position-{moved.Id}']");
        Assert.Contains("row-moved", row.ClassName);
        Assert.Contains("in den November verschoben", row.TextContent);
        Assert.Empty(cut.FindAll($"[data-testid='plan-postpone-{moved.Id}']"));
        Assert.Empty(cut.FindAll($"[data-testid='plan-tick-{moved.Id}']"));

        cut.Find($"[data-testid='plan-postpone-undo-{moved.Id}']").Click();

        cut.WaitForAssertion(() => Assert.Equal(moved.Id, Assert.Single(PlanApiFake.PostponeUndone)));
        cut.WaitForAssertion(() => Assert.DoesNotContain("verschoben", Row(cut, moved.Id)));
    }

    [Fact]
    public void A_copy_names_its_origin_month()
    {
        var copy = FakePlanApi.Position("GitHub", 4m, PotKeys.Wants) with { MovedFromMonth = new PlanMonthRef(2026, 9) };
        var cut = OpenPage(copy);

        Assert.Contains("aus September", Row(cut, copy.Id));
    }

    [Fact]
    public void A_copy_does_not_show_the_changed_by_hand_hint_but_a_hand_edited_position_does()
    {
        var copy = FakePlanApi.Position("GitHub", 4m, PotKeys.Wants) with { MovedFromMonth = new PlanMonthRef(2026, 9), ChangedByHand = true };
        var edited = FakePlanApi.Position("Miete", 890m) with { ChangedByHand = true };
        var cut = OpenPage(copy, edited);

        Assert.DoesNotContain("von Hand geändert", Row(cut, copy.Id));
        Assert.Contains("von Hand geändert", Row(cut, edited.Id));
    }

    [Fact]
    public void A_moved_row_keeps_a_placeholder_where_the_checkbox_is()
    {
        var moved = FakePlanApi.Position("GitHub", 4m, PotKeys.Wants) with { MovedToMonth = new PlanMonthRef(2026, 11) };
        var cut = OpenPage(moved);

        Assert.Single(cut.Find($"[data-testid='plan-position-{moved.Id}']").QuerySelectorAll(".tick-placeholder"));
    }

    [Fact]
    public void Paid_positions_offer_no_postponing()
    {
        var ticked = FakePlanApi.Position("Miete", 890m, booked: 890m, done: true);
        var booked = FakePlanApi.Position("Strom", 80m, booked: 20m);
        var cut = OpenPage(ticked, booked);

        Assert.Empty(cut.FindAll($"[data-testid='plan-postpone-{ticked.Id}']"));
        Assert.Empty(cut.FindAll($"[data-testid='plan-postpone-{booked.Id}']"));
    }

    [Fact]
    public void The_moved_position_does_not_count_in_the_unpaid_total_of_the_page()
    {
        var moved = FakePlanApi.Position("GitHub", 4m, PotKeys.Wants) with { MovedToMonth = new PlanMonthRef(2026, 11) };
        var rent = FakePlanApi.Position("Miete", 890m);
        var cut = OpenPage(moved, rent);

        var unpaid = cut.Find("[data-testid='month-block-unpaid']").TextContent;
        Assert.Contains("890", unpaid);
        Assert.DoesNotContain("894", unpaid);
    }
}
