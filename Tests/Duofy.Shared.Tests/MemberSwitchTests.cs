using Bunit;
using Bunit.TestDoubles;
using Duofy.Schemas;
using Duofy.Shared.Layout;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 44 und Spec 68, R8: der Profil-Knopf mit dem Menü "wessen Zahlen". Angemeldet ist hier Ben; Anna ist ein
/// anderes Mitglied seines Haushalts und gibt ihm frei, was der Test vorgibt.
/// </summary>
public class MemberSwitchTests : PageTestContext
{
    private static readonly GrantLevels PlanView = GrantLevels.None with { Plan = AccessLevelKeys.View };

    private readonly BunitAuthorizationContext authorization;

    public MemberSwitchTests()
    {
        authorization = AddAuthorization();
        authorization.SetAuthorized("ben");
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

    private IRenderedComponent<MemberSwitch> OpenMenu()
    {
        var cut = Render<MemberSwitch>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='member-profile']")));
        cut.Find("[data-testid='member-profile']").Click();
        return cut;
    }

    private static string ProfileText(IRenderedComponent<MemberSwitch> cut) =>
        cut.Find("[data-testid='member-profile']").TextContent.Trim();

    private static List<string> Choices(IRenderedComponent<MemberSwitch> cut) =>
        cut.FindAll("[data-testid^='member-choice-']").Select(i => i.GetAttribute("data-testid")!).ToList();

    // A1 (R1): Allein und ohne Haushalt gibt es den Profil-Knopf trotzdem, und das Menü bietet nur Ich an.
    [Fact]
    public void Without_a_household_the_profile_button_is_there_and_offers_only_me()
    {
        HouseholdApiFake.Household = null;

        var cut = OpenMenu();
        cut.WaitForAssertion(() => Assert.Equal(1, HouseholdApiFake.MineCalls));

        Assert.Equal("Ich", ProfileText(cut));
        Assert.Equal(["member-choice-me", "member-choice-household"], Choices(cut));
    }

    // A1 (R1): Mit Haushalt, aber ohne Freigabe, bietet das Menü keine Person an.
    [Fact]
    public void Without_any_grant_the_menu_offers_no_person()
    {
        Household(GrantLevels.None);

        var cut = OpenMenu();

        cut.WaitForAssertion(() => Assert.Equal(1, HouseholdApiFake.MineCalls));
        Assert.Empty(Member.Sharing);
        Assert.Equal(["member-choice-me", "member-choice-household"], Choices(cut));
    }

    // A2 (R2): Ohne Haushalt ist "Haushalt" als nicht verfügbar gekennzeichnet, nennt den Grund und führt zur Haushalt-Seite.
    [Fact]
    public void Without_a_household_the_household_entry_is_unavailable_explains_why_and_links_to_the_household_page()
    {
        HouseholdApiFake.Household = null;

        var cut = OpenMenu();
        cut.WaitForAssertion(() => Assert.Equal(1, HouseholdApiFake.MineCalls));

        var entry = cut.Find("[data-testid='member-choice-household']");
        Assert.Equal("a", entry.TagName.ToLowerInvariant());
        Assert.Equal("true", entry.GetAttribute("aria-disabled"));
        Assert.Equal("/haushalt", entry.GetAttribute("href"));
        Assert.Equal("member-household-reason", entry.GetAttribute("aria-describedby"));
        Assert.Contains("mindestens zwei", cut.Find("#member-household-reason").TextContent);
    }

    // A2 (R2): Auch ein Haushalt von einem Mitglied ist nicht verfügbar.
    [Fact]
    public void In_a_household_of_one_the_household_entry_is_unavailable()
    {
        HouseholdApiFake.Household = HouseholdApiFake.Household! with { Members = [FakeHouseholdApi.Ben(isMe: true)] };

        var cut = OpenMenu();
        cut.WaitForAssertion(() => Assert.Equal(1, HouseholdApiFake.MineCalls));

        Assert.Equal("true", cut.Find("[data-testid='member-choice-household']").GetAttribute("aria-disabled"));
    }

    // A3 (R2): Bei zwei Mitgliedern wählt ein Tipp auf "Haushalt" die Haushaltssicht, und der Knopf sagt es.
    [Fact]
    public void With_two_members_the_household_entry_chooses_the_household_view()
    {
        Household(GrantLevels.None);
        var cut = OpenMenu();
        cut.WaitForAssertion(() => Assert.Equal("button", cut.Find("[data-testid='member-choice-household']").TagName.ToLowerInvariant()));

        cut.Find("[data-testid='member-choice-household']").Click();

        Assert.True(Member.IsHousehold);
        Assert.Null(Member.Owner);
        Assert.Equal("Haushalt", ProfileText(cut));
        Assert.Empty(cut.FindAll("[data-testid='member-menu']"));
    }

