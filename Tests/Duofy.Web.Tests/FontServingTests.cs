using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Duofy.Web.Tests;

/// <summary>Spec 2, A1 und A2: Der eigene Server liefert Inter aus, die Startseite verweist auf keinen anderen Host.</summary>
public class FontServingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public FontServingTests(WebApplicationFactory<Program> factory) => this.factory = factory;

    private HttpClient Client() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task Server_serves_the_inter_font_file_itself()
    {
        var response = await Client().GetAsync("/_content/Duofy.Shared/fonts/inter-latin-wght-normal.woff2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.Content.ReadAsByteArrayAsync()).Length > 10_000);
    }

    [Fact]
    public async Task Start_page_references_only_its_own_host()
    {
        var html = await (await Client().GetAsync("/anmelden")).Content.ReadAsStringAsync();

        var external = System.Text.RegularExpressions.Regex
            .Matches(html, @"(?:src|href)=""(https?:)?//[^""]+""")
            .Select(m => m.Value)
            .ToList();
        Assert.True(external.Count == 0, "Fremde Adressen: " + string.Join(", ", external));
    }
}
