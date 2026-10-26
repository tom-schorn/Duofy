using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Pages;

namespace Duofy.Shared.Tests;

/// <summary>Die Zielquoten auf der Seite Benutzerkonto (Spec 66, R8).</summary>
public class QuotaSettingsTests : PageTestContext
{
    [Fact]
    public void Quotas_are_prefilled_and_can_be_saved()
    {
        Users.Quotas = new QuotaSettings(50, 30, 20);
        var cut = Render<Account>();
        cut.WaitForAssertion(() => Assert.Equal("50", Field(cut, "account-quota-needs").GetAttribute("value")));

        Field(cut, "account-quota-needs").Change("60");
        Field(cut, "account-quota-wants").Change("20");
        Field(cut, "account-quotas-save").Click();

        cut.WaitForAssertion(() => Assert.Contains("Die Quoten wurden gespeichert.", cut.Find("[data-testid='account-quotas']").TextContent));
        Assert.Equal(new QuotaSettings(60, 20, 20), Assert.Single(Users.QuotaUpdates));
    }

    [Fact]
    public void Quotas_that_do_not_add_up_show_the_rule()
    {
        Users.QuotaError = ErrorCodes.QuotaInvalid;
        var cut = Render<Account>();
        cut.WaitForAssertion(() => Assert.Equal("30", Field(cut, "account-quota-wants").GetAttribute("value")));

        Field(cut, "account-quota-needs").Change("60");
        Field(cut, "account-quotas-save").Click();

        cut.WaitForAssertion(() => Assert.Contains("zusammen 100 ergeben", cut.Find("[data-testid='account-quotas']").TextContent));
    }
}
