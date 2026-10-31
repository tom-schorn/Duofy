using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Duofy.Web.Tests;

/// <summary>
/// Spec 11: Die Testseite des Schritt-Dialogs ist nur im Entwicklungsbetrieb erreichbar, in Produktion nicht.
/// </summary>
public class StepDialogPageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public StepDialogPageTests(WebApplicationFactory<Program> factory) => this.factory = factory;

    private HttpClient ClientFor(string environment) =>
        factory.WithWebHostBuilder(builder => builder.UseEnvironment(environment))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task Test_page_offers_the_example_dialog_in_development()
    {
        var response = await ClientFor("Development").GetAsync("/schritt-dialog");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("step-dialog-demo-open", html);
    }

    [Fact]
    public async Task Test_page_is_not_found_in_production()
    {
        var response = await ClientFor("Production").GetAsync("/schritt-dialog");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("step-dialog-demo-open", html);
    }
}
