using Duofy.Schemas;
using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 125, R2 und R3: Im Haushaltsplan arbeitet Tom für Jasmin, ohne dass die Sicht des Haushalts endet. Der Dienst
/// kennt dafür die Personen, die in einem Bereich mindestens eine Stufe freigegeben haben, und einen Bereich, in dem
/// Dialoge als diese Person laufen.
/// </summary>
public class ActiveMemberActingTests : PageTestContext
{
    private static readonly Guid JasminId = Guid.NewGuid();

    private ActiveMember Member => Services.GetRequiredService<ActiveMember>();

    private static MemberResponse Jasmin(GrantLevels toMe) =>
        new(JasminId, "Jasmin", "Muster", "jasmin@example.org", HouseholdRoles.Member, false, toMe, GrantLevels.None);

    private async Task SetUpAsync(GrantLevels jasminGrantsTom)
    {
        HouseholdApiFake.Household = new HouseholdResponse(Guid.NewGuid(), "Haushalt", 50, 30, 20,
            [FakeHouseholdApi.Anna(isMe: true), Jasmin(jasminGrantsTom)]);
        await Member.LoadAsync();
    }

    [Fact]
    public async Task People_with_a_level_are_me_first_and_then_the_members_whose_grant_reaches_it()
    {
        await SetUpAsync(GrantLevels.None with { Plan = AccessLevelKeys.Create, Book = AccessLevelKeys.View });

        Assert.Equal(["Anna", "Jasmin"], Member.PeopleWith(GrantAreas.Plan, AccessLevelKeys.Create).Select(m => m.FirstName));
        Assert.Equal(["Anna"], Member.PeopleWith(GrantAreas.Book, AccessLevelKeys.Create).Select(m => m.FirstName));
    }

    [Fact]
    public async Task Acting_as_a_member_makes_the_owner_and_the_grants_theirs_until_the_scope_ends_and_leaves_the_household_view()
    {
        await SetUpAsync(GrantLevels.None with { Plan = AccessLevelKeys.Edit });
        Member.ChooseHousehold();
        var changes = 0;
        Member.Changed += () => changes++;

        using (Member.ActAs(JasminId))
        {
            Assert.Equal(JasminId, Member.Owner);
            Assert.Equal("Jasmin", Member.Name);
            Assert.True(Member.Allows(GrantAreas.Plan, AccessLevelKeys.Edit));
            Assert.False(Member.Allows(GrantAreas.Book, AccessLevelKeys.View));
        }

        Assert.Null(Member.Owner);
        Assert.True(Member.IsHousehold);
        Assert.False(Member.Allows(GrantAreas.Plan, AccessLevelKeys.View));
        Assert.Equal(0, changes);
    }

    [Fact]
    public async Task Acting_as_me_means_my_own_data_with_every_right()
    {
        await SetUpAsync(GrantLevels.None);
        Member.ChooseHousehold();

        using (Member.ActAs(FakeHouseholdApi.AnnaId))
        {
            Assert.Null(Member.Owner);
            Assert.True(Member.Allows(GrantAreas.Plan, AccessLevelKeys.Delete));
        }
    }

    [Fact]
    public async Task What_a_member_may_do_for_another_follows_that_members_grants_also_in_the_household_view()
    {
        await SetUpAsync(GrantLevels.None with { Plan = AccessLevelKeys.Edit, Book = AccessLevelKeys.Delete });
        Member.ChooseHousehold();

        Assert.True(Member.AllowsFor(JasminId, GrantAreas.Book, AccessLevelKeys.Edit));
        Assert.False(Member.AllowsFor(JasminId, GrantAreas.Plan, AccessLevelKeys.Delete));
        Assert.True(Member.AllowsFor(FakeHouseholdApi.AnnaId, GrantAreas.Plan, AccessLevelKeys.Delete));
        Assert.False(Member.AllowsFor(Guid.NewGuid(), GrantAreas.Plan, AccessLevelKeys.View));
    }
}
