using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Api.Tests;

/// <summary>
/// Die echte Adresse der Person hinter dem Cloudflare Tunnel (Spec 79, R4, A2): Die API nimmt sie aus
/// CF-Connecting-IP, aber nur, wenn die Anfrage aus einem vertrauten Netz kommt (Duofy:TrustedProxies). Die Bremse
/// für die Anmeldung zählt dann je Person und nicht für alle zusammen.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ForwardedClientTests(ApiFixture fixture)
{
    private const string Tunnel = "172.30.10.2";
    private const string Web = "172.30.10.12";
    private const string Outside = "198.51.100.7";

    // A2
    [Fact]
    public async Task Two_clients_behind_the_tunnel_have_separate_budgets()
    {
        await using var api = new ProxiedApiFactory(fixture.ConnectionString, trustedProxies: "172.30.10.2/32");
        var client = api.CreateClient();

        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await LoginAsync(client, Tunnel, "203.0.113.1")).StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await LoginAsync(client, Tunnel, "203.0.113.1")).StatusCode);
        var third = await LoginAsync(client, Tunnel, "203.0.113.1");
        var other = await LoginAsync(client, Tunnel, "203.0.113.2");

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, other.StatusCode);
    }

    // A2: Von außerhalb des vertrauten Netzes kann sich niemand mit einem gefälschten Kopf ein neues Budget holen.
    [Fact]
    public async Task A_forged_header_from_outside_the_trusted_network_is_ignored()
    {
        await using var api = new ProxiedApiFactory(fixture.ConnectionString, trustedProxies: "172.30.10.2/32");
        var client = api.CreateClient();

        await LoginAsync(client, Outside, "203.0.113.1");
        await LoginAsync(client, Outside, "203.0.113.2");
        var third = await LoginAsync(client, Outside, "203.0.113.3");

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    // Ein selbst betriebener Server ohne Tunnel vertraut niemandem.
    [Fact]
    public async Task Without_trusted_networks_the_header_is_ignored()
    {
        await using var api = new ProxiedApiFactory(fixture.ConnectionString, trustedProxies: "");
        var client = api.CreateClient();

        await LoginAsync(client, Tunnel, "203.0.113.1");
        await LoginAsync(client, Tunnel, "203.0.113.2");
        var third = await LoginAsync(client, Tunnel, "203.0.113.3");

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    // Amendment 2026-10-26: Die Web-Variante ruft die API von Server zu Server; ihr Container ist von der Bremse
    // ausgenommen, sonst teilten sich alle Web-Nutzer ein Budget. Vor ihr bremst Cloudflare je Person.
    [Fact]
    public async Task Requests_from_the_exempt_web_container_are_not_limited()
    {
        await using var api = new ProxiedApiFactory(fixture.ConnectionString, trustedProxies: "172.30.10.2/32", exempt: Web);
        var client = api.CreateClient();

        for (var request = 0; request < 3; request++)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await LoginAsync(client, Web, clientAddress: null)).StatusCode);
        }
    }

    // Wer durch den Tunnel die Adresse des Web-Containers als seine ausgibt, bleibt gebremst: Ausgenommen ist nur die
    // Verbindung selbst, nicht die weitergereichte Adresse.
    [Fact]
    public async Task A_client_claiming_the_web_container_address_through_the_tunnel_is_still_limited()
    {
        await using var api = new ProxiedApiFactory(fixture.ConnectionString, trustedProxies: "172.30.10.2/32", exempt: Web);
        var client = api.CreateClient();

        await LoginAsync(client, Tunnel, Web);
        await LoginAsync(client, Tunnel, Web);
        var third = await LoginAsync(client, Tunnel, Web);

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    [Fact]
    public async Task Without_exempt_addresses_the_web_container_is_limited_like_everybody()
    {
        await using var api = new ProxiedApiFactory(fixture.ConnectionString, trustedProxies: "172.30.10.2/32");
        var client = api.CreateClient();

        await LoginAsync(client, Web, clientAddress: null);
        await LoginAsync(client, Web, clientAddress: null);
        var third = await LoginAsync(client, Web, clientAddress: null);

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string from, string? clientAddress)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest("niemand@example.org", "falsch-falsch")),
        };
        request.Headers.Add(RemoteAddress.Header, from);
        if (clientAddress is not null)
        {
            request.Headers.Add("CF-Connecting-IP", clientAddress);
        }
        return client.SendAsync(request);
    }

    /// <summary>Eine API mit einer Bremse von zwei Anfragen pro Minute, den vertrauten Netzen und den ausgenommenen Adressen.</summary>
    private sealed class ProxiedApiFactory(string connectionString, string trustedProxies, string exempt = "")
        : DuofyApiFactory(connectionString, authPermitLimit: 2, new Dictionary<string, string>
        {
            ["Duofy:TrustedProxies"] = trustedProxies,
            ["RateLimiting:ExemptAddresses"] = exempt,
        })
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services => services.AddTransient<IStartupFilter, RemoteAddress>());
        }
    }

    /// <summary>
    /// Der Testserver kennt keine Absenderadresse. Dieser Filter setzt sie vor allem anderen aus einem Kopf, als käme
    /// die Anfrage von dort.
    /// </summary>
    private sealed class RemoteAddress : IStartupFilter
    {
        public const string Header = "X-Test-Remote-Address";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(context.Request.Headers[Header].ToString());
                await nextMiddleware(context);
            });
            next(app);
        };
    }
}
