using Duofy.Shared.Services;

namespace Duofy.Shared.Tests;

/// <summary>Der Rücksprung nach Anmelden oder Registrieren führt nur auf Seiten von Duofy.</summary>
public class ReturnAddressTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/einladung/abc_DEF-1")]
    [InlineData("/konto/loeschen?x=1")]
    public void Local_paths_are_allowed(string url)
    {
        Assert.True(ReturnAddress.IsLocalPath(url));
    }

    // Browser entfernen Tabs und Zeilenumbrüche aus Adressen; "/\t/evil.example" würde so zu "//evil.example".
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("konto")]
    [InlineData("https://evil.example/")]
    [InlineData("//evil.example/")]
    [InlineData("/\evil.example")]
    [InlineData("/\t/evil.example")]
    [InlineData("/\n/evil.example")]
    [InlineData("/\r/evil.example")]
    [InlineData("/ /evil.example")]
    public void Everything_else_is_refused(string? url)
    {
        Assert.False(ReturnAddress.IsLocalPath(url));
        Assert.Equal("/anmelden", ReturnAddress.With("/anmelden", url));
    }
}
