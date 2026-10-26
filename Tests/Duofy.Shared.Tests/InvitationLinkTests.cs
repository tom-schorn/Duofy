using Duofy.Shared.Services;

namespace Duofy.Shared.Tests;

/// <summary>Der Link einer Einladung (Spec 67): das Token aus einem eingefügten Link lesen.</summary>
public class InvitationLinkTests
{
    [Theory]
    [InlineData("https://duofy.example/einladung/abc_DEF-123", "abc_DEF-123")]
    [InlineData("  http://192.168.2.10:8080/einladung/xyz/  ", "xyz")]
    [InlineData("duofy.example/einladung/xyz", "xyz")]
    public void A_pasted_link_yields_its_token(string link, string token)
    {
        Assert.Equal(token, InvitationLink.TokenOf(link));
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://duofy.example/haushalt")]
    [InlineData("https://duofy.example/einladung/")]
    [InlineData("https://duofy.example/einladung/a b")]
    [InlineData("https://duofy.example/einladung/abc/weiter")]
    public void Anything_else_is_no_invitation_link(string link)
    {
        Assert.Null(InvitationLink.TokenOf(link));
    }
}
