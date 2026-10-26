using Duofy.Domain;

namespace Duofy.Domain.Tests;

/// <summary>Der Link des Administrators zum Zurücksetzen eines Passworts (Spec 53, R4): 24 Stunden, einmal, nur als Hash gespeichert.</summary>
public class PasswordResetLinkTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_link_is_valid_for_24_hours_and_stores_only_the_hash_of_its_random_token()
    {
        var anna = Guid.NewGuid();
        var tom = Guid.NewGuid();

        var (link, token) = PasswordResetLink.Create(anna, tom, Now);
        var (_, other) = PasswordResetLink.Create(anna, tom, Now);

        Assert.NotEqual(Guid.Empty, link.Id);
        Assert.Equal((anna, tom, Now.AddHours(24)), (link.UserId, link.CreatedById, link.ExpiresAt));
        Assert.Equal(43, token.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", token);
        Assert.NotEqual(token, other);
        Assert.Equal(PasswordResetLink.Hash(token), link.TokenHash);
        Assert.DoesNotContain(token, link.TokenHash);
        Assert.Null(link.UsedAt);
    }

    [Fact]
    public void The_hash_is_the_same_for_the_same_token_and_differs_for_another()
    {
        Assert.Equal(PasswordResetLink.Hash("abc"), PasswordResetLink.Hash("abc"));
        Assert.NotEqual(PasswordResetLink.Hash("abc"), PasswordResetLink.Hash("abd"));
        Assert.Equal(64, PasswordResetLink.Hash("abc").Length);
    }

    [Fact]
    public void It_is_usable_until_it_expires_and_only_once()
    {
        var (link, _) = PasswordResetLink.Create(Guid.NewGuid(), Guid.NewGuid(), Now);

        Assert.True(link.IsUsable(Now.AddHours(24).AddTicks(-10)));
        Assert.False(link.IsUsable(Now.AddHours(24)));

        link.Use(Now.AddMinutes(5));

        Assert.Equal(Now.AddMinutes(5), link.UsedAt);
        Assert.False(link.IsUsable(Now.AddMinutes(6)));
    }

    // A13: Offene Links einer Person schließen; benutzte und abgelaufene bleiben, wie sie sind.
    [Fact]
    public void Closing_marks_only_the_open_links_as_used_and_leaves_used_and_expired_ones_as_they_are()
    {
        var anna = Guid.NewGuid();
        var tom = Guid.NewGuid();
        var (open, _) = PasswordResetLink.Create(anna, tom, Now);
        var (used, _) = PasswordResetLink.Create(anna, tom, Now);
        used.Use(Now.AddMinutes(1));
        var (expired, _) = PasswordResetLink.Create(anna, tom, Now.AddDays(-2));
        var later = Now.AddMinutes(10);

        PasswordResetLink.CloseOpen([open, used, expired], later);

        Assert.Equal(later, open.UsedAt);
        Assert.Equal(Now.AddMinutes(1), used.UsedAt);
        Assert.Null(expired.UsedAt);
        Assert.False(open.IsUsable(later));
    }
}
