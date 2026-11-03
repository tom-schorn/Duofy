using Bunit;
using Duofy.ApiClient;
using Duofy.Shared.Layout;
using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Die Serverwahl am Fuß von Anmelden und Registrieren: die Zeile, der Dialog und seine
/// Antworten (R1 bis R4). Prüfung und Speicherung des Servers testet Duofy.ApiClient.Tests.
/// </summary>
public class ServerChoiceTests : PageTestContext
{
    private static readonly Uri Hosted = new("https://api.plagevo.de/");

    private readonly ServerStore store = new(Hosted, new InMemoryServerPreference());
    private readonly FakeServerSelector selector;

    public ServerChoiceTests()
    {
        selector = new FakeServerSelector(store);
        Services.AddSingleton<IThemePreferenceStore>(new NoThemeStore());
    }

    // Die App meldet sich an; ohne diesen Aufruf läuft der Test wie im Browser.
    private void RunAsApp()
    {
        Services.AddSingleton<IAppHost, AppHost>();
        Services.AddSingleton<IServerStore>(store);
        Services.AddSingleton<IServerSelector>(selector);
    }

    private IRenderedComponent<Routes> Open(string path)
    {
        Navigation.NavigateTo(path);
        var cut = Render<Routes>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".auth-layout")));
        return cut;
    }

    private static string LineOf(IRenderedComponent<Routes> cut) => cut.Find("[data-testid='server-line']").TextContent.Trim();

    private static void OpenDialog(IRenderedComponent<Routes> cut)
    {
        cut.Find("[data-testid='server-line']").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='server-choice']")));
    }

    // A1
    [Theory]
    [InlineData("/anmelden")]
    [InlineData("/registrieren")]
    public void A_fresh_app_says_at_the_bottom_that_the_hosted_service_is_used(string path)
    {
        RunAsApp();

        var cut = Open(path);

        Assert.Equal("Server: Plagevo (gehostet)", LineOf(cut));
    }

    // Der vorgelesene Name enthält den sichtbaren Text, damit Sprachsteuerung und Vorlesen denselben Server nennen (WCAG 2.5.3).
    [Fact]
    public void The_accessible_name_of_the_line_contains_its_visible_text()
    {
        RunAsApp();
        store.Set(new Uri("https://mein.server.test/duofy"));

        var cut = Open("/anmelden");

        var line = cut.Find("[data-testid='server-line']");
        Assert.Equal("Server: mein.server.test", LineOf(cut));
        Assert.Equal("Server: mein.server.test, ändern", line.GetAttribute("aria-label"));
    }

    // A8
    [Theory]
    [InlineData("/anmelden")]
    [InlineData("/registrieren")]
    public void The_web_version_has_no_server_line(string path)
    {
        var cut = Open(path);

        Assert.Empty(cut.FindAll("[data-testid='server-line']"));
    }

    // A2
    [Fact]
    public void An_own_address_that_is_taken_shows_its_host_in_the_line()
    {
        RunAsApp();
        selector.Outcome = ServerChoiceOutcome.Taken;
        var cut = Open("/anmelden");
        OpenDialog(cut);

        cut.Find("[data-testid='server-choice']").Change("own");
        cut.WaitForAssertion(() => cut.Find("[data-testid='server-address']"));
        cut.Find("[data-testid='server-address']").Change("https://mein.server.test/duofy");
        cut.Find("[data-testid='server-save']").Click();

        cut.WaitForAssertion(() => Assert.Equal("Server: mein.server.test", LineOf(cut)));
        Assert.Equal("https://mein.server.test/duofy", selector.Tried.Single());
        Assert.Empty(cut.FindAll("[data-testid='server-choice']"));
    }

    // A3
    [Fact]
    public void An_address_without_a_server_shows_the_text_and_the_old_server_stays()
    {
        RunAsApp();
        selector.Outcome = ServerChoiceOutcome.NoServer;
        var cut = Open("/anmelden");
        OpenDialog(cut);

        cut.Find("[data-testid='server-choice']").Change("own");
        cut.WaitForAssertion(() => cut.Find("[data-testid='server-address']"));
        cut.Find("[data-testid='server-address']").Change("https://tippfehler.test");
        cut.Find("[data-testid='server-save']").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("Unter dieser Adresse antwortet kein Duofy-Server.", cut.Find("[data-testid=\'server-error\']").TextContent));
        Assert.Equal("Server: Plagevo (gehostet)", LineOf(cut));
        Assert.NotEmpty(cut.FindAll("[data-testid='server-choice']"));
    }

    // A4 (Text)
    [Fact]
    public void An_address_without_https_shows_the_https_text()
    {
        RunAsApp();
        selector.Outcome = ServerChoiceOutcome.NeedsHttps;
        var cut = Open("/anmelden");
        OpenDialog(cut);

        cut.Find("[data-testid='server-choice']").Change("own");
        cut.WaitForAssertion(() => cut.Find("[data-testid='server-address']"));
        cut.Find("[data-testid='server-address']").Change("http://192.168.2.67:5080");
        cut.Find("[data-testid='server-save']").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("Bitte eine https-Adresse eingeben.", cut.Find("[data-testid=\'server-error\']").TextContent));
    }

    // R2
    [Fact]
    public void The_dialog_preselects_the_hosted_service_and_needs_no_address_for_it()
    {
        RunAsApp();
        var cut = Open("/anmelden");
        OpenDialog(cut);

        Assert.Equal("hosted", cut.Find("[data-testid='server-choice']").GetAttribute("value"));
        // Nachtrag 2026-10-27: Der gehostete Dienst heißt Plagevo; der eigene Server bleibt mit eigener Adresse.
        Assert.Equal(["Plagevo", "Eigener Server"],
            cut.FindAll("[data-testid='server-choice'] option").Select(o => o.TextContent.Trim()));
        Assert.Empty(cut.FindAll("[data-testid='server-address']"));

        cut.Find("[data-testid='server-save']").Click();

        cut.WaitForAssertion(() => Assert.Equal(1, selector.HostedCalls));
        Assert.Empty(selector.Tried);
    }

    // R2
    [Fact]
    public void The_dialog_preselects_an_own_server_with_its_address()
    {
        RunAsApp();
        store.Set(new Uri("https://mein.server.test/duofy"));
        var cut = Open("/anmelden");
        OpenDialog(cut);

        Assert.Equal("own", cut.Find("[data-testid='server-choice']").GetAttribute("value"));
        Assert.Equal("https://mein.server.test/duofy/", Field(cut, "server-address").GetAttribute("value"));
        Assert.Equal("Server: mein.server.test", LineOf(cut));
    }

    [Fact]
    public void A_server_with_a_port_shows_the_port_too()
    {
        RunAsApp();
        store.Set(new Uri("http://192.168.2.67:5080"));

        var cut = Open("/registrieren");

        Assert.Equal("Server: 192.168.2.67:5080", LineOf(cut));
    }

    private sealed class FakeServerSelector(ServerStore store) : IServerSelector
    {
        public ServerChoiceOutcome Outcome { get; set; } = ServerChoiceOutcome.Taken;
        public List<string?> Tried { get; } = [];
        public int HostedCalls { get; private set; }

        public Task<ServerChoiceOutcome> TryUseAsync(string? input, CancellationToken cancellationToken = default)
        {
            Tried.Add(input);
            if (Outcome == ServerChoiceOutcome.Taken)
            {
                store.Set(new Uri(input!));
            }
            return Task.FromResult(Outcome);
        }

        public Task UseHostedAsync(CancellationToken cancellationToken = default)
        {
            HostedCalls++;
            store.Reset();
            return Task.CompletedTask;
        }
    }

    private sealed class NoThemeStore : IThemePreferenceStore
    {
        public Task<ThemeChoice> LoadAsync() => Task.FromResult(ThemeChoice.Device);
        public Task SaveAsync(ThemeChoice choice) => Task.CompletedTask;
    }

    // Spec 82: In der App ist der Dialog dasselbe Fenster wie im Browser; ohne Übernehmen ändert sich nichts.
    [Fact]
    public void In_the_app_the_dialog_is_the_same_window_and_closing_changes_nothing()
    {
        RunAsApp();
        var cut = Open("/anmelden");

        OpenDialog(cut);

        var dialog = cut.Find(DialogSelector);
        Assert.Equal("Server", dialog.QuerySelector("h2")!.TextContent.Trim());
        Assert.Equal("Übernehmen", dialog.QuerySelector("[data-testid='server-save']")!.TextContent.Trim());
        Assert.Empty(dialog.QuerySelectorAll("[data-testid='form-header']"));

        Assert.Equal("Server: Plagevo (gehostet)", LineOf(cut));
        Assert.Empty(selector.Tried);
        Assert.Equal(0, selector.HostedCalls);
    }

    [Fact]
    public async Task In_the_browser_the_dialog_is_a_window_with_a_title_and_a_save_button_and_returns_the_choice()
    {
        Services.AddSingleton<IServerStore>(store);
        Services.AddSingleton<IServerSelector>(selector);
        var host = RenderDialogHost();

        var opened = Services.GetRequiredService<FormDialogs>().OpenAsync<Duofy.Shared.Pages.ServerDialog, bool>("Server",
            new FormParameters<Duofy.Shared.Pages.ServerDialog>().Add(d => d.Title, "Server"));
        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll("[data-testid='server-choice']")));

        Assert.Equal("Server", host.Find(DialogSelector + " h2").TextContent.Trim());
        Assert.Empty(host.FindAll("[data-testid='form-header']"));

        host.Find("[data-testid='server-save']").Click();
        var outcome = await opened;

        Assert.True(outcome.Saved);
        Assert.True(outcome.Value);
        Assert.Equal(1, selector.HostedCalls);
    }
}
