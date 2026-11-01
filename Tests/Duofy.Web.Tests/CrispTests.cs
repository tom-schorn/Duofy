using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Duofy.Web.Tests;

/// <summary>
/// Crisp-Chat (Issue #36): Das Skript von Crisp steht nie in der ausgelieferten Seite, auch nicht mit Website-ID. Es wird
/// erst im Browser nachgeladen, wenn die Person dem Chat zugestimmt hat (ConsentBanner). Den Baustein selbst prüfen die
/// Tests in Duofy.Shared.Tests; die Einstellungen stehen in .env.example und deploy.
/// </summary>
public class CrispTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string WebsiteId = "01234567-89ab-cdef-0123-456789abcdef";
    private readonly WebApplicationFactory<Program> factory;

    public CrispTests(WebApplicationFactory<Program> factory) => this.factory = factory;

    [Fact]
    public async Task Even_with_a_website_id_the_served_page_carries_no_crisp_script_and_not_the_id()
    {
        var page = await PageAsync(WebsiteId);

        Assert.DoesNotContain("crisp", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(WebsiteId, page);
    }

    [Fact]
    public async Task Without_a_website_id_there_is_no_chat()
    {
        var page = await PageAsync("");

        Assert.DoesNotContain("crisp", page, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_setting_is_named_in_both_examples_and_passed_on_by_compose()
    {
        var root = File.ReadAllText(Path(".env.example"));
        var deploy = File.ReadAllText(Path("deploy", ".env.example"));
        var compose = File.ReadAllText(Path("deploy", "docker-compose.yml"));

        Assert.Matches(@"(?m)^Crisp__WebsiteId=\r?$", root);
        Assert.Matches(@"(?m)^CRISP_WEBSITE_ID=\r?$", deploy);
        Assert.Contains("Crisp__WebsiteId: ${Crisp__WebsiteId:-${CRISP_WEBSITE_ID:-}}", compose);
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

    private async Task<string> PageAsync(string id)
    {
        using var web = factory.WithWebHostBuilder(builder => builder.UseSetting("Crisp:WebsiteId", id));
        var client = web.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        return await client.GetStringAsync("/anmelden");
    }
}
