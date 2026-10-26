using Duofy.Domain;

namespace Duofy.Domain.Tests;

/// <summary>Die Einladung zur Registrierung auf dem Server (Spec 70, R4): sieben Tage, einmal, auf Wunsch nur für eine Adresse.</summary>
public class RegistrationInvitationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_invitation_is_valid_for_seven_days_and_has_a_random_url_safe_token()
    {
        var anna = Guid.NewGuid();

        var invitation = RegistrationInvitation.Create(anna, email: null, Now);
        var other = RegistrationInvitation.Create(anna, email: null, Now);

        Assert.NotEqual(Guid.Empty, invitation.Id);
        Assert.Equal((anna, Now, Now.AddDays(7)), (invitation.CreatedById, invitation.CreatedAt, invitation.ExpiresAt));
        Assert.Equal(43, invitation.Token.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", invitation.Token);
        Assert.NotEqual(invitation.Token, other.Token);
        Assert.Null(invitation.Email);
        Assert.Null(invitation.UsedAt);
    }

    [Fact]
    public void The_email_is_stored_trimmed_in_lower_case_and_an_empty_one_counts_as_none()
    {
        Assert.Equal("ben@example.de", RegistrationInvitation.Create(Guid.NewGuid(), "  Ben@Example.DE ", Now).Email);
        Assert.Null(RegistrationInvitation.Create(Guid.NewGuid(), "   ", Now).Email);
    }

    [Fact]
    public void Without_an_email_it_admits_every_address_until_it_expires()
    {
        var invitation = RegistrationInvitation.Create(Guid.NewGuid(), email: null, Now);

        Assert.True(invitation.Admits("clara@example.de", Now.AddDays(7).AddTicks(-10)));
        Assert.False(invitation.Admits("clara@example.de", Now.AddDays(7)));
    }

    [Fact]
    public void With_an_email_it_admits_only_that_address_in_any_case()
    {
        var invitation = RegistrationInvitation.Create(Guid.NewGuid(), "ben@example.de", Now);

        Assert.True(invitation.Admits("BEN@example.de", Now));
        Assert.False(invitation.Admits("clara@example.de", Now));
    }

    [Fact]
    public void A_used_invitation_admits_nobody_any_more()
    {
        var invitation = RegistrationInvitation.Create(Guid.NewGuid(), email: null, Now);
        var ben = Guid.NewGuid();

        invitation.Use(ben, Now.AddHours(1));

        Assert.Equal((ben, Now.AddHours(1)), (invitation.UsedById, invitation.UsedAt));
        Assert.False(invitation.Admits("clara@example.de", Now.AddHours(2)));
    }
}
