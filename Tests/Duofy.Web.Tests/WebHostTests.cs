using System.Net;
using Duofy.ApiClient;
using Duofy.Web.Session;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Web.Tests;

/// <summary>
/// Die Web-Variante ohne Datenbank: Die Tokens liegen in der Sitzung auf dem
/// Server, der Browser hält nur ein HttpOnly-Cookie mit der Kennung der Sitzung.
/// </summary>
public class WebHostTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public WebHostTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddTransient<IStartupFilter, TestEndpoints>()));
    }

    private HttpClient Client() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    // A13 – braucht die Seite /konto/loeschen aus Duofy.Shared (mia); bis dahin rot.
    [Fact]
    public async Task Delete_account_page_without_session_redirects_to_sign_in()
    {
        var response = await Client().GetAsync("/konto/loeschen");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/anmelden?returnUrl=%2Fkonto%2Floeschen", response.Headers.Location?.OriginalString);
    }

    // Spec 53: Die Links aus Mails und vom Administrator öffnet oft jemand, der nicht angemeldet ist.
    [Theory]
    [InlineData("/bestaetigen?id=00000000-0000-0000-0000-000000000001&token=abc")]
    [InlineData("/passwort-neu?link=abc")]
    [InlineData("/passwort-neu?email=anna%40example.org&token=abc")]
    [InlineData("/email-bestaetigen?id=00000000-0000-0000-0000-000000000001&email=neu%40example.org&token=abc")]
    public async Task The_pages_behind_mailed_links_open_without_sign_in(string address)
    {
        var response = await Client().GetAsync(address);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Spec 79, A1: Der HEALTHCHECK des Containers fragt hier, ohne Anmeldung.
    [Fact]
    public async Task Health_answers_without_sign_in()
    {
        var response = await Client().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Protected_address_without_sign_in_redirects_with_its_path_and_query()
    {
        var response = await Client().GetAsync("/test/geschuetzt?monat=3");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/anmelden?returnUrl=%2Ftest%2Fgeschuetzt%3Fmonat%3D3", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task First_visit_gets_an_http_only_session_cookie()
    {
        var response = await Client().GetAsync("/not-found");

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(SessionCookie.DevelopmentName + "="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Outside_development_the_session_cookie_is_secure_and_host_only()
    {
        using var production = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        var client = production.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

        var response = await client.GetAsync("/not-found");

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(SessionCookie.Name + "="));
        Assert.StartsWith("__Host-", cookie);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Known_session_cookie_is_not_replaced()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/not-found");
        request.Headers.Add("Cookie", $"{SessionCookie.DevelopmentName}={SessionCookie.NewId()}");

        var response = await Client().SendAsync(request);

        Assert.False(response.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(c => c.StartsWith(SessionCookie.DevelopmentName + "=")));
    }

    // Kommt die Verbindung der interaktiven Seiten ohne Cookie, hat der Browser das
    // Cookie der Seite nicht angenommen. Eine neue Kennung nur für diese Verbindung
    // würde die Anmeldung in einer Sitzung ablegen, die der Browser nie wieder findet.
    [Fact]
    public async Task Interactive_connection_without_cookie_gets_no_new_session()
    {
        var response = await Client().PostAsync("/_blazor/negotiate?negotiateVersion=1", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(c => c.StartsWith(SessionCookie.DevelopmentName + "=")));
    }

    [Fact]
    public async Task Storing_tokens_without_a_session_asks_for_cookies()
    {
        using var scope = factory.Services.CreateScope();

        var error = await Assert.ThrowsAsync<TokenStoreUnavailableException>(
            () => scope.ServiceProvider.GetRequiredService<ITokenStore>().SaveAsync(new AuthTokens("access", "refresh")));

        Assert.Equal(ClientErrorCodes.CookiesRequired, error.ErrorCode);
    }

    [Fact]
    public async Task Session_with_tokens_is_signed_in_and_without_tokens_is_not()
    {
        var signedIn = SessionCookie.NewId();
        var anonymous = SessionCookie.NewId();
        await SaveTokensAsync(signedIn, new AuthTokens("access", "refresh"));

        Assert.Equal("ja", await WhoAsync(signedIn));
        Assert.Equal("nein", await WhoAsync(anonymous));
    }

    [Fact]
    public async Task Token_store_keeps_tokens_per_session()
    {
        var anna = SessionCookie.NewId();
        var ben = SessionCookie.NewId();

        await SaveTokensAsync(anna, new AuthTokens("anna-access", "anna-refresh"));

        Assert.Equal(new AuthTokens("anna-access", "anna-refresh"), await LoadTokensAsync(anna));
        Assert.Null(await LoadTokensAsync(ben));

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<WebSession>().Id = anna;
        await scope.ServiceProvider.GetRequiredService<ITokenStore>().ClearAsync();
        Assert.Null(await LoadTokensAsync(anna));
    }

    [Fact]
    public async Task Commitments_page_without_sign_in_redirects_to_sign_in()
    {
        var response = await Client().GetAsync("/vertraege");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/anmelden?returnUrl=%2Fvertraege", response.Headers.Location?.OriginalString);
    }

    private async Task SaveTokensAsync(string sessionId, AuthTokens tokens)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<WebSession>().Id = sessionId;
        await scope.ServiceProvider.GetRequiredService<ITokenStore>().SaveAsync(tokens);
    }

    private async Task<AuthTokens?> LoadTokensAsync(string sessionId)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<WebSession>().Id = sessionId;
        return await scope.ServiceProvider.GetRequiredService<ITokenStore>().GetAsync();
    }

    private async Task<string> WhoAsync(string sessionId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/test/angemeldet");
        request.Headers.Add("Cookie", $"{SessionCookie.DevelopmentName}={sessionId}");
        return await (await Client().SendAsync(request)).Content.ReadAsStringAsync();
    }

    /// <summary>Zwei Adressen nur für die Tests, vor die eigentliche Anwendung gesetzt.</summary>
    private sealed class TestEndpoints : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                switch (context.Request.Path.Value)
                {
                    case "/test/geschuetzt":
                        await context.ChallengeAsync();
                        return;
                    case "/test/angemeldet":
                        var result = await context.AuthenticateAsync();
                        await context.Response.WriteAsync(result.Principal?.Identity?.IsAuthenticated == true ? "ja" : "nein");
                        return;
                    default:
                        await nextMiddleware(context);
                        return;
                }
            });
            next(app);
        };
    }
}
