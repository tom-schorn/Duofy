using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Duofy.Web.Tests;

/// <summary>
/// Cloudflare Web Analytics (Spec 79, R8; Issue #43): Das Skript von Cloudflare steht nie in der ausgelieferten Seite.
/// Es wird erst im Browser nachgeladen, wenn die Person zugestimmt hat (ConsentBanner, Issue #35); ohne Token
/// gibt es den Dienst gar nicht. Den Baustein selbst prüfen die Tests in Duofy.Shared.Tests.
/// </summary>
public class AnalyticsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public AnalyticsTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    // Auch mit Token liefert der Server das Skript nicht aus: Ohne Zustimmung lädt nichts von Cloudflare.
    [Fact]
    public async Task Even_with_a_token_the_served_page_carries_no_cloudflare_script()
    {
        var page = await PageAsync(token: "0123456789abcdef0123456789abcdef");

        Assert.DoesNotContain("cloudflareinsights", page);
        Assert.DoesNotContain("data-cf-beacon", page);
        Assert.DoesNotContain("0123456789abcdef0123456789abcdef", page);
    }

    // Ohne Token lädt das Skript weiterhin nicht (Abnahmekriterium von Issue #43).
    [Fact]
    public async Task Without_a_token_there_is_no_beacon()
    {
        var page = await PageAsync(token: "");

        Assert.DoesNotContain("cloudflareinsights", page);
        Assert.DoesNotContain("data-cf-beacon", page);
    }

    private async Task<string> PageAsync(string token)
    {
        using var web = factory.WithWebHostBuilder(builder => builder.UseSetting("Duofy:Analytics:Token", token));
        var client = web.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        return await client.GetStringAsync("/anmelden");
    }
}
