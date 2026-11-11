using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Layout;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 126: Die Wahl einer Person gilt sofort auf jeder Seite. Der Dienst <see cref="ActiveMember"/> ist der eine
/// gemeinsame Zustand; jede Seite holt beim Wechsel ihre Daten neu, ohne dass jemand die Seite wechselt. Angemeldet ist
/// hier Ben im Haushalt von Anna, "heute" ist der 21. Oktober 2026.
/// </summary>
public class PersonSwitchTests : PageTestContext
{
    private static readonly Guid Anna = FakeHouseholdApi.AnnaId;

    private readonly IRenderedComponent<IComponent> menu;

    public PersonSwitchTests()
    {
        menu = RenderDialogHost();
        Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true));
        HouseholdApiFake.Household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
        [
            FakeHouseholdApi.Anna(isMe: false) with { GrantsToMe = new GrantLevels("delete", "delete", "delete", "delete", "delete", "view") },
            FakeHouseholdApi.Ben(isMe: true),
        ]);
    }

    private ActiveMember Member => Services.GetRequiredService<ActiveMember>();

    // A2 (R2): Auf der Seite Konten zeigt die Wahl sofort Annas Konten.
    [Fact]
    public async Task A2_the_accounts_page_reloads_for_the_chosen_person_without_navigation()
    {
        await Member.LoadAsync();
        var cut = Render<Accounts>();
        cut.WaitForAssertion(() => Assert.Contains((Guid?)null, AccountApiFake.ListOwners));

        Member.Choose(Anna);

        cut.WaitForAssertion(() => Assert.Equal(Anna, AccountApiFake.ListOwners.Last()));
    }

    // A2 (R2): Dasselbe auf der Seite Verträge.
    [Fact]
    public async Task A2_the_contracts_page_reloads_for_the_chosen_person_without_navigation()
    {
        await Member.LoadAsync();
        var cut = Render<Commitments>();
        cut.WaitForAssertion(() => Assert.Contains((Guid?)null, CommitmentApiFake.ListOwners));

        Member.Choose(Anna);

        cut.WaitForAssertion(() => Assert.Equal(Anna, CommitmentApiFake.ListOwners.Last()));
    }

    // A2 (R2): Dasselbe in der Planübersicht und im Monat mit seinen Buchungen.
    [Fact]
    public async Task A2_the_plan_pages_reload_for_the_chosen_person_without_navigation()
    {
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10);
        await Member.LoadAsync();
        var overview = Render<PlanOverview>();
        var month = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        month.WaitForAssertion(() => Assert.NotEmpty(month.FindAll("[data-testid='month-tab-review']")));
        PlanApiFake.Owners.Clear();

        Member.Choose(Anna);

        overview.WaitForAssertion(() => Assert.Contains(Anna, PlanApiFake.Owners));
        Assert.All(PlanApiFake.Owners, owner => Assert.Equal(Anna, owner));
        Assert.True(PlanApiFake.Owners.Count >= 2, "Übersicht und Monat holen beide neu.");
    }

    // A4 (R4): Die Auswertung für Anna zeigt Annas Summen, dieselbe Komponente wie bei Ben.
    [Fact]
    public async Task A4_the_review_shows_the_totals_of_the_chosen_person()
    {
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with
        {
            Overview = new(3000m, 1800m, 1200m,
                [new PlanPotResponse(PotKeys.Needs, 1000m, 1200m), new PlanPotResponse(PotKeys.Wants, 300m, 200m), new PlanPotResponse(PotKeys.Savings, 500m, 600m)], []),
        };
        await Member.LoadAsync();
        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-tab-review']")));
        cut.Find("[data-testid='month-tab-review']").Click();
        cut.WaitForAssertion(() => Assert.Contains("2.000,00 €", cut.Find("[data-testid='month-review']").TextContent));

        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with
        {
            Overview = new(1500m, 500m, 1000m,
                [new PlanPotResponse(PotKeys.Needs, 400m, 150m), new PlanPotResponse(PotKeys.Wants, 100m, 50m), new PlanPotResponse(PotKeys.Savings, 0m, 0m)], []),
        };
        Member.Choose(Anna);

        cut.WaitForAssertion(() => Assert.Contains("200,00 €", cut.Find("[data-testid='month-review']").TextContent));
        Assert.DoesNotContain("2.000,00 €", cut.Find("[data-testid='month-review']").TextContent);
    }

    // A2 (R2): Auch am Handy, wo es kein Profil-Menü in der Seitenleiste gibt, steht die Auswahl in der oberen Leiste auf jeder Seite.
    [Fact]
    public async Task A2_the_top_bar_offers_the_switch_on_every_page_and_choosing_changes_the_view_at_once()
    {
        await Member.LoadAsync();
        var chip = Render<ViewChip>();
        var trigger = chip.Find("[data-testid='shell-view']");
        Assert.Equal("Ich", trigger.TextContent.Trim());

        trigger.Click();
        menu.WaitForAssertion(() => Assert.NotEmpty(menu.FindAll($"[data-testid='member-choice-{Anna}']")));
        Assert.Empty(menu.FindAll("[data-testid='member-choice-household']"));
        menu.Find($"[data-testid='member-choice-{Anna}']").Click();

        Assert.Equal(Anna, Member.Owner);
        chip.WaitForAssertion(() => Assert.Equal("Ansicht: Anna", chip.Find("[data-testid='shell-view']").TextContent.Trim()));
    }

    // Ohne Freigabe gibt es nichts zu wählen: die Marke fehlt.
    [Fact]
    public async Task Without_any_sharing_person_the_top_bar_shows_no_switch()
    {
        HouseholdApiFake.Household = HouseholdApiFake.Household! with { Members = [FakeHouseholdApi.Anna(isMe: false), FakeHouseholdApi.Ben(isMe: true)] };
        await Member.LoadAsync();

        var chip = Render<ViewChip>();

        Assert.Empty(chip.FindAll("[data-testid='shell-view']"));
    }
}
