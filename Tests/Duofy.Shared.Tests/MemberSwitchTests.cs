using Bunit;
using Bunit.TestDoubles;
using Duofy.Schemas;
using Duofy.Shared.Layout;
using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 68, R8: der Umschalter "wessen Zahlen". Angemeldet ist hier Ben; Anna ist ein anderes Mitglied
/// seines Haushalts und gibt ihm frei, was der Test vorgibt.
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

    // A14: Ohne Freigabe bleibt nur der Haushalt (Spec 69, R9) zusätzlich zu "Ich"; keine Person.
    [Fact]
    public void Without_any_grant_the_switch_offers_no_person()
    {
        Household(GrantLevels.None);

        var cut = Render<MemberSwitch>();

        cut.WaitForAssertion(() => Assert.Equal(1, HouseholdApiFake.MineCalls));
        cut.WaitForAssertion(() => Assert.Equal(["Ich", "Haushalt"], cut.FindAll("[data-testid='member-switch'] option").Select(o => o.TextContent.Trim()).ToList()));
        Assert.Empty(Member.Sharing);
    }

    // A14: Ohne Haushalt gibt es niemanden zum Umschalten.
    [Fact]
    public void Without_a_household_there_is_no_switch()
    {
        HouseholdApiFake.Household = null;

        var cut = Render<MemberSwitch>();

        cut.WaitForAssertion(() => Assert.Equal(1, HouseholdApiFake.MineCalls));
        Assert.Empty(cut.FindAll("[data-testid='member-switch']"));
    }

    // A13
    [Fact]
    public void With_a_grant_the_switch_offers_me_and_anna_and_choosing_her_makes_her_the_owner()
    {
        Household(PlanView);

        var cut = Render<MemberSwitch>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='member-switch']")));
        var options = cut.FindAll("[data-testid='member-switch'] option").Select(o => o.TextContent.Trim()).ToList();
        Assert.Equal(["Ich", "Anna", "Haushalt"], options);
        Assert.Equal("Wessen Zahlen", cut.Find("[data-testid='member-switch']").GetAttribute("aria-label"));

        cut.Find("[data-testid='member-switch']").Change(FakeHouseholdApi.AnnaId.ToString());

        Assert.Equal(FakeHouseholdApi.AnnaId, Member.Owner);
        Assert.Equal("Anna", Member.Name);
        Assert.True(Member.Allows(GrantAreas.Plan, AccessLevelKeys.View));
        Assert.False(Member.Allows(GrantAreas.Plan, AccessLevelKeys.Create));
        Assert.False(Member.Allows(GrantAreas.Commitments, AccessLevelKeys.View));

        cut.Find("[data-testid='member-switch']").Change("");

        Assert.Null(Member.Owner);
        Assert.True(Member.Allows(GrantAreas.Commitments, AccessLevelKeys.Delete));
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
        Assert.False(Member.HasSwitch);
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
        Assert.True(Member.HasSwitch);
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
