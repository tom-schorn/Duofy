using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Duofy.Web.Tests;

/// <summary>
/// Cloudflare Web Analytics (Spec 79, R8): Mit Duofy:Analytics:Token trägt jede Seite der Web-Variante den Beacon mit
/// diesem Token; er zählt Seitenaufrufe ohne Cookies. Ohne Token gibt es keinen.
/// </summary>
public class AnalyticsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Beacon = "https://static.cloudflareinsights.com/beacon.min.js";

    private readonly WebApplicationFactory<Program> factory;

    public AnalyticsTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    // A7
    [Fact]
    public async Task With_a_token_every_page_carries_the_beacon_with_that_token()
    {
        var page = await PageAsync(token: "0123456789abcdef0123456789abcdef");

        Assert.Contains($"src=\"{Beacon}\"", page);
        Assert.Contains("data-cf-beacon=\"{&quot;token&quot;:&quot;0123456789abcdef0123456789abcdef&quot;}\"", page);
    }

    // A7
    [Fact]
    public async Task Without_a_token_there_is_no_beacon()
    {
        var page = await PageAsync(token: "");

        Assert.DoesNotContain("cloudflareinsights", page);
    }

    // Das Token kommt aus der Umgebung; es kann die Seite trotzdem nicht verändern.
    [Fact]
    public async Task A_token_cannot_break_out_of_the_attribute()
    {
        var page = await PageAsync(token: "x\"><script>alert(1)</script>");

        Assert.DoesNotContain("<script>alert(1)</script>", page);
    }

    private async Task<string> PageAsync(string token)
    {
        using var web = factory.WithWebHostBuilder(builder => builder.UseSetting("Duofy:Analytics:Token", token));
        var client = web.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        return await client.GetStringAsync("/anmelden");
    }
}
