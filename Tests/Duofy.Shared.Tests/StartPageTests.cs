using Bunit;
using Duofy.ApiClient;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Duofy.Shared.Theme;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Die Startseite mit Begrüßung, Frei-Betrag, anstehenden Zahlungen und Soll-Ist (Spec 33, A1 bis A11).
/// "Heute" ist der 21. Oktober 2026.
/// </summary>
public class StartPageTests : PageTestContext
{
    private const string NotSignedInText = "Du bist nicht angemeldet";
    private const string NotShared = "Ben teilt Planung nicht mit dir.";

    private readonly IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> dialogs;

    private readonly TokenAuthenticationStateProvider signIn;

    public StartPageTests()
    {
        Services.AddSingleton<IThemePreferenceStore>(new NoThemeStore());
        Services.AddCascadingAuthenticationState();
        AddAuthorization().SetAuthorized("anna");
        // Wie in der App: Die Token-Ablage entscheidet, ob jemand angemeldet ist, und [Authorize] verlangt eine angemeldete Person.
        // bUnits Ersatz der Berechtigungsprüfung folgt dem Anmeldezustand nicht, deshalb steht hier die einfache Prüfung.
        signIn = new TokenAuthenticationStateProvider(Tokens);
        Services.AddSingleton<AuthenticationStateProvider>(signIn);
        Services.AddSingleton<IAuthorizationService>(new SignedInAuthorizationService());
        dialogs = RenderDialogHost();
        Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true));
    }

    private ActiveMember Member => Services.GetRequiredService<ActiveMember>();

    private static PlanOverviewResponse Overview(decimal free = 340m, IReadOnlyList<PlanCarryOverLineResponse>? carryOvers = null, IReadOnlyList<PlanPotResponse>? pots = null) =>
        new(3000m, 2660m, free,
            pots ?? [new PlanPotResponse(PotKeys.Needs, 1000m, 1200m), new PlanPotResponse(PotKeys.Wants, 0m, 0m), new PlanPotResponse(PotKeys.Savings, 0m, 0m)],
            [], carryOvers);

    private void SeedPlan(decimal free = 340m, IReadOnlyList<PlanCarryOverLineResponse>? carryOvers = null, params PlanPositionResponse[] positions) =>
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with { Positions = positions, Overview = Overview(free, carryOvers) };

    private static AccountResponse Account(string name, decimal balance, bool available = true) =>
        new(Guid.NewGuid(), name, AccountTypes.Checking, 0m, new DateOnly(2026, 1, 1), available, null, false, true, balance);

    private IRenderedComponent<Start> OpenStart()
    {
        var cut = Render<Start>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='start-hero']")));
        return cut;
    }

    private async Task ChooseBenAsync(GrantLevels levels)
    {
        HouseholdApiFake.Household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
        [
            FakeHouseholdApi.Anna(),
            FakeHouseholdApi.Ben(isMe: false) with { GrantsToMe = levels },
        ]);
        await Member.LoadAsync();
        Member.Choose(FakeHouseholdApi.BenId);
    }

    private static string Text(IRenderedComponent<Start> cut, string testId) => cut.Find($"[data-testid='{testId}']").TextContent;

    // A1 (R1)
    [Fact]
    public void The_hero_block_greets_by_name_and_shows_the_free_amount_of_the_current_month()
    {
        SeedPlan(free: 340m);

        var cut = OpenStart();

        var hero = Text(cut, "start-hero");
        Assert.Contains("Hallo, Anna!", hero);
        Assert.Contains("Verplanbar im Oktober", hero);
        Assert.Contains("340,00 €", hero);
        Assert.DoesNotContain("Hier entsteht bald", cut.Markup);
    }

    // A1 (R1): ein Frei-Betrag von 0 heißt "Alles verplant"
    [Fact]
    public void A_free_amount_of_zero_reads_all_planned()
    {
        SeedPlan(free: 0m);

        var cut = OpenStart();

        Assert.Contains("Alles verplant", Text(cut, "start-hero"));
    }

    // A2 (R1)
    [Fact]
    public void The_hero_block_names_a_carry_over_that_is_in_the_free_amount()
    {
        SeedPlan(free: 390m, carryOvers: [new PlanCarryOverLineResponse(CarryOverDirections.In, 2026, 9, 50m)]);

        var cut = OpenStart();

        Assert.Contains("Enthält Übertrag: 50,00 €", Text(cut, "start-hero"));
    }

    [Fact]
    public void Without_a_carry_over_the_hero_block_has_no_note()
    {
        SeedPlan();

        var cut = OpenStart();

        Assert.DoesNotContain("Übertrag", Text(cut, "start-hero"));
    }

    // Spec 97, R4: Ist die Anmeldung zu Ende, zeigt die Startseite keinen Fehler; der Router leitet zur Anmeldung.
    [Fact]
    public void An_ended_sign_in_shows_no_error_when_the_user_cannot_be_loaded()
    {
        Users.MeFailure = ClientErrorCodes.NotSignedIn;

        var cut = Render<Start>();

        Assert.Empty(cut.FindAll("[role='alert']"));
        Assert.DoesNotContain(NotSignedInText, cut.Markup);
    }

    [Fact]
    public void An_ended_sign_in_shows_no_error_when_the_plan_cannot_be_loaded()
    {
        PlanApiFake.Error = ClientErrorCodes.NotSignedIn;

        var cut = Render<Start>();

        Assert.Empty(cut.FindAll("[role='alert']"));
        Assert.DoesNotContain(NotSignedInText, cut.Markup);
    }

    // Spec 97, A7: Endet die Anmeldung, schaltet der Anmeldezustand um, und der echte Router führt von der
    // Startseite zur Anmeldeseite (RedirectToSignIn), ohne dass die Startseite einen Fehler zeigt.
    [Fact]
    public void An_ended_sign_in_leads_from_the_start_page_to_the_sign_in_page_through_the_router()
    {
        SeedPlan();
        Navigation.NavigateTo("/");
        var cut = Render<CascadingAuthenticationState>(p => p.AddChildContent<Routes>());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='start-hero']")));
        Assert.DoesNotContain("/anmelden", CurrentPath);

        Tokens.Tokens = null;
        signIn.NotifyTokensChanged();

        cut.WaitForAssertion(() => Assert.StartsWith("/anmelden", CurrentPath));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".auth-layout")));
        Assert.Empty(cut.FindAll("[data-testid='start-hero']"));
        Assert.Empty(cut.FindAll(AlertSelector));
        Assert.DoesNotContain(NotSignedInText, cut.Markup);
    }

    [Fact]
    public void Another_user_error_is_still_shown()
    {
        Users.MeFailure = ClientErrorCodes.Unexpected;

        var cut = Render<Start>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[role='alert']")));
    }

    // A3 (R2)
    [Fact]
    public void A_new_booking_goes_to_the_current_month()
    {
        SeedPlan();
        var cut = OpenStart();

        cut.Find("[data-testid='start-new-booking']").Closest("button")!.Click();

        Assert.EndsWith("/plan/2026/10?buchen=1", Navigation.Uri);
        Assert.Empty(dialogs.FindAll(".step-dialog"));
    }

    [Fact]
    public async Task New_booking_is_locked_when_the_person_may_not_book()
    {
        SeedPlan();
        await ChooseBenAsync(GrantLevels.None with { Plan = AccessLevelKeys.View });

        var cut = OpenStart();

        var button = cut.Find("[data-testid='start-new-booking']");
        Assert.Equal("true", button.GetAttribute("aria-disabled"));
        Assert.Equal(GrantLock.ReasonId, button.GetAttribute("aria-describedby"));
        Assert.Contains("Dafür hat dir Ben keine Freigabe gegeben.", cut.Markup);
    }

    // A4 (R3)
    [Fact]
    public void Without_a_household_a_hint_leads_to_the_household_page()
    {
        SeedPlan();
        HouseholdApiFake.Household = null;

        var cut = OpenStart();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='start-household-hint']")));
        Assert.Contains("Mit jemandem zusammen planen?", Text(cut, "start-household-hint"));
        Assert.Equal("/haushalt", cut.Find("[data-testid='start-household-hint-link']").GetAttribute("href"));
    }

    [Fact]
    public void With_a_household_there_is_no_hint()
    {
        SeedPlan();

        var cut = OpenStart();

        Assert.Empty(cut.FindAll("[data-testid='start-household-hint']"));
    }

    // A5 (R4)
    [Fact]
    public void Without_a_plan_for_the_month_the_empty_state_leads_to_the_plan_and_creates_nothing()
    {
        var cut = Render<Start>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='start-empty']")));
        Assert.Contains("Für Oktober gibt es noch keinen Plan", Text(cut, "start-empty"));
        Assert.Equal("/plan/2026/10", cut.Find("[data-testid='start-empty-link']").GetAttribute("href"));
        Assert.Contains("Hallo, Anna!", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid='start-free']"));
        Assert.Empty(cut.FindAll("[data-testid='start-due']"));
        Assert.Empty(cut.FindAll("[data-testid='start-pots']"));
        Assert.Empty(PlanApiFake.GetCalls);
    }

    // A6 (R5)
    [Fact]
    public void Coming_and_overdue_payments_are_listed_overdue_first_with_their_badge()
    {
        var soon = FakePlanApi.Position("Strom", 80m, day: 23);
        var late = FakePlanApi.Position("Miete", 30m, day: 15);
        SeedPlan(positions: [soon, late]);

        var cut = OpenStart();

        var rows = cut.FindAll("[data-testid^='start-due-item-']");
        Assert.Equal(2, rows.Count);
        Assert.Equal("start-due-item-overdue", rows[0].GetAttribute("data-testid"));
        Assert.Contains("Miete", rows[0].TextContent);
        Assert.Contains("überfällig", rows[0].TextContent);
        Assert.Contains("Strom", rows[1].TextContent);
        Assert.Contains("anstehend", rows[1].TextContent);
    }

    [Fact]
    public void Without_anything_due_the_block_says_so()
    {
        SeedPlan();

        var cut = OpenStart();

        Assert.Contains("In den nächsten Tagen ist nichts fällig.", Text(cut, "start-due"));
    }

    [Fact]
    public void At_most_five_payments_are_shown()
    {
        SeedPlan(positions: Enumerable.Range(0, 7).Select(i => FakePlanApi.Position($"Posten {i}", 10m, day: 22)).ToArray());

        var cut = OpenStart();

        Assert.Equal(5, cut.FindAll("[data-testid^='start-due-item-']").Count);
    }

    // A1 (R1): unter Verplanbar nur die verfügbaren Konten und ihre Summe
    [Fact]
    public void A1_the_account_line_lists_available_accounts_and_their_sum()
    {
        SeedPlan();
        Accounts.Accounts.Clear();
        AccountApiFake.Accounts.AddRange([Account("Girokonto", 1200m), Account("Tagesgeld", 5000m, available: false)]);

        var cut = OpenStart();

        var line = Text(cut, "start-accounts");
        Assert.Contains("Girokonto 1.200,00 €", line);
        Assert.Contains("Summe 1.200,00 €", line);
        Assert.DoesNotContain("Tagesgeld", line);
        Assert.Contains("start-accounts", cut.Find("[data-testid='start-hero']").InnerHtml);
        Assert.Null(AccountApiFake.ListOwners.Last());
    }

    [Fact]
    public void Without_a_grant_for_the_accounts_the_line_is_missing()
    {
        SeedPlan();
        AccountApiFake.Accounts.Add(Account("Girokonto", 1200m));
        HouseholdApiFake.Household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
        [
            FakeHouseholdApi.Anna(),
            FakeHouseholdApi.Ben(isMe: false) with { GrantsToMe = GrantLevels.None with { Plan = AccessLevelKeys.View } },
        ]);
        Member.LoadAsync().GetAwaiter().GetResult();
        Member.Choose(FakeHouseholdApi.BenId);

        var cut = OpenStart();

        Assert.Empty(cut.FindAll("[data-testid='start-accounts']"));
    }

    // A2 (R2): eine Zeile je Topf in Euro, ohne Richtwerte in Prozent
    [Fact]
    public void A2_a_pot_line_reads_booked_of_planned_without_guide_values()
    {
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with
        {
            Overview = Overview(pots: [new PlanPotResponse(PotKeys.Needs, 1778m, 1634m), new PlanPotResponse(PotKeys.Wants, 100m, 130m), new PlanPotResponse(PotKeys.Savings, 0m, 0m)]),
        };

        var cut = OpenStart();

        var pots = cut.Find("[data-testid='start-pots']");
        Assert.Equal(3, pots.QuerySelectorAll("[data-testid^='start-pot-']").Length);
        var needs = cut.Find("[data-testid='start-pot-needs']");
        Assert.Contains("Grundbedarf", needs.TextContent);
        Assert.Contains("1.634,00 € von 1.778,00 €", needs.TextContent);
        Assert.Contains("width: 91.9", needs.InnerHtml);
        Assert.DoesNotContain("überzogen", needs.TextContent);
        Assert.DoesNotContain("%", pots.TextContent);
        Assert.DoesNotContain("Ziel", pots.TextContent);
        Assert.Empty(cut.FindAll("[data-testid='quota-on-planned-income']"));
        var wants = cut.Find("[data-testid='start-pot-wants']");
        Assert.Contains("überzogen", wants.TextContent);
        Assert.NotEmpty(wants.QuerySelectorAll(".bar-fill--over-end"));
    }

    // Ein Topf ohne Plan und ohne Buchung zeigt "nichts geplant" statt Nullbeträgen und leerem Balken.
    [Fact]
    public void A_pot_with_nothing_planned_and_nothing_booked_says_so_without_amounts_and_bar()
    {
        SeedPlan();

        var cut = OpenStart();

        var savings = cut.Find("[data-testid='start-pot-savings']");
        Assert.Contains("Sparen", savings.TextContent);
        Assert.Contains("nichts geplant", savings.TextContent);
        Assert.DoesNotContain("0,00", savings.TextContent);
        Assert.Empty(savings.QuerySelectorAll(".bar"));
        Assert.Contains("muted", cut.Find("[data-testid='start-pot-savings'] .start-pot-sum").ClassName);
        Assert.NotEmpty(cut.Find("[data-testid='start-pot-needs']").QuerySelectorAll(".bar"));
    }

    // Name und Betrag haben immer einen Abstand, und der Betrag rutscht unter den Namen, wenn die Zeile nicht reicht (360 und 412 px).
    [Fact]
    public void The_pot_line_keeps_a_gap_and_lets_the_amount_wrap_below_the_name()
    {
        var css = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "duofy.css"));

        var line = System.Text.RegularExpressions.Regex.Match(css, @"\.start-pot\s*\{([^}]*)\}").Groups[1].Value;
        Assert.Contains("flex-wrap: wrap", line);
        Assert.Matches(@"gap:\s*\d+px\s+\d+px", line);
        var sum = System.Text.RegularExpressions.Regex.Match(css, @"\.start-pot-sum\s*\{([^}]*)\}").Groups[1].Value;
        Assert.Contains("flex: 1 1", sum);
        Assert.Contains("flex-wrap: wrap", sum);
    }

    // A4 (R4): der Haushaltsblock unter dem eigenen, nur für Mitglieder
    [Fact]
    public async Task A4_the_household_block_shows_only_for_members()
    {
        SeedPlan();
        var household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20, [FakeHouseholdApi.Anna(), FakeHouseholdApi.Ben()]);
        HouseholdApiFake.Household = household;
        HouseholdApiFake.PlanMonths.Add(new HouseholdPlanMonth(2026, 10, 700m));
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10) with
        {
            Free = 700m,
            Pots = [new HouseholdPotResponse(PotKeys.Needs, 2000m, 1500m), new HouseholdPotResponse(PotKeys.Wants, 0m, 0m), new HouseholdPotResponse(PotKeys.Savings, 0m, 0m)],
        };
        await Member.LoadAsync();

        var cut = OpenStart();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='start-household']")));
        var block = Text(cut, "start-household");
        Assert.Contains("Haushalt", block);
        Assert.Contains("700,00 €", block);
        Assert.Contains("1.500,00 € von 2.000,00 €", cut.Find("[data-testid='start-household-pot-needs']").TextContent);
        Assert.True(cut.Markup.IndexOf("start-hero", StringComparison.Ordinal) < cut.Markup.IndexOf("start-household\"", StringComparison.Ordinal));
        Assert.Equal("/haushaltsplan", cut.Find("[data-testid='start-household-link']").GetAttribute("href")![..14]);
    }

    [Fact]
    public void A4_without_a_household_there_is_no_household_block()
    {
        SeedPlan();
        HouseholdApiFake.Household = null;

        var cut = OpenStart();

        Assert.Empty(cut.FindAll("[data-testid='start-household']"));
    }

    // Review M2: Der Haushaltsblock hängt nicht am eigenen Plan des Monats.
    [Fact]
    public async Task M2_the_household_block_shows_although_the_own_month_has_no_plan()
    {
        var household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20, [FakeHouseholdApi.Anna(), FakeHouseholdApi.Ben()]);
        HouseholdApiFake.Household = household;
        HouseholdApiFake.PlanMonths.Add(new HouseholdPlanMonth(2026, 10, 700m));
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10) with { Free = 700m };
        await Member.LoadAsync();

        var cut = Render<Start>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='start-household']")));
        Assert.NotEmpty(cut.FindAll("[data-testid='start-empty']"));
        Assert.Contains("700,00 €", Text(cut, "start-household"));
    }

    // Spec 126, A3 (R3): In der Sicht auf ein anderes Mitglied steht derselbe Haushaltsblock wie bei einem selbst.
    [Fact]
    public async Task A3_the_household_block_shows_in_the_view_of_another_member_too()
    {
        SeedPlan();
        var household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
            [FakeHouseholdApi.Anna(), FakeHouseholdApi.Ben(isMe: false) with { GrantsToMe = GrantLevels.None with { Plan = AccessLevelKeys.View } }]);
        HouseholdApiFake.Household = household;
        HouseholdApiFake.PlanMonths.Add(new HouseholdPlanMonth(2026, 10, 700m));
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10) with { Free = 700m };
        await Member.LoadAsync();
        Member.Choose(FakeHouseholdApi.BenId);

        var cut = OpenStart();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='start-household']")));
        Assert.Contains("700,00 €", Text(cut, "start-household"));
    }

    // Spec 126, A3 (R3): Die Begrüßung nennt die gewählte Person, und alle Blöcke stehen da wie bei einem selbst.
    [Fact]
    public async Task A3_the_chosen_person_is_greeted_and_gets_the_same_blocks_as_oneself()
    {
        SeedPlan(free: 340m, positions: []);
        AccountApiFake.Accounts.Add(Account("Girokonto", 1200m));
        var household = await SeedHouseholdPlanAsync(GuidelineMember(FakeHouseholdApi.AnnaId, "Anna", 50m, 30m, 20m), GuidelineMember(FakeHouseholdApi.BenId, "Ben", 75m, 15m, 10m));
        var own = OpenStart();
        cut_ShouldHaveAllBlocks(own);
        var ownBlocks = Blocks(own);
        Assert.Contains("Hallo, Anna!", own.Markup);

        HouseholdApiFake.Household = household with
        {
            Members = [FakeHouseholdApi.Anna(), FakeHouseholdApi.Ben(isMe: false) with { GrantsToMe = new GrantLevels("delete", "delete", "delete", "delete", "delete") }],
        };
        await Member.RefreshAsync();
        SeedPlan(free: 120m, positions: []);
        Assert.Contains("340,00 €", Text(own, "start-hero"));
        Member.Choose(FakeHouseholdApi.BenId);

        own.WaitForAssertion(() => Assert.Contains("Hallo, Ben!", own.Markup));
        own.WaitForAssertion(() => Assert.Equal(ownBlocks, Blocks(own)));
        // Die Blöcke zeigen Bens Zahlen, nicht mehr die eigenen: Der Besitzer ging mit, und das Verplanbar ist neu.
        own.WaitForAssertion(() => Assert.Contains("120,00 €", Text(own, "start-hero")));
        Assert.DoesNotContain("340,00 €", Text(own, "start-hero"));
        Assert.Equal(FakeHouseholdApi.BenId, PlanApiFake.Owners.Last());
        Assert.Contains("Jahr von Ben", Text(own, "start-year-section"));
        Assert.DoesNotContain("Dein Jahr", own.Markup);
        Assert.DoesNotContain("Hallo, Anna!", own.Markup);
    }

    private static readonly string[] BlockIds = ["start-due", "start-pots", "start-guideline", "start-household", "start-year-section"];

    private static void cut_ShouldHaveAllBlocks(IRenderedComponent<Start> cut) =>
        cut.WaitForAssertion(() => Assert.All(BlockIds, id => Assert.NotEmpty(cut.FindAll($"[data-testid='{id}']"))));

    private static List<string> Blocks(IRenderedComponent<Start> cut) =>
        BlockIds.Where(id => cut.FindAll($"[data-testid='{id}']").Count > 0).ToList();

    // Review M3: Die Jahreslinie steht auch ohne Plan im laufenden Monat, wenn frühere Monate einen haben.
    [Fact]
    public void M3_the_year_line_shows_although_the_current_month_has_no_plan()
    {
        PlanApiFake.Overview = [new PlanOverviewMonthResponse(2026, 8, 20m, false, 0m, []), new PlanOverviewMonthResponse(2026, 7, 10m, false, 0m, [])];

        var cut = Render<Start>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='start-year-section']")));
        Assert.NotEmpty(cut.FindAll("[data-testid='start-empty']"));
        Assert.Equal("Juli: 10,00 €", cut.Find("[data-testid='start-year-bar-7']").GetAttribute("aria-label"));
    }

    // A5 (R5): Jahreslinie von Januar bis zum laufenden Monat, grün und rot, ohne künftige Monate
    [Fact]
    public void A5_the_year_line_has_one_bar_per_past_month()
    {
        SeedPlan();
        PlanApiFake.Overview =
        [
            .. Enumerable.Range(1, 10).Select(m => new PlanOverviewMonthResponse(2026, m, m == 3 ? -120m : 100m * m, false, 0m, [])).Reverse(),
            new PlanOverviewMonthResponse(2026, 11, 50m, false, 0m, []),
        ];

        var cut = OpenStart();

        var bars = cut.FindAll("[data-testid^='start-year-bar-']");
        Assert.Equal(10, bars.Count);
        Assert.Empty(cut.FindAll("[data-testid='start-year-bar-11']"));
        Assert.Equal("Januar: 100,00 €", cut.Find("[data-testid='start-year-bar-1']").GetAttribute("aria-label"));
        Assert.Equal("März: -120,00 €", cut.Find("[data-testid='start-year-bar-3']").GetAttribute("aria-label"));
        Assert.Contains("year-bar--positive", cut.Find("[data-testid='start-year-bar-1']").ClassName);
        Assert.Contains("year-bar--negative", cut.Find("[data-testid='start-year-bar-3']").ClassName);
        Assert.Equal("/plan", cut.Find("[data-testid='start-year-link']").GetAttribute("href"));
        Assert.Equal([2026], PlanApiFake.OverviewCalls.Distinct());
    }

    // Ein älterer Server kennt die Übersicht nicht (404): Die Startseite zeigt alles andere, nur ohne Jahreslinie und ohne Fehlermeldung.
    [Fact]
    public void Against_a_server_without_the_overview_the_start_page_shows_everything_but_the_year_line()
    {
        SeedPlan(free: 340m);
        PlanApiFake.OverviewMissing = true;
        AccountApiFake.Accounts.Add(Account("Girokonto", 1200m));

        var cut = OpenStart();

        Assert.Contains("340,00 €", Text(cut, "start-hero"));
        Assert.Contains("Girokonto 1.200,00 €", Text(cut, "start-accounts"));
        Assert.Equal(3, cut.FindAll("[data-testid^='start-pot-']").Count);
        Assert.Empty(cut.FindAll("[data-testid='start-year-section']"));
        Assert.Empty(cut.FindAll(AlertSelector));
        Assert.DoesNotContain("Das gibt es nicht mehr", cut.Markup);
        Assert.Equal(1, PlanApiFake.ListMonthsCalls);
    }

    [Fact]
    public void Against_a_server_without_the_overview_a_missing_month_still_shows_the_empty_state()
    {
        PlanApiFake.OverviewMissing = true;

        var cut = Render<Start>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='start-empty']")));
        Assert.Empty(cut.FindAll(AlertSelector));
    }

    [Fact]
    public void A_month_without_a_plan_is_an_empty_slot_in_the_year_line()
    {
        SeedPlan();
        PlanApiFake.Overview = [new PlanOverviewMonthResponse(2026, 10, 340m, false, 0m, []), new PlanOverviewMonthResponse(2026, 8, 20m, false, 0m, [])];

        var cut = OpenStart();

        Assert.Equal(10, cut.FindAll("[data-testid^='start-year-bar-']").Count);
        Assert.Equal("Januar: kein Plan", cut.Find("[data-testid='start-year-bar-1']").GetAttribute("aria-label"));
        Assert.Contains("year-bar--empty", cut.Find("[data-testid='start-year-bar-1']").ClassName);
    }

    // A8 (R7)
    [Fact]
    public void The_blocks_open_the_month_of_the_plan()
    {
        SeedPlan();

        var cut = OpenStart();

        foreach (var id in new[] { "start-hero-link", "start-due-link", "start-pots-link" })
        {
            Assert.Equal("/plan/2026/10", cut.Find($"[data-testid='{id}']").GetAttribute("href"));
        }
    }

    // Spec 126, R3: Ohne die Freigabe der Konten fehlt die Kontenzeile nicht stumm, sondern ein kurzer Hinweis sagt es.
    [Fact]
    public async Task Without_a_grant_for_the_accounts_a_hint_says_so()
    {
        SeedPlan();
        await ChooseBenAsync(GrantLevels.None with { Plan = AccessLevelKeys.View });

        var cut = OpenStart();

        Assert.Equal("Ben teilt Konten nicht mit dir.", Text(cut, "start-accounts-not-shared").Trim());
    }

    [Fact]
    public async Task Another_member_who_shares_the_plan_but_not_the_bookings_shows_their_figures_without_payments()
    {
        SeedPlan(free: 120m);
        await ChooseBenAsync(GrantLevels.None with { Plan = AccessLevelKeys.View });

        var cut = OpenStart();

        Assert.Equal(FakeHouseholdApi.BenId, PlanApiFake.Owners.Last());
        Assert.Contains("120,00 €", Text(cut, "start-hero"));
        Assert.Contains("Ben teilt Buchungen nicht mit dir.", Text(cut, "start-due"));
        Assert.Empty(cut.FindAll("[data-testid^='start-due-item-']"));
    }

    // A10 (R8)
    [Fact]
    public async Task Another_member_who_does_not_share_the_plan_shows_the_note_instead_of_the_blocks()
    {
        SeedPlan();
        await ChooseBenAsync(GrantLevels.None with { Accounts = AccessLevelKeys.View });

        var cut = Render<Start>();

        cut.WaitForAssertion(() => Assert.Contains(NotShared, cut.Markup));
        Assert.Empty(cut.FindAll("[data-testid='start-free']"));
        Assert.Empty(PlanApiFake.GetCalls);
        Assert.Equal(0, PlanApiFake.ListMonthsCalls);
    }

    // A11 (R8)
    [Fact]
    public async Task The_household_view_shows_the_household_plan_and_offers_no_booking()
    {
        var household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20, [FakeHouseholdApi.Anna(), FakeHouseholdApi.Ben()]);
        HouseholdApiFake.Household = household;
        HouseholdApiFake.PlanMonths.Add(new HouseholdPlanMonth(2026, 10, 700m));
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10) with { Free = 700m };
        await Member.LoadAsync();
        Member.ChooseHousehold();

        var cut = OpenStart();

        Assert.Contains("700,00 €", Text(cut, "start-hero"));
        Assert.Equal((household.Id, 2026, 10), Assert.Single(HouseholdApiFake.PlanCalls));
        Assert.Empty(cut.FindAll("[data-testid='start-new-booking']"));
        Assert.Equal(0, PlanApiFake.ListMonthsCalls);
    }

    // Der Wechsel im Umschalter lädt die Zahlen der neuen Sicht.
    [Fact]
    public async Task Choosing_another_member_reloads_the_figures()
    {
        SeedPlan(free: 340m);
        HouseholdApiFake.Household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
        [
            FakeHouseholdApi.Anna(),
            FakeHouseholdApi.Ben(isMe: false) with { GrantsToMe = GrantLevels.None with { Plan = AccessLevelKeys.View } },
        ]);
        var cut = OpenStart();
        Assert.Contains("340,00 €", Text(cut, "start-hero"));

        Member.Choose(FakeHouseholdApi.BenId);

        cut.WaitForAssertion(() => Assert.Equal(FakeHouseholdApi.BenId, PlanApiFake.Owners.Last()));
    }

    // --- Richtwert-Hinweis (Issue #34) ------------------------------------------------------------------------------

    private static HouseholdMemberResponse GuidelineMember(Guid id, string name, decimal needs, decimal wants, decimal savings) => new(
        id, name, 3000m, true,
        [
            new HouseholdMemberQuotaResponse(PotKeys.Needs, 50, needs, needs - 50m),
            new HouseholdMemberQuotaResponse(PotKeys.Wants, 30, wants, wants - 30m),
            new HouseholdMemberQuotaResponse(PotKeys.Savings, 20, savings, savings - 20m),
        ]);

    private async Task<HouseholdResponse> SeedHouseholdPlanAsync(params HouseholdMemberResponse[] members)
    {
        var household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20, [FakeHouseholdApi.Anna(), FakeHouseholdApi.Ben()]);
        HouseholdApiFake.Household = household;
        HouseholdApiFake.PlanMonths.Add(new HouseholdPlanMonth(2026, 10, 700m));
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10) with { Free = 700m, Members = members };
        await Member.LoadAsync();
        return household;
    }

    [Fact]
    public async Task In_the_household_view_a_person_far_from_the_guideline_gets_a_calm_hint_with_the_name()
    {
        await SeedHouseholdPlanAsync(GuidelineMember(FakeHouseholdApi.AnnaId, "Anna", 50m, 30m, 20m), GuidelineMember(FakeHouseholdApi.BenId, "Ben", 75m, 15m, 10m));
        Member.ChooseHousehold();

        var cut = OpenStart();

        var hint = Text(cut, "start-guideline");
        Assert.Contains("Ben", hint);
        Assert.DoesNotContain("Anna", hint);
        Assert.Contains("Anhaltspunkt", hint);
        Assert.Equal($"/plan/2026/10", cut.Find("[data-testid='start-guideline-link']").GetAttribute("href"));
        Assert.Single(HouseholdApiFake.PlanCalls);
    }

    [Fact]
    public async Task Without_a_deviation_beyond_the_tolerance_there_is_no_hint()
    {
        await SeedHouseholdPlanAsync(GuidelineMember(FakeHouseholdApi.AnnaId, "Anna", 52m, 28m, 20m), GuidelineMember(FakeHouseholdApi.BenId, "Ben", 48m, 32m, 20m));
        Member.ChooseHousehold();

        var cut = OpenStart();

        Assert.Empty(cut.FindAll("[data-testid='start-guideline']"));
    }

    [Fact]
    public async Task A_person_without_income_gets_no_hint()
    {
        await SeedHouseholdPlanAsync(
            GuidelineMember(FakeHouseholdApi.AnnaId, "Anna", 50m, 30m, 20m),
            new HouseholdMemberResponse(FakeHouseholdApi.BenId, "Ben", 0m, false, []));
        Member.ChooseHousehold();

        var cut = OpenStart();

        Assert.Empty(cut.FindAll("[data-testid='start-guideline']"));
    }

    [Fact]
    public async Task In_the_own_view_of_a_household_member_the_hint_is_there_too()
    {
        SeedPlan(free: 340m);
        var household = await SeedHouseholdPlanAsync(GuidelineMember(FakeHouseholdApi.AnnaId, "Anna", 80m, 10m, 10m), GuidelineMember(FakeHouseholdApi.BenId, "Ben", 50m, 30m, 20m));

        var cut = OpenStart();

        cut.WaitForAssertion(() => Assert.Contains("Anna", Text(cut, "start-guideline")));
        Assert.Equal((household.Id, 2026, 10), Assert.Single(HouseholdApiFake.PlanCalls));
    }

    [Fact]
    public async Task Without_a_household_plan_for_the_month_or_without_a_household_there_is_no_hint()
    {
        SeedPlan(free: 340m);
        var cut = OpenStart();
        Assert.Empty(cut.FindAll("[data-testid='start-guideline']"));
        Assert.Empty(HouseholdApiFake.PlanCalls);

        HouseholdApiFake.Household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20, [FakeHouseholdApi.Anna(), FakeHouseholdApi.Ben()]);
        await Member.LoadAsync();
        var again = OpenStart();
        Assert.Empty(again.FindAll("[data-testid='start-guideline']"));
        Assert.Empty(HouseholdApiFake.PlanCalls);
    }

    private sealed class SignedInAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(System.Security.Claims.ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements) =>
            Task.FromResult(user.Identity?.IsAuthenticated == true ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        public Task<AuthorizationResult> AuthorizeAsync(System.Security.Claims.ClaimsPrincipal user, object? resource, string policyName) =>
            AuthorizeAsync(user, resource, []);
    }

    private sealed class NoThemeStore : IThemePreferenceStore
    {
        public Task<ThemeChoice> LoadAsync() => Task.FromResult(ThemeChoice.Device);
        public Task SaveAsync(ThemeChoice choice) => Task.CompletedTask;
    }
}
