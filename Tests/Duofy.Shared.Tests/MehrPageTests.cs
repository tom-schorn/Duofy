using Bunit;
using Duofy.Shared.Pages;

namespace Duofy.Shared.Tests;

/// <summary>
/// Die Seite Mehr (R14): jeder Eintrag führt zum selben Ziel wie vorher, Admin nur für Verwaltende. Seit Spec 82 R12 hat sie
/// keinen Haushalt-Eintrag mehr (der Haushalt hat sein Ziel unten), dafür oben das Profil mit der Auswahl der Person.
/// </summary>
public class MehrPageTests : PageTestContext
{
    [Fact]
    public void Every_entry_leads_to_the_same_target()
    {
        LegalApiFake.Documents["imprint"] = "Max Muster";
        Users.Me = Users.Me with { IsInstanceAdmin = true };

        var cut = Render<Mehr>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='more-admin']")));
        Assert.Equal("/konten", cut.Find("[data-testid='more-accounts']").GetAttribute("href"));
        Assert.Equal("/vertraege", cut.Find("[data-testid='more-contracts']").GetAttribute("href"));
        Assert.Empty(cut.FindAll("[data-testid='more-household']"));
        Assert.Equal("/konto", cut.Find("[data-testid='more-user-account']").GetAttribute("href"));
        Assert.Equal("/admin", cut.Find("[data-testid='more-admin']").GetAttribute("href"));
        Assert.Equal("/lizenzen", cut.Find("[data-testid='more-licenses']").GetAttribute("href"));
        Assert.Equal("/rechtliches/impressum", cut.Find("[data-testid='more-legal-imprint']").GetAttribute("href"));
        Assert.Contains("card", cut.Find("[data-testid='more-list']").ClassList);
    }

    [Fact]
    public void Sign_out_leads_to_the_sign_in()
    {
        var cut = Render<Mehr>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='more-sign-out']")));

        cut.Find("[data-testid='more-sign-out']").Click();

        cut.WaitForAssertion(() => Assert.Equal("/anmelden", CurrentPath));
        Assert.Null(Tokens.Tokens);
    }
}
