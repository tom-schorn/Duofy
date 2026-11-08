using System.Net.Http.Json;
using Duofy.Schemas;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duofy.Api.Tests;

/// <summary>Die optionalen äußeren Adressen der Rechtstexte (Spec 109, R3): Duofy:ImprintUrl, Duofy:PrivacyUrl, Duofy:TermsUrl.</summary>
[Collection(ApiCollection.Name)]
public sealed class LegalEndpointTests(ApiFixture fixture) : IAsyncLifetime
{
    private readonly List<WebApplicationFactory<Program>> factories = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var factory in factories)
        {
            await factory.DisposeAsync();
        }
    }

    private WebApplicationFactory<Program> Factory(string? imprint = null, string? privacy = null, string? terms = null, CapturedLogs? logs = null)
    {
        var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Duofy:ImprintFile", "");
            builder.UseSetting("Duofy:PrivacyFile", "");
            builder.UseSetting("Duofy:TermsFile", "");
            builder.UseSetting("Duofy:ImprintUrl", imprint ?? "");
            builder.UseSetting("Duofy:PrivacyUrl", privacy ?? "");
            builder.UseSetting("Duofy:TermsUrl", terms ?? "");
            if (logs is not null)
            {
                builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logs));
            }
        });
        factories.Add(factory);
        return factory;
    }

    // A3
    [Fact]
    public async Task A3_external_addresses_are_listed()
    {
        var client = Factory(imprint: "https://example.org/impressum", terms: "https://example.org/agb").CreateClient();

        var list = await client.GetFromJsonAsync<LegalListResponse>("/api/v1/legal");

        // Ein Text mit Adresse gilt als vorhanden, auch ohne Datei.
        Assert.Equal(["imprint", "terms"], list!.Documents);
        Assert.Equal("https://example.org/impressum", list.Urls!["imprint"]);
        Assert.Equal("https://example.org/agb", list.Urls["terms"]);
        Assert.False(list.Urls.ContainsKey("privacy"));
    }

    [Fact]
    public async Task Without_addresses_the_list_carries_none()
    {
        var client = Factory().CreateClient();

        var list = await client.GetFromJsonAsync<LegalListResponse>("/api/v1/legal");

        Assert.Empty(list!.Documents);
        Assert.True(list.Urls is null or { Count: 0 });
    }

    [Fact]
    public async Task The_instance_names_a_document_that_has_only_an_address()
    {
        var client = Factory(privacy: "https://example.org/datenschutz").CreateClient();

        var instance = await client.GetFromJsonAsync<InstanceResponse>("/api/v1/instance");

        Assert.Equal(["privacy"], instance!.LegalDocuments);
    }

    // A4
    [Theory]
    [InlineData("Duofy:ImprintUrl", "http://example.org/impressum")]
    [InlineData("Duofy:PrivacyUrl", "javascript:alert(1)")]
    [InlineData("Duofy:TermsUrl", "/nutzungsbedingungen")]
    [InlineData("Duofy:TermsUrl", "https://")]
    [InlineData("Duofy:ImprintUrl", "plagevo.de/impressum")]
    public void A4_an_invalid_address_stops_the_start(string setting, string value)
    {
        var logs = new CapturedLogs();
        var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(setting, value);
            builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logs));
        });
        factories.Add(factory);

        var start = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(start);
        Assert.Contains(setting, start.ToString());
        Assert.Contains(logs.Messages("Duofy.Api.Legal"), m => m.Contains(setting));
    }
}
