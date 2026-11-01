using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Duofy.Web.Tests;

/// <summary>
/// Name und Logo der Web-Variante (Issue 8): Der Host liest dieselbe Einstellung wie die API (Duofy:Hosted) und dazu
/// die eigene Duofy:AppName; die Anmeldeseite und das Symbol im Tab folgen daraus.
/// </summary>
public class BrandHostTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public BrandHostTests(WebApplicationFactory<Program> factory) => this.factory = factory;

    private async Task<string> SignInPageAsync(params (string Key, string Value)[] settings)
    {
        using var configured = factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });
        using var client = configured.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        // Der Titel steht mit Zeichenverweisen im Kopf (zur&#xFC;ck); für den Vergleich in Klartext.
        return System.Net.WebUtility.HtmlDecode(await client.GetStringAsync("/anmelden"));
    }

    [Fact]
    public async Task Without_a_setting_the_sign_in_page_is_duofy_with_the_old_icon()
    {
        var html = await SignInPageAsync();

        Assert.Contains("Willkommen zurück · Duofy", html);
        Assert.Contains("_content/Duofy.Shared/favicon.png", html);
        Assert.DoesNotContain("plagevo-wortmarke", html);
    }

    [Fact]
    public async Task On_the_hosted_instance_the_sign_in_page_is_plagevo_with_logo_title_and_icons_for_both_modes()
    {
        var html = await SignInPageAsync(("Duofy:Hosted", "true"));

        Assert.Contains("Willkommen zurück · Plagevo", html);
        Assert.Contains("plagevo-wortmarke-hell.svg", html);
        Assert.Contains("plagevo-wortmarke-dunkel.svg", html);
        Assert.Contains("_content/Duofy.Shared/brand/plagevo-favicon-hell.svg", html);
        Assert.Contains("_content/Duofy.Shared/brand/plagevo-favicon-dunkel.svg", html);
        Assert.Contains("prefers-color-scheme: dark", html);
        Assert.DoesNotContain("_content/Duofy.Shared/favicon.png", html);
    }

    [Fact]
    public async Task An_explicit_name_wins_over_the_hosted_flag()
    {
        var html = await SignInPageAsync(("Duofy:Hosted", "true"), ("Duofy:AppName", "Duofy"));

        Assert.Contains("Willkommen zurück · Duofy", html);
        Assert.DoesNotContain("plagevo-wortmarke", html);
    }

    // Die Marke kommt aus der Umgebung des Servers: Die Compose-Datei reicht beide Werte an den Web-Dienst weiter und
    // beide Beispieldateien nennen den Namen.
    [Fact]
    public void The_web_service_gets_the_hosted_flag_and_the_name_and_the_examples_name_the_variable()
    {
        var compose = File.ReadAllText(Path("deploy", "docker-compose.yml")).ReplaceLineEndings("\n");
        var start = compose.IndexOf("\n  web:", StringComparison.Ordinal);
        var web = compose[start..compose.IndexOf("\n  tunnel:", start, StringComparison.Ordinal)];

        Assert.Contains("Duofy__Hosted:", web);
        Assert.Contains("Duofy__AppName:", web);
        Assert.Contains("Duofy__AppName=", File.ReadAllText(Path(".env.example")));
        Assert.Contains("DUOFY_APP_NAME=", File.ReadAllText(Path("deploy", ".env.example")));
    }

    private static string Path(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "Duofy.slnx")))
        {
            directory = directory.Parent;
        }
        return System.IO.Path.Combine([directory!.FullName, .. parts]);
    }
}
