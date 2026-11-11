using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Layout;
using Duofy.Shared.Pages;
using Duofy.Shared.Pages.Month;
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
            FakeHouseholdApi.Anna(isMe: false) with { GrantsToMe = new GrantLevels("delete", "delete", "delete", "delete", "delete") },
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
        Assert.Equal(Anna, PlanApiFake.Owners.Last());
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
        Assert.Equal("Wessen Zahlen: Ich", trigger.GetAttribute("aria-label"));

        trigger.Click();
        menu.WaitForAssertion(() => Assert.NotEmpty(menu.FindAll($"[data-testid='member-choice-{Anna}']")));
        Assert.Empty(menu.FindAll("[data-testid='member-choice-household']"));
        menu.Find($"[data-testid='member-choice-{Anna}']").Click();

        Assert.Equal(Anna, Member.Owner);
        chip.WaitForAssertion(() => Assert.Equal("Ansicht: Anna", chip.Find("[data-testid='shell-view']").TextContent.Trim()));
        Assert.Equal("Ansicht: Anna", chip.Find("[data-testid='shell-view']").GetAttribute("aria-label"));
    }

    // Spec 126, S1: In der Sicht auf Anna sprechen die Seiten von Anna, nicht von "dir".
    [Fact]
    public async Task The_pages_speak_about_the_chosen_person()
    {
        await Member.LoadAsync();
        Member.Choose(Anna);

        var overview = Render<PlanOverview>();
        overview.WaitForAssertion(() => Assert.Contains("Planung von Anna", overview.Markup));
        Assert.Contains("Monatspläne von Anna", overview.Markup);
        Assert.DoesNotContain("Meine Planung", overview.Markup);
        Assert.DoesNotContain("Deine Monatspläne", overview.Markup);

        AccountApiFake.Accounts.Clear();
        var accounts = Render<Accounts>();
        accounts.WaitForAssertion(() => Assert.Contains("Wo das Geld von Anna liegt", accounts.Markup));
        Assert.Contains("Anna hat noch kein Konto.", accounts.Markup);
        Assert.DoesNotContain("dein", accounts.Markup);
    }

    // Spec 126, S1: Auch Verträge, Sparen, der Plan und die geänderten Verträge sprechen von der gewählten Person.
    [Fact]
    public async Task Contracts_savings_and_plan_speak_about_the_chosen_person()
    {
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10);
        await Member.LoadAsync();
        Member.Choose(Anna);

        var commitments = Render<Commitments>();
        commitments.WaitForAssertion(() => Assert.Contains("Anna hat noch keinen Vertrag.", commitments.Markup));

        var savings = Render<Savings>();
        savings.WaitForAssertion(() => Assert.Contains("Anna hat noch kein Sparziel.", savings.Markup));
        Assert.Contains("Anna hat keine Forderung eingetragen.", savings.Find("[data-testid='savings-empty-debts']").TextContent);
        Assert.DoesNotContain("Keine Forderung eingetragen", savings.Markup);

        var plan = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        plan.WaitForAssertion(() => Assert.Contains("Der Plan von Anna.", plan.Markup));

        var change = new PlanChangeResponse(PlanChangeKinds.Changed, Guid.NewGuid(), Guid.NewGuid(), "Miete", [], false);
        var card = Render<PlanChangesCard>(p => p
            .Add(x => x.Changes, [change])
            .Add(x => x.OnApply, EventCallback.Factory.Create<PlanChangeResponse?>(this, _ => { }))
            .Add(x => x.FieldText, (_, value) => value ?? ""));
        Assert.Contains("Die Verträge von Anna haben sich geändert", card.Markup);
    }

    // Spec 126, S2: Wo Anna einen Bereich nicht teilt, nennt der Hinweis Person und Bereich.
    [Fact]
    public async Task The_not_shared_hints_name_the_person_and_the_area()
    {
        HouseholdApiFake.Household = HouseholdApiFake.Household! with
        {
            Members = [FakeHouseholdApi.Anna(isMe: false) with { GrantsToMe = GrantLevels.None with { Accounts = AccessLevelKeys.View } }, FakeHouseholdApi.Ben(isMe: true)],
        };
        await Member.LoadAsync();
        Member.Choose(Anna);

        var plan = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        plan.WaitForAssertion(() => Assert.Contains("Anna teilt Planung nicht mit dir.", plan.Find("[data-testid='not-shared']").TextContent));

        var overview = Render<PlanOverview>();
        Assert.Contains("Anna teilt Planung nicht mit dir.", overview.Find("[data-testid='not-shared']").TextContent);

        var savings = Render<Savings>();
        Assert.Contains("Anna teilt Sparen und Schulden nicht mit dir.", savings.Find("[data-testid='not-shared']").TextContent);
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
