using Duofy.Shared.Services;
using Duofy.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Duofy.Web.Tests;

/// <summary>Teilen im Browser (Spec 67, A17): Der Web-Host kopiert den Link in die Zwischenablage.</summary>
public class ShareServiceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public ShareServiceTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public void The_web_host_shares_by_copying()
    {
        using var scope = factory.Services.CreateScope();

        Assert.IsType<ClipboardShareService>(scope.ServiceProvider.GetRequiredService<IShareService>());
    }

    [Fact]
    public async Task Sharing_writes_the_link_to_the_clipboard()
    {
        var js = new RecordingJsRuntime();

        await new ClipboardShareService(js).ShareAsync("Einladung in Haushalt von Anna", "https://duofy.example/einladung/abc");

        var call = Assert.Single(js.Calls);
        Assert.Equal("navigator.clipboard.writeText", call.Identifier);
        Assert.Equal(["https://duofy.example/einladung/abc"], call.Args);
    }

    [Fact]
    public async Task The_invitation_page_opens_without_sign_in()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

        var response = await client.GetAsync("/einladung/abc");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class RecordingJsRuntime : IJSRuntime
    {
        public List<(string Identifier, object?[] Args)> Calls { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Calls.Add((identifier, args ?? []));
            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }
}
