using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components;

namespace Duofy.Shared.Tests;

/// <summary>Planmonat und Posten je Zeile im Buchungsdialog (Spec 66, R6, R7). "Heute" ist der 21. Oktober 2026.</summary>
public class BookingDialogPlanTests : PageTestContext
{
    private readonly IRenderedComponent<IComponent> dialogs;

    public BookingDialogPlanTests()
    {
        dialogs = RenderRadzenHost();
        Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true));
    }

    private void OpenDialog()
    {
        OpenMonthAndNewBooking();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='booking-line-position-0']")));
    }

    // Spec 72, A6
    [Fact]
    public void The_pot_is_marked_as_required_starts_empty_and_follows_the_category()
    {
        OpenDialog();

        var pot = dialogs.Find("[data-testid='booking-line-pot-0']");
        Assert.Equal("", pot.GetAttribute("value"));
        Assert.Equal("true", pot.GetAttribute("aria-required"));
        Assert.Contains("(bitte wählen)", pot.TextContent);
        Assert.Contains("Topf (Pflicht)", dialogs.Markup);

        dialogs.Find("[data-testid='booking-line-category-0']").Change(FakeCategoryApi.Restaurant.Id.ToString());

        Assert.Equal(PotKeys.Wants, dialogs.Find("[data-testid='booking-line-pot-0']").GetAttribute("value"));
    }

    [Fact]
    public void An_income_offers_only_its_pot_and_nothing_to_choose()
    {
        OpenDialog();

        dialogs.Find("[data-testid='booking-kind-income']").Click();

        var pot = dialogs.Find("[data-testid='booking-line-pot-0']");
        Assert.Equal(PotKeys.Income, pot.GetAttribute("value"));
        Assert.DoesNotContain("(bitte wählen)", pot.TextContent);
    }

    [Fact]
    public void A_line_offers_the_positions_of_the_plan_month_also_ticked_ones_and_sends_the_chosen_one()
    {
        var groceries = FakePlanApi.Position("Lebensmittel", 600m);
        var rent = FakePlanApi.Position("Miete", 890m, booked: 890m, done: true);
        var salary = FakePlanApi.Position("Gehalt", 2400m, PotKeys.Income);
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with { Positions = [rent, groceries, salary] };
        OpenDialog();

        var options = dialogs.Find("[data-testid='booking-line-position-0']").TextContent;
        Assert.Contains("Lebensmittel", options);
        Assert.Contains("Miete (abgehakt)", options);
        Assert.DoesNotContain("Gehalt", options);

        Field(dialogs, "booking-amount").Change("42");
        dialogs.Find("[data-testid='booking-line-position-0']").Change(groceries.Id.ToString());
        dialogs.Find("[data-testid='booking-next']").Click();
        dialogs.Find("[data-testid='booking-save']").Click();

        dialogs.WaitForAssertion(() => Assert.Single(BookingApiFake.Created));
        var request = BookingApiFake.Created[0];
        Assert.Equal((2026, 10), (request.PlanYear, request.PlanMonth));
        var line = Assert.Single(request.Lines!);
        Assert.Equal(groceries.Id, line.PositionId);
        // Die Zeile hatte noch keine Kategorie; sie kommt vom Posten.
        Assert.Equal(groceries.CategoryId, line.CategoryId);
    }

    [Fact]
    public void Choosing_the_next_month_loads_its_positions_and_sends_it_as_plan_month()
    {
        var rentNovember = FakePlanApi.Position("Miete November", 890m);
        PlanApiFake.Plans[(2026, 11)] = FakePlanApi.Empty(2026, 11) with { Positions = [rentNovember] };
        OpenDialog();

        dialogs.Find("[data-testid='booking-plan-month']").Change("2026-11");

        dialogs.WaitForAssertion(() =>
            Assert.Contains("Miete November", dialogs.Find("[data-testid='booking-line-position-0']").TextContent));
        Assert.Contains((2026, 11), PlanApiFake.GetCalls);
        Field(dialogs, "booking-amount").Change("890");
        dialogs.Find("[data-testid='booking-line-category-0']").Change(FakeCategoryApi.Groceries.Id.ToString());
        dialogs.Find("[data-testid='booking-next']").Click();
        dialogs.Find("[data-testid='booking-save']").Click();

        dialogs.WaitForAssertion(() => Assert.Equal((2026, 11), (BookingApiFake.Created.Single().PlanYear, BookingApiFake.Created.Single().PlanMonth)));
    }

    // Spec 51, A9: Die Mahnkosten gehören zur GEZ, auch wenn sie schon abgehakt ist und die Kategorie eine andere ist.
    [Fact]
    public void A_second_booking_can_go_on_a_ticked_position_with_its_own_category()
    {
        var gez = FakePlanApi.Position("GEZ", 18.36m, booked: 18.36m, done: true);
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with { Positions = [gez] };
        OpenDialog();

        Field(dialogs, "booking-amount").Change("5");
        dialogs.Find("[data-testid='booking-line-category-0']").Change(FakeCategoryApi.Restaurant.Id.ToString());
        dialogs.Find("[data-testid='booking-line-position-0']").Change(gez.Id.ToString());
        dialogs.Find("[data-testid='booking-next']").Click();
        dialogs.Find("[data-testid='booking-save']").Click();

        dialogs.WaitForAssertion(() => Assert.Single(BookingApiFake.Created));
        var line = Assert.Single(BookingApiFake.Created[0].Lines!);
        Assert.Equal(gez.Id, line.PositionId);
        Assert.Equal(FakeCategoryApi.Restaurant.Id, line.CategoryId);
    }
}
