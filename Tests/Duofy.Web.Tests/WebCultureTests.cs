using System.Globalization;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Duofy.Web.Tests;

/// <summary>Das Image setzt keine Sprache; der Web-Host legt deshalb selbst Deutsch fest (Review von Issue 41).</summary>
public class WebCultureTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public WebCultureTests(WebApplicationFactory<Program> factory) => this.factory = factory;

    [Fact]
    public void The_host_sets_german_as_its_fixed_default_language()
    {
        using var client = factory.CreateClient();

        Assert.Equal("de-DE", CultureInfo.DefaultThreadCurrentCulture?.Name);
        Assert.Equal("de-DE", CultureInfo.DefaultThreadCurrentUICulture?.Name);
    }
}
