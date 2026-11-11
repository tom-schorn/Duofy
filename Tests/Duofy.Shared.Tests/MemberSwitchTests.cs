using Bunit;
using Bunit.TestDoubles;
using Duofy.Schemas;
using Duofy.Shared.Layout;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 44 und Spec 68, R8, mit Spec 82 R11: die Auswahl "wessen Zahlen" im Profil-Menü unten in der Seitenleiste. Angemeldet ist hier Ben; Anna ist ein
/// anderes Mitglied seines Haushalts und gibt ihm frei, was der Test vorgibt.
/// </summary>
public class MemberSwitchTests : PageTestContext
{
    private static readonly GrantLevels PlanView = GrantLevels.None with { Plan = AccessLevelKeys.View };

    private readonly BunitAuthorizationContext authorization;

    // Das Menü geht in der Hülle für Dialoge und Menüs auf; dort stehen seine Einträge, solange es offen ist.
    private readonly IRenderedComponent<IComponent> menu;

    public MemberSwitchTests()
    {
        authorization = AddAuthorization();
        authorization.SetAuthorized("ben");
        menu = RenderDialogHost();
    }

    private void Household(GrantLevels annaGrantsBen)
    {
        HouseholdApiFake.Household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
        [
            FakeHouseholdApi.Anna(isMe: false) with { GrantsToMe = annaGrantsBen },
            FakeHouseholdApi.Ben(isMe: true),
        ]);
    }

    private ActiveMember Member => Services.GetRequiredService<ActiveMember>();

    private IRenderedComponent<UserMenu> OpenMenu()
    {
        var cut = Render<UserMenu>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='member-profile']")));
        Toggle(cut);
        return cut;
    }

    private void Toggle(IRenderedComponent<UserMenu> cut)
    {
        cut.Find("[data-testid='user-menu-open']").Click();
        cut.WaitForState(() => true);
    }

    private static string ProfileText(IRenderedComponent<UserMenu> cut) =>
        cut.Find("[data-testid='member-profile']").TextContent.Trim();

    // Offen oder zu steht am Knopf; die Einträge gibt es nur, solange das Menü offen ist.
    private bool MenuIsOpen(IRenderedComponent<UserMenu> cut) =>
        cut.Find("[data-testid='user-menu-open']").GetAttribute("aria-expanded") == "true" && menu.FindAll("[data-testid='member-choice-me']").Count > 0;

    private string ItemText(string testId) => menu.Find($"[data-testid='{testId}']").TextContent.Trim();

    private List<string> Choices() =>
        menu.FindAll("[data-testid^='member-choice-']").Select(i => i.GetAttribute("data-testid")!).ToList();

    // A1 (R1): Allein und ohne Haushalt gibt es den Profil-Knopf trotzdem, und das Menü bietet nur Ich an.
    [Fact]
    public void Without_a_household_the_profile_button_is_there_and_offers_only_me()
    {
        HouseholdApiFake.Household = null;

        var cut = OpenMenu();
        cut.WaitForAssertion(() => Assert.Equal(1, HouseholdApiFake.MineCalls));

        Assert.DoesNotContain("Ansicht", ProfileText(cut));
        menu.WaitForAssertion(() => Assert.Equal(["member-choice-me"], Choices()));
    }

    // A1 (R1): Mit Haushalt, aber ohne Freigabe, bietet das Menü keine Person an.
    [Fact]
    public void Without_any_grant_the_menu_offers_no_person()
    {
        Household(GrantLevels.None);

        var cut = OpenMenu();

        cut.WaitForAssertion(() => Assert.Equal(1, HouseholdApiFake.MineCalls));
        Assert.Empty(Member.Sharing);
        menu.WaitForAssertion(() => Assert.Equal(["member-choice-me"], Choices()));
    }

    // Spec 126, A1 (R1): Mit Haushalt und Freigabe bietet das Menü Ich und die Person an, aber keinen Eintrag "Haushalt".
    [Fact]
    public void A1_the_menu_lists_me_and_the_person_and_no_household_entry()
    {
        Household(PlanView);

        OpenMenu();

        menu.WaitForAssertion(() => Assert.Equal(["member-choice-me", $"member-choice-{FakeHouseholdApi.AnnaId}"], Choices()));
        Assert.Empty(menu.FindAll("[data-testid='member-choice-household'], #member-household-reason"));
        Assert.DoesNotContain("Haushalt", menu.Markup.Replace("Haushalt von Anna", ""));
    }

    // A4 (R3): Anna steht mit Vornamen im Menü; Wählen macht sie zum Besitzer, Ich nimmt es zurück.
    [Fact]
    public void With_a_grant_the_menu_offers_me_and_anna_and_choosing_her_makes_her_the_owner()
    {
        Household(PlanView);

        var cut = OpenMenu();
        var annaChoice = $"member-choice-{FakeHouseholdApi.AnnaId}";
        menu.WaitForAssertion(() => Assert.Equal(["member-choice-me", annaChoice], Choices()));
        Assert.Equal("Anna", ItemText(annaChoice));
        Assert.StartsWith("Benutzermenü", cut.Find("[data-testid='user-menu-open']").GetAttribute("aria-label"));

        menu.Find($"[data-testid='{annaChoice}']").Click();

        Assert.Equal(FakeHouseholdApi.AnnaId, Member.Owner);
        Assert.Equal("Anna", Member.Name);
        cut.WaitForAssertion(() => Assert.Equal("Ansicht: Anna", ProfileText(cut)));
        Assert.True(Member.Allows(GrantAreas.Plan, AccessLevelKeys.View));
        Assert.False(Member.Allows(GrantAreas.Plan, AccessLevelKeys.Create));
        Assert.False(Member.Allows(GrantAreas.Commitments, AccessLevelKeys.View));

        Toggle(cut);
        menu.WaitForAssertion(() => Assert.NotEmpty(menu.FindAll("[data-testid='member-choice-me']")));
        menu.Find("[data-testid='member-choice-me']").Click();

        Assert.Null(Member.Owner);
        cut.WaitForAssertion(() => Assert.DoesNotContain("Ansicht", ProfileText(cut)));
        Assert.True(Member.Allows(GrantAreas.Commitments, AccessLevelKeys.Delete));
    }

    // A5 (R4): Ein zweiter Umschalter (andere Seite, anderes Layout) zeigt dieselbe Sicht, beschriftet.
    [Fact]
    public void A_second_switch_after_navigating_shows_the_same_chosen_view()
    {
        Household(PlanView);
        OpenMenu();
        var annaChoice = $"[data-testid='member-choice-{FakeHouseholdApi.AnnaId}']";
        menu.WaitForAssertion(() => Assert.NotEmpty(menu.FindAll(annaChoice)));
        menu.Find(annaChoice).Click();

        var second = Render<UserMenu>();

        second.WaitForAssertion(() => Assert.Equal("Ansicht: Anna", ProfileText(second)));
    }

    // A6 (R5), Spec 82: Der Umschalter bietet nur die Sichten; Konto und Abmelden stehen im Benutzermenü.
    [Fact]
    public void The_menu_offers_only_the_views_and_no_account_or_sign_out()
    {
        OpenMenu();

        menu.WaitForAssertion(() => Assert.Equal(["member-choice-me"], Choices()));
        Assert.Empty(menu.FindAll("[data-testid='member-account'], [data-testid='member-sign-out']"));
    }

    // R5: Das Menü schließt sich beim zweiten Tippen auf den Knopf. Escape bringt Blueprint über sein JavaScript mit; das ist im Browser gemessen.
    [Fact]
    public void The_menu_closes_with_a_second_tap()
    {
        var cut = OpenMenu();
        cut.WaitForAssertion(() => Assert.True(MenuIsOpen(cut)));

        Toggle(cut);

        cut.WaitForAssertion(() => Assert.False(MenuIsOpen(cut)));
    }

    [Fact]
    public void The_current_view_is_marked_in_the_menu()
    {
        Household(PlanView);
        var cut = OpenMenu();
        menu.WaitForAssertion(() => Assert.Equal("true", menu.Find("[data-testid='member-choice-me']").GetAttribute("aria-current")));

        menu.Find($"[data-testid='member-choice-{FakeHouseholdApi.AnnaId}']").Click();
        cut.WaitForAssertion(() => Assert.False(MenuIsOpen(cut)));
        Toggle(cut);

        menu.WaitForAssertion(() => Assert.Equal("true", menu.Find($"[data-testid='member-choice-{FakeHouseholdApi.AnnaId}']").GetAttribute("aria-current")));
        Assert.Null(menu.Find("[data-testid='member-choice-me']").GetAttribute("aria-current"));
    }

    // A7 (R6): Ohne Freigabe steht Anna nicht im Menü; wird sie trotzdem gewählt, bleibt es bei Ich und es gibt
    // keinen fremden Besitzer, mit dem Daten angefragt würden.
    [Fact]
    public void Choosing_a_person_without_a_grant_shows_no_data_of_that_person()
    {
        Household(GrantLevels.None);
        var cut = OpenMenu();
        cut.WaitForAssertion(() => Assert.Equal(1, HouseholdApiFake.MineCalls));
        Assert.DoesNotContain($"member-choice-{FakeHouseholdApi.AnnaId}", Choices());

        Member.Choose(FakeHouseholdApi.AnnaId);

        Assert.Null(Member.Owner);
        Assert.Null(Member.Chosen);
        Assert.DoesNotContain("Ansicht", ProfileText(cut));
    }

    // R8: Abmelden setzt die Wahl zurück.
    [Fact]
    public async Task Signing_out_resets_the_chosen_member()
    {
        Household(PlanView);
        await Member.LoadAsync();
        Member.Choose(FakeHouseholdApi.AnnaId);

        authorization.SetNotAuthorized();

        Assert.Null(Member.Owner);
        Assert.Empty(Member.Sharing);
    }

    // A15: Erneuert der Client im Hintergrund die Tokens, meldet der Anmeldezustand eine Änderung; Ben ist
    // aber weiter angemeldet, deshalb bleibt Anna gewählt.
    [Fact]
    public async Task Renewing_the_tokens_keeps_the_chosen_member()
    {
        Household(PlanView);
        await Member.LoadAsync();
        Member.Choose(FakeHouseholdApi.AnnaId);

        authorization.SetAuthorized("ben");
        await Member.LoadAsync();

        Assert.Equal(FakeHouseholdApi.AnnaId, Member.Owner);
        Assert.Single(Member.Sharing);
    }

    // A15: Steht nach der Änderung jemand anderes als "Ich" im Haushalt, gilt die Wahl nicht mehr.
    [Fact]
    public async Task When_another_person_is_signed_in_afterwards_the_choice_falls_back_to_me()
    {
        Household(PlanView);
        await Member.LoadAsync();
        Member.Choose(FakeHouseholdApi.AnnaId);

        var clara = FakeHouseholdApi.Ben(isMe: true) with { UserId = Guid.NewGuid(), FirstName = "Clara" };
        HouseholdApiFake.Household = HouseholdApiFake.Household! with
        {
            Members = [FakeHouseholdApi.Anna(isMe: false) with { GrantsToMe = PlanView }, clara],
        };
        authorization.SetAuthorized("clara");
        await Member.LoadAsync();

        Assert.Null(Member.Owner);
    }

    // R3: Nimmt Anna alles zurück, gilt nach dem Neuladen wieder "Ich".
    [Fact]
    public async Task When_anna_takes_her_grants_back_the_choice_falls_back_to_me()
    {
        Household(PlanView);
        await Member.LoadAsync();
        Member.Choose(FakeHouseholdApi.AnnaId);

        Household(GrantLevels.None);
        await Member.RefreshAsync();

        Assert.Null(Member.Owner);
        Assert.Empty(Member.Sharing);
    }
}
