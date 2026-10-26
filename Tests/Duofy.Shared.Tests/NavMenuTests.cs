using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Layout;

namespace Duofy.Shared.Tests;

/// <summary>Die Navigation für angemeldete Personen führt zu Plan, Konten, Buchungen und Verträgen.</summary>
public class NavMenuTests : PageTestContext
{
    [Fact]
    public void Signed_in_person_finds_the_accounts_and_the_bookings()
    {
        var cut = Render<NavMenu>();

        var links = cut.FindAll("a").Select(a => (Href: a.GetAttribute("href"), Text: a.TextContent.Trim())).ToList();
        Assert.Contains(("/konten", "Konten"), links);
        Assert.Contains(("/buchungen", "Buchungen"), links);
    }

    [Fact]
    public void Signed_in_person_finds_the_contracts()
    {
        var cut = Render<NavMenu>();

        var links = cut.FindAll("a").Select(a => (Href: a.GetAttribute("href"), Text: a.TextContent.Trim())).ToList();
        Assert.Contains(("/vertraege", "Verträge"), links);
    }

    // Spec 66: Ohne Plan-Eintrag kommt man im Browser nicht zum Monatsplan und damit zu keiner Buchung.
    [Fact]
    public void Signed_in_person_finds_the_plan_right_after_start()
    {
        var cut = Render<NavMenu>();

        var links = cut.FindAll("a").Select(a => (Href: a.GetAttribute("href"), Text: a.TextContent.Trim())).ToList();
        Assert.Equal([("/", "Start"), ("/plan", "Plan")], links.Take(2));
    }

    // Spec 68, R8: Im Browser steht der Umschalter oben in der Navigation, sobald jemand etwas teilt.
    [Fact]
    public void The_member_switch_sits_at_the_top_of_the_navigation_when_somebody_shares()
    {
        HouseholdApiFake.Household = HouseholdApiFake.Household! with
        {
            Members = [FakeHouseholdApi.Anna(), FakeHouseholdApi.Ben() with { GrantsToMe = GrantLevels.None with { Book = AccessLevelKeys.View } }],
        };

        var cut = Render<NavMenu>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='member-switch']")));
        Assert.Equal(["Ich", "Ben", "Haushalt"], cut.FindAll("[data-testid='member-switch'] option").Select(o => o.TextContent.Trim()));
    }
}