    // A4 (R3): Anna steht mit Vornamen im Menü; Wählen macht sie zum Besitzer, Ich nimmt es zurück.
    [Fact]
    public void With_a_grant_the_menu_offers_me_and_anna_and_choosing_her_makes_her_the_owner()
    {
        Household(PlanView);

        var cut = OpenMenu();
        var annaChoice = $"member-choice-{FakeHouseholdApi.AnnaId}";
        cut.WaitForAssertion(() => Assert.Equal(["member-choice-me", annaChoice, "member-choice-household"], Choices(cut)));
        Assert.Equal("Anna", cut.Find($"[data-testid='{annaChoice}']").TextContent.Trim());
        Assert.Equal("Wessen Zahlen", cut.Find("[data-testid='member-menu']").GetAttribute("aria-label"));

        cut.Find($"[data-testid='{annaChoice}']").Click();

        Assert.Equal(FakeHouseholdApi.AnnaId, Member.Owner);
        Assert.Equal("Anna", Member.Name);
        Assert.Equal("Anna", ProfileText(cut));
        Assert.True(Member.Allows(GrantAreas.Plan, AccessLevelKeys.View));
        Assert.False(Member.Allows(GrantAreas.Plan, AccessLevelKeys.Create));
        Assert.False(Member.Allows(GrantAreas.Commitments, AccessLevelKeys.View));

        cut.Find("[data-testid='member-profile']").Click();
        cut.Find("[data-testid='member-choice-me']").Click();

        Assert.Null(Member.Owner);
        Assert.Equal("Ich", ProfileText(cut));
        Assert.True(Member.Allows(GrantAreas.Commitments, AccessLevelKeys.Delete));
    }

    // A5 (R4): Ein zweiter Umschalter (andere Seite, anderes Layout) zeigt dieselbe Sicht, beschriftet.
    [Fact]
    public void A_second_switch_after_navigating_shows_the_same_chosen_view()
    {
        Household(PlanView);
        var first = OpenMenu();
        var annaChoice = $"[data-testid='member-choice-{FakeHouseholdApi.AnnaId}']";
        first.WaitForAssertion(() => Assert.NotEmpty(first.FindAll(annaChoice)));
        first.Find(annaChoice).Click();

        var second = Render<MemberSwitch>();

        second.WaitForAssertion(() => Assert.Equal("Anna", ProfileText(second)));
    }

    // A6 (R5): Das Menü endet mit Konto und Abmelden; Konto führt zur Kontoseite.
    [Fact]
    public void The_menu_offers_account_and_sign_out_after_the_views()
    {
        var cut = OpenMenu();

        var items = cut.FindAll("[data-testid='member-menu'] [role^='menuitem']").Select(i => i.GetAttribute("data-testid")).ToList();
        Assert.Equal(["member-choice-me", "member-choice-household", "member-account", "member-sign-out"], items);
        Assert.Equal("/konto", cut.Find("[data-testid='member-account']").GetAttribute("href"));
    }

    // A6 (R5): Abmelden meldet ab und führt zur Anmeldung.
    [Fact]
    public void Signing_out_from_the_menu_logs_out_and_goes_to_the_sign_in_page()
    {
        var cut = OpenMenu();

        cut.Find("[data-testid='member-sign-out']").Click();

        cut.WaitForAssertion(() => Assert.Equal(1, Auth.LogoutCalls));
        Assert.EndsWith("/anmelden", Services.GetRequiredService<NavigationManager>().Uri);
    }

    // R5: Das Menü schließt sich per Escape und beim zweiten Tippen auf den Knopf.
    [Fact]
    public void The_menu_closes_with_escape_and_with_a_second_tap()
    {
        var cut = OpenMenu();
        Assert.Equal("true", cut.Find("[data-testid='member-profile']").GetAttribute("aria-expanded"));

        cut.Find("[data-testid='member-switch']").KeyDown("Escape");
        Assert.Empty(cut.FindAll("[data-testid='member-menu']"));

        cut.Find("[data-testid='member-profile']").Click();
        cut.Find("[data-testid='member-profile']").Click();
        Assert.Empty(cut.FindAll("[data-testid='member-menu']"));
    }

    // A7 (R6): Ohne Freigabe steht Anna nicht im Menü; wird sie trotzdem gewählt, bleibt es bei Ich und es gibt
    // keinen fremden Besitzer, mit dem Daten angefragt würden.
    [Fact]
    public void Choosing_a_person_without_a_grant_shows_no_data_of_that_person()
    {
        Household(GrantLevels.None);
        var cut = OpenMenu();
        cut.WaitForAssertion(() => Assert.Equal(1, HouseholdApiFake.MineCalls));
        Assert.DoesNotContain($"member-choice-{FakeHouseholdApi.AnnaId}", Choices(cut));

        Member.Choose(FakeHouseholdApi.AnnaId);

        Assert.Null(Member.Owner);
        Assert.Null(Member.Chosen);
        Assert.Equal("Ich", ProfileText(cut));
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
