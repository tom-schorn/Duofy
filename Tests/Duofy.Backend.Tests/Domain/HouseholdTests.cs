using Duofy.Domain;

namespace Duofy.Domain.Tests;

/// <summary>Der Haushalt (Spec 67): Gründen, der Gründer und die Einladungen.</summary>
public class HouseholdTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    // Spec 67, A1
    [Fact]
    public void A_founded_household_without_a_name_is_named_after_the_first_name_with_the_person_as_founder()
    {
        var anna = Guid.NewGuid();

        var household = Household.Found(anna, "Anna", name: null, Now);

        Assert.Equal("Haushalt von Anna", household.Name);
        var member = Assert.Single(household.Members);
        Assert.Equal((anna, HouseholdRole.Founder, Now), (member.UserId, member.Role, member.JoinedAt));
        Assert.Equal(household.Id, member.HouseholdId);
        Assert.NotEqual(Guid.Empty, household.Id);
        Assert.Equal(Quotas.Default, household.Quotas);
    }

    [Fact]
    public void A_given_name_is_used_as_it_is()
    {
        var household = Household.Found(Guid.NewGuid(), "Anna", "Familie Muster", Now);

        Assert.Equal("Familie Muster", household.Name);
    }

    [Fact]
    public void A_long_first_name_is_cut_to_the_length_of_a_household_name()
    {
        var household = Household.Found(Guid.NewGuid(), new string('A', 100), name: null, Now);

        Assert.Equal(HouseholdRules.NameMaxLength, household.Name.Length);
        Assert.StartsWith("Haushalt von AAA", household.Name);
    }

    // Spec 67, A14
    [Fact]
    public void The_next_founder_is_the_earliest_joined_of_the_others()
    {
        var anna = Guid.NewGuid();
        var ben = Guid.NewGuid();
        var clara = Guid.NewGuid();
        var household = Household.Found(anna, "Anna", name: null, Now);
        household.Members.Add(new HouseholdMember { HouseholdId = household.Id, UserId = clara, Role = HouseholdRole.Member, JoinedAt = Now.AddHours(2) });
        household.Members.Add(new HouseholdMember { HouseholdId = household.Id, UserId = ben, Role = HouseholdRole.Member, JoinedAt = Now.AddHours(1) });

        Assert.True(household.IsFounder(anna));
        Assert.False(household.IsFounder(ben));
        Assert.Equal(ben, household.NextFounder(anna)?.UserId);
        Assert.Null(Household.Found(anna, "Anna", name: null, Now).NextFounder(anna));
    }

    // Spec 67, A3
    [Fact]
    public void An_invitation_has_a_url_safe_token_of_256_bits_and_lasts_14_days()
    {
        var household = Guid.NewGuid();
        var anna = Guid.NewGuid();

        var invitation = HouseholdInvitation.Create(household, anna, Now);
        var other = HouseholdInvitation.Create(household, anna, Now);

        Assert.True(invitation.Token.Length >= 43, invitation.Token);
        Assert.Matches("^[A-Za-z0-9_-]+$", invitation.Token);
        Assert.NotEqual(invitation.Token, other.Token);
        Assert.NotEqual(invitation.Id, other.Id);
        Assert.Equal(Now.AddDays(14), invitation.ExpiresAt);
        Assert.Equal((household, anna, InvitationStatus.Pending), (invitation.HouseholdId, invitation.CreatedById, invitation.Status));
        Assert.Equal(InvitationState.Open, invitation.StateAt(Now));
    }

    // Spec 67, A9
    [Fact]
    public void An_invitation_is_expired_from_its_expiry_on()
    {
        var invitation = HouseholdInvitation.Create(Guid.NewGuid(), Guid.NewGuid(), Now);

        Assert.Equal(InvitationState.Open, invitation.StateAt(Now.AddDays(14).AddTicks(-1)));
        Assert.Equal(InvitationState.Expired, invitation.StateAt(Now.AddDays(14)));
    }

    // Spec 67, A3, A7
    [Fact]
    public void A_used_or_revoked_invitation_is_no_longer_open()
    {
        var used = HouseholdInvitation.Create(Guid.NewGuid(), Guid.NewGuid(), Now);
        var revoked = HouseholdInvitation.Create(Guid.NewGuid(), Guid.NewGuid(), Now);
        var ben = Guid.NewGuid();

        used.Accept(ben, Now.AddHours(1));
        revoked.Revoke();

        Assert.Equal((InvitationStatus.Accepted, ben, Now.AddHours(1)), (used.Status, used.UsedById, used.UsedAt));
        Assert.Equal(InvitationState.NotOpen, used.StateAt(Now.AddHours(2)));
        Assert.Equal(InvitationStatus.Revoked, revoked.Status);
        Assert.Equal(InvitationState.NotOpen, revoked.StateAt(Now));
        // Benutzt oder zurückgezogen geht vor abgelaufen: Die Antwort sagt, was zuerst geschah.
        Assert.Equal(InvitationState.NotOpen, revoked.StateAt(Now.AddDays(30)));
    }
}
