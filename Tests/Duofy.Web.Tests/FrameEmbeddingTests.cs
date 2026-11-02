using System.Net;
using Duofy.Web.Session;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Duofy.Web.Tests;

/// <summary>
/// Einbetten in eine fremde Seite (Home Assistant) über eine Liste erlaubter Adressen, Duofy:FrameAncestors.
/// Ohne Liste bleibt alles gesperrt wie bisher; mit Liste dürfen genau diese Seiten die Web-Variante einbetten.
/// </summary>
public class FrameEmbeddingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Allowed = "https://ha.example.org:8123";
    private const string SecondAllowed = "https://home.example.net";
    private const string SignInPage = "/anmelden";

    private readonly WebApplicationFactory<Program> factory;

    public FrameEmbeddingTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    // Die Cookies außerhalb der Entwicklung verlangen https; der Testserver antwortet deshalb unter https.
    private HttpClient Client(string? frameAncestors, string environment = "Production")
    {
        var web = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            if (frameAncestors is not null)
            {
                builder.UseSetting("Duofy:FrameAncestors", frameAncestors);
            }
        });
        return web.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
            BaseAddress = new Uri("https://localhost"),
        });
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(" | ", values) : null;

    private static string Cookie(HttpResponseMessage response, string namePrefix) =>
        Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(namePrefix));

    // --- Ohne Liste: wie bisher ------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Without_a_list_the_page_stays_locked_as_before(string? frameAncestors)
    {
        var response = await Client(frameAncestors).GetAsync(SignInPage);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("SAMEORIGIN", Header(response, "X-Frame-Options"));
        Assert.Equal("frame-ancestors 'self'", Header(response, "Content-Security-Policy"));
    }

    [Fact]
    public async Task Without_a_list_the_cookies_stay_as_before()
    {
        var response = await Client(null).GetAsync(SignInPage);

        var session = Cookie(response, SessionCookie.Name + "=");
        Assert.Contains("samesite=lax", session, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", session, StringComparison.OrdinalIgnoreCase);
        var antiforgery = Cookie(response, ".AspNetCore.Antiforgery.");
        Assert.Contains("samesite=strict", antiforgery, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", antiforgery, StringComparison.OrdinalIgnoreCase);
    }

    // --- Mit Liste -------------------------------------------------------------

    [Fact]
    public async Task With_a_list_the_page_allows_exactly_these_addresses()
    {
        var response = await Client($"{Allowed} {SecondAllowed}").GetAsync(SignInPage);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(Header(response, "X-Frame-Options"));
        Assert.Equal($"frame-ancestors 'self' {Allowed} {SecondAllowed}", Header(response, "Content-Security-Policy"));
    }

    // Ohne X-Frame-Options schützt nur noch frame-ancestors: Jede Antwort braucht es, nicht nur die Seiten mit Blazor.
    [Theory]
    [InlineData("/gibt-es-nicht")]
    [InlineData("/health")]
    [InlineData("/_framework/blazor.web.js")]
    public async Task With_a_list_other_responses_are_not_open_to_every_site(string address)
    {
        var response = await Client(Allowed).GetAsync(address);

        Assert.Equal($"frame-ancestors 'self' {Allowed}", Header(response, "Content-Security-Policy"));
    }

    [Fact]
    public async Task With_a_list_the_interactive_connection_carries_the_same_policy()
    {
        var response = await Client(Allowed).PostAsync("/_blazor/negotiate?negotiateVersion=1", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"frame-ancestors 'self' {Allowed}", Header(response, "Content-Security-Policy"));
    }

    [Fact]
    public async Task A_foreign_address_is_not_in_the_header()
    {
        var response = await Client(Allowed).GetAsync(SignInPage);

        Assert.DoesNotContain("evil.example", Header(response, "Content-Security-Policy"));
        Assert.DoesNotContain("*", Header(response, "Content-Security-Policy"));
    }

    [Fact]
    public async Task With_a_list_the_cookies_work_across_sites_and_stay_secure()
    {
        var response = await Client(Allowed).GetAsync(SignInPage);

        foreach (var cookie in new[] { Cookie(response, SessionCookie.Name + "="), Cookie(response, ".AspNetCore.Antiforgery.") })
        {
            Assert.Contains("samesite=none", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        }
    }

    // SameSite=None ohne Secure verwirft der Browser; deshalb auch in der Entwicklung.
    [Fact]
    public async Task With_a_list_the_cookies_stay_secure_in_development()
    {
        var response = await Client("http://127.0.0.1:9000", environment: "Development").GetAsync(SignInPage);

        foreach (var cookie in new[] { Cookie(response, SessionCookie.DevelopmentName + "="), Cookie(response, ".AspNetCore.Antiforgery.") })
        {
            Assert.Contains("samesite=none", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        }
    }

    // Hinter einem Proxy, der die Verschlüsselung übernimmt, kommt hier http an; die Seite muss trotzdem antworten.
    [Fact]
    public async Task With_a_list_the_page_answers_behind_a_proxy_that_terminates_https()
    {
        var web = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Duofy:FrameAncestors", Allowed);
        });
        var client = web.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

        var response = await client.GetAsync(SignInPage);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("secure", Cookie(response, ".AspNetCore.Antiforgery."), StringComparison.OrdinalIgnoreCase);
    }

    // --- Ungültige Werte -------------------------------------------------------

    [Theory]
    [InlineData("*")]
    [InlineData("https://*.example.org")]
    [InlineData("http://ha.example.org")]
    [InlineData("ha.example.org")]
    [InlineData("https://ha.example.org/")]
    [InlineData("https://ha.example.org/lovelace")]
    [InlineData("https://ha.example.org?a=1")]
    [InlineData("https://ha.example.org#a")]
    [InlineData("https://nutzer@ha.example.org")]
    [InlineData("https://ha.example.org:99999")]
    [InlineData("https://ha.example.org,https://home.example.net")]
    [InlineData("https://ha.example.org;script-src")]
    [InlineData("'self'")]
    [InlineData("\"https://ha.example.org\"")]
    [InlineData("'none'")]
    [InlineData("https://ha.example.org https://home.example.net/pfad")]
    [InlineData("ftp://ha.example.org")]
    [InlineData("https://")]
    [InlineData("https://-ha.example.org")]
    [InlineData("https://[::1]:8123")]
    [InlineData("https://hä.example.org")]
    public void An_invalid_value_stops_the_start_with_a_clear_message(string value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => Client(value));

        Assert.Contains("Duofy:FrameAncestors", error.Message);
        Assert.Contains(".env.example", error.Message);
    }

    // Zeilenumbrüche trennen nur Adressen; sie dürfen nie als eigene Kopfzeile im Header landen.
    [Fact]
    public async Task A_line_break_in_the_value_cannot_inject_a_header()
    {
        var error = Record.Exception(() => Client($"{Allowed}\r\nX-Evil: 1"));
        Assert.IsType<InvalidOperationException>(error);

        var response = await Client($"{Allowed}\r\n{SecondAllowed}").GetAsync(SignInPage);
        Assert.Equal($"frame-ancestors 'self' {Allowed} {SecondAllowed}", Header(response, "Content-Security-Policy"));
        Assert.Null(Header(response, "X-Evil"));
    }

    [Fact]
    public async Task Http_is_accepted_only_in_development()
    {
        var response = await Client("http://127.0.0.1:9000", environment: "Development").GetAsync(SignInPage);

        Assert.Equal("frame-ancestors 'self' http://127.0.0.1:9000", Header(response, "Content-Security-Policy"));
        Assert.Throws<InvalidOperationException>(() => Client("http://127.0.0.1:9000"));
    }
}
