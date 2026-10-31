using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Duofy.Web.Tests;

/// <summary>
/// Spec 6, A5: Die Testseite der Bausteine ist nur im Entwicklungsbetrieb erreichbar, in Produktion nicht.
/// </summary>
public class BuildingBlocksPageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public BuildingBlocksPageTests(WebApplicationFactory<Program> factory) => this.factory = factory;

    private HttpClient ClientFor(string environment) =>
        factory.WithWebHostBuilder(builder => builder.UseEnvironment(environment))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task Test_page_shows_both_panels_in_development()
    {
        var response = await ClientFor("Development").GetAsync("/bausteine");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("blocks-panel-light", html);
        Assert.Contains("blocks-panel-dark", html);
    }

    [Fact]
    public async Task Test_page_is_not_found_in_production()
    {
        var response = await ClientFor("Production").GetAsync("/bausteine");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("blocks-panel", html);
    }
}
