using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Web.Tests;

/// <summary>
/// Die Web-Variante hinter dem Cloudflare Tunnel (Spec 79, R4): Sie nimmt die Adresse der Person aus CF-Connecting-IP
/// und das Schema aus X-Forwarded-Proto, aber nur von einer Verbindung aus einem vertrauten Netz (Duofy:TrustedProxies).
/// </summary>
public class ForwardedClientTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Tunnel = "172.30.10.2";

    private readonly WebApplicationFactory<Program> factory;

    public ForwardedClientTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    // A2
    [Fact]
    public async Task Behind_the_tunnel_the_web_sees_the_client_address_and_https()
    {
        var seen = await SeenAsync(trustedProxies: "172.30.10.2/32", from: Tunnel);

        Assert.Equal("203.0.113.1 https", seen);
    }

    // A2: Ein gefälschter Kopf von außerhalb des vertrauten Netzes zählt nicht.
    [Fact]
    public async Task A_forged_header_from_outside_the_trusted_network_is_ignored()
    {
        var seen = await SeenAsync(trustedProxies: "172.30.10.2/32", from: "198.51.100.7");

        Assert.Equal("198.51.100.7 http", seen);
    }

    [Fact]
    public async Task Without_trusted_networks_the_header_is_ignored()
    {
        var seen = await SeenAsync(trustedProxies: "", from: Tunnel);

        Assert.Equal(Tunnel + " http", seen);
    }

    // Was die Web-Variante als Absender und Schema der Anfrage sieht.
    private async Task<string> SeenAsync(string trustedProxies, string from)
    {
        using var web = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Duofy:TrustedProxies", trustedProxies);
            builder.ConfigureServices(services => services.AddTransient<IStartupFilter, Probe>());
        });
        var client = web.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        var request = new HttpRequestMessage(HttpMethod.Get, "/test/absender");
        request.Headers.Add(Probe.RemoteHeader, from);
        request.Headers.Add("CF-Connecting-IP", "203.0.113.1");
        request.Headers.Add("X-Forwarded-Proto", "https");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Setzt vor allem anderen die Absenderadresse aus einem Kopf (der Testserver kennt keine) und hängt hinter die
    /// Pipeline der Anwendung eine Adresse, die Absender und Schema zurückgibt.
    /// </summary>
    private sealed class Probe : IStartupFilter
    {
        public const string RemoteHeader = "X-Test-Remote-Address";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(context.Request.Headers[RemoteHeader].ToString());
                await nextMiddleware(context);
            });
            next(app);
            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Path == "/test/absender")
                {
                    await context.Response.WriteAsync($"{context.Connection.RemoteIpAddress} {context.Request.Scheme}");
                    return;
                }
                await nextMiddleware(context);
            });
        };
    }
}
