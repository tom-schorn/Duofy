using Bunit;
using Duofy.Shared.Layout;
using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Issue #35 (Einwilligungs-Baustein): Standard ist "nicht einverstanden", Ablehnen ist so leicht wie Zustimmen,
/// die Wahl wird gespeichert und ist widerrufbar, ohne Einwilligung wird kein Fremdskript geladen.
/// Das Laden des Skripts selbst (consent.js) läuft im Browser; der Test sieht nur den Aufruf dorthin.
/// Der echte Browser ist damit nicht gesehen.
/// </summary>
public class ConsentBannerTests : UiTestContext
{
    private const string Token = "0123456789abcdef0123456789abcdef";
    private const string WebsiteId = "01234567-89ab-cdef-0123-456789abcdef";
    private const string Module = "./_content/Duofy.Shared/consent.js";

    private readonly MemoryConsentStore store = new();
    private readonly BunitJSModuleInterop module;

    public ConsentBannerTests()
    {
        Services.AddSingleton<IConsentStore>(store);
        module = JSInterop.SetupModule(Module);
        module.SetupVoid("loadBeacon", _ => true).SetVoidResult();
        module.SetupVoid("loadChat", _ => true).SetVoidResult();
        module.SetupVoid("resetChat", _ => true).SetVoidResult();
    }

    private IRenderedComponent<ConsentBanner> Open(string? token = Token) =>
        Render<ConsentBanner>(p => p.Add(x => x.StatisticsToken, token));

    private IRenderedComponent<ConsentBanner> OpenWith(string? token, string? chatId) =>
        Render<ConsentBanner>(p => p.Add(x => x.StatisticsToken, token).Add(x => x.ChatWebsiteId, chatId));

    private int Loaded() => module.Invocations["loadBeacon"].Count;

    private int ChatLoaded() => module.Invocations["loadChat"].Count;

    // A1: Ohne gespeicherte Wahl gilt "nicht einverstanden": der Hinweis steht da, nichts wird geladen.
    [Fact]
    public void Without_a_saved_choice_the_notice_is_shown_and_nothing_is_loaded()
    {
        var cut = Open();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='consent-banner']")));
        Assert.Equal(0, Loaded());
        Assert.Null(store.Saved);
    }

    // A2: Ablehnen ist so leicht wie Zustimmen: beide Knöpfe gleich gestaltet, gleich groß, gleiche Stufe.
    [Fact]
    public void Declining_is_as_easy_as_agreeing()
    {
        var cut = Open();
        cut.WaitForAssertion(() => cut.Find("[data-testid='consent-banner']"));

        var accept = cut.Find("[data-testid='consent-accept']");
        var decline = cut.Find("[data-testid='consent-decline']");

        Assert.Equal(accept.ClassList.Where(c => c.StartsWith("rz-")).Order(),
                     decline.ClassList.Where(c => c.StartsWith("rz-")).Order());
        Assert.Contains("rz-button", accept.ClassList);
        Assert.Equal("Ablehnen", decline.TextContent.Trim());
        Assert.Equal("Zustimmen", accept.TextContent.Trim());
        Assert.Equal(decline.OuterHtml, accept.NextElementSibling!.OuterHtml);
    }

    // A3: Zustimmen speichert die Wahl, lädt das Skript mit dem Token und schließt den Hinweis.
    [Fact]
    public void Agreeing_saves_the_choice_loads_the_script_and_closes_the_notice()
    {
        var cut = Open();
        cut.WaitForAssertion(() => cut.Find("[data-testid='consent-banner']"));

        cut.Find("[data-testid='consent-accept']").Click();

        Assert.True(store.Saved);
        var call = Assert.Single(module.Invocations["loadBeacon"]);
        Assert.Equal(Token, call.Arguments[0]);
        Assert.Empty(cut.FindAll("[data-testid='consent-banner']"));
    }

    // A3: Ablehnen speichert die Wahl, lädt nichts und schließt den Hinweis.
    [Fact]
    public void Declining_saves_the_choice_loads_nothing_and_closes_the_notice()
    {
        var cut = Open();
        cut.WaitForAssertion(() => cut.Find("[data-testid='consent-banner']"));

        cut.Find("[data-testid='consent-decline']").Click();

        Assert.False(store.Saved);
        Assert.Equal(0, Loaded());
        Assert.Empty(cut.FindAll("[data-testid='consent-banner']"));
    }

    // A4: Die gespeicherte Wahl gilt beim nächsten Aufruf: Zustimmung lädt das Skript ohne Frage ...
    [Fact]
    public void A_saved_agreement_loads_the_script_without_asking_again()
    {
        store.Stored = true;

        var cut = Open();

        cut.WaitForAssertion(() => Assert.Equal(1, Loaded()));
        Assert.Empty(cut.FindAll("[data-testid='consent-banner']"));
    }

    // ... und eine gespeicherte Ablehnung lädt nichts und fragt nicht noch einmal.
    [Fact]
    public void A_saved_refusal_loads_nothing_and_does_not_ask_again()
    {
        store.Stored = false;

        var cut = Open();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='consent-change']")));
        Assert.Equal(0, Loaded());
        Assert.Empty(cut.FindAll("[data-testid='consent-banner']"));
    }

    // A5: Widerrufbar: Nach der Wahl bleibt ein Knopf zum Ändern; Zustimmen nach Ablehnen lädt das Skript.
    [Fact]
    public void The_choice_can_be_changed_from_refusal_to_agreement()
    {
        store.Stored = false;
        var cut = Open();
        cut.WaitForElement("[data-testid='consent-change']").Click();

        cut.Find("[data-testid='consent-accept']").Click();

        Assert.True(store.Saved);
        Assert.Single(module.Invocations["loadBeacon"]);
    }

    // A5: Widerruf einer Zustimmung speichert "nicht einverstanden" und lädt die Seite neu,
    // denn ein schon geladenes Fremdskript lässt sich nicht aus der Seite nehmen.
    [Fact]
    public void Withdrawing_an_agreement_saves_the_refusal_and_reloads_the_page()
    {
        store.Stored = true;
        var cut = Open();
        cut.WaitForElement("[data-testid='consent-change']").Click();

        cut.Find("[data-testid='consent-decline']").Click();

        Assert.False(store.Saved);
        var navigation = Assert.IsType<Bunit.TestDoubles.BunitNavigationManager>(Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>());
        Assert.Single(module.Invocations["loadBeacon"]);
        Assert.Contains(navigation.History, h => h.Options.ForceLoad);
    }

    // A6: Ohne Token gibt es keinen Dienst, also keine Frage, keinen Knopf, keinen Zugriff auf Speicher und Skript.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Without_a_token_nothing_is_shown_read_or_loaded(string? token)
    {
        var cut = Open(token);

        Assert.Empty(cut.Markup.Trim());
        Assert.Equal(0, store.Reads);
        Assert.Empty(JSInterop.Invocations);
    }

    // A7: Der Hinweis ist für Screenreader ein Bereich mit Namen, und der Text sagt, was geladen wird.
    [Fact]
    public void The_notice_is_a_labelled_region_that_says_what_it_is_about()
    {
        var cut = Open();
        var banner = cut.WaitForElement("[data-testid='consent-banner']");

        Assert.Equal("region", banner.GetAttribute("role"));
        Assert.Equal("Einwilligung", banner.GetAttribute("aria-label"));
        Assert.Contains("Cloudflare Web Analytics", banner.TextContent);
        Assert.Contains("Statistik", banner.TextContent);
    }

    // Die Ablage im Browser: kein Eintrag heißt "noch nicht gewählt", nicht "einverstanden".
    [Fact]
    public async Task Local_storage_keeps_the_choice_and_no_entry_means_undecided()
    {
        JSInterop.SetupVoid("localStorage.setItem", _ => true).SetVoidResult();
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult(null);
        var local = new LocalStorageConsentStore(JSInterop.JSRuntime);

        Assert.Null(await local.LoadAsync(ConsentCategory.Statistics));

        await local.SaveAsync(ConsentCategory.Statistics, true);
        await local.SaveAsync(ConsentCategory.Statistics, false);

        var writes = JSInterop.Invocations["localStorage.setItem"];
        Assert.Equal(["duofy-consent-statistics", "granted"], [writes[0].Arguments[0]!, writes[0].Arguments[1]!]);
        Assert.Equal("denied", writes[1].Arguments[1]);
    }

    [Theory]
    [InlineData("granted", true)]
    [InlineData("denied", false)]
    [InlineData("irgendwas", null)]
    public async Task Local_storage_reads_only_the_two_known_values(string stored, bool? expected)
    {
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult(stored);
        var local = new LocalStorageConsentStore(JSInterop.JSRuntime);

        Assert.Equal(expected, await local.LoadAsync(ConsentCategory.Statistics));
    }

    // --- Chat (Issue #36): eine eigene Kategorie neben der Statistik, Wahl je Kategorie, Standard nein ------------

    // Ohne Website-ID gibt es keinen Chat: keine Frage, kein Speicherzugriff, kein Skript (Standard bei Self-Host).
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Without_a_website_id_there_is_no_chat_and_no_question(string? id)
    {
        var cut = OpenWith(null, id);

        Assert.Empty(cut.Markup.Trim());
        Assert.Equal(0, store.Reads);
        Assert.Empty(JSInterop.Invocations);
    }

    [Fact]
    public void With_only_a_website_id_the_notice_asks_about_the_chat_and_loads_nothing()
    {
        var cut = OpenWith(null, WebsiteId);

        var banner = cut.WaitForElement("[data-testid='consent-banner']");
        Assert.Contains("Chat", banner.TextContent);
        Assert.Contains("Crisp", banner.TextContent);
        Assert.Empty(cut.FindAll("[data-testid='consent-accept']"));
        Assert.Equal(0, ChatLoaded());
        Assert.Equal(0, Loaded());
        Assert.Null(store.SavedFor(ConsentCategory.Chat));
    }

    [Fact]
    public void Agreeing_to_the_chat_saves_the_choice_for_the_chat_only_and_loads_the_chat_with_the_website_id_and_nothing_else()
    {
        var cut = OpenWith(null, WebsiteId);
        cut.WaitForElement("[data-testid='consent-chat-accept']").Click();

        Assert.True(store.SavedFor(ConsentCategory.Chat));
        Assert.Null(store.SavedFor(ConsentCategory.Statistics));
        var call = Assert.Single(module.Invocations["loadChat"]);
        Assert.Equal([WebsiteId], call.Arguments);
        Assert.Empty(cut.FindAll("[data-testid='consent-banner']"));
    }

    [Fact]
    public void Declining_the_chat_loads_nothing_and_is_as_easy_as_agreeing()
    {
        var cut = OpenWith(null, WebsiteId);
        var accept = cut.WaitForElement("[data-testid='consent-chat-accept']");
        var decline = cut.Find("[data-testid='consent-chat-decline']");

        Assert.Equal(accept.ClassList.Where(c => c.StartsWith("rz-")).Order(),
                     decline.ClassList.Where(c => c.StartsWith("rz-")).Order());
        Assert.Contains("rz-button", accept.ClassList);
        Assert.Equal(decline.OuterHtml, accept.NextElementSibling!.OuterHtml);

        decline.Click();

        Assert.False(store.SavedFor(ConsentCategory.Chat));
        Assert.Equal(0, ChatLoaded());
    }

    // Wahl je Kategorie: Statistik zustimmen und Chat ablehnen hängen nicht zusammen.
    [Fact]
    public void Both_services_are_asked_separately_and_each_choice_stands_for_itself()
    {
        var cut = OpenWith(Token, WebsiteId);
        cut.WaitForElement("[data-testid='consent-banner']");

        cut.Find("[data-testid='consent-accept']").Click();

        Assert.True(store.SavedFor(ConsentCategory.Statistics));
        Assert.Null(store.SavedFor(ConsentCategory.Chat));
        Assert.Equal(1, Loaded());
        Assert.Equal(0, ChatLoaded());
        Assert.NotEmpty(cut.FindAll("[data-testid='consent-chat-accept']"));

        cut.Find("[data-testid='consent-chat-decline']").Click();

        Assert.False(store.SavedFor(ConsentCategory.Chat));
        Assert.Equal(0, ChatLoaded());
        Assert.Empty(cut.FindAll("[data-testid='consent-banner']"));
    }

    [Fact]
    public void Nothing_is_pre_chosen_when_both_services_are_asked()
    {
        var cut = OpenWith(Token, WebsiteId);
        cut.WaitForElement("[data-testid='consent-banner']");

        Assert.Equal(0, Loaded());
        Assert.Equal(0, ChatLoaded());
        Assert.Empty(store.AllSaved);
    }

    [Fact]
    public void A_saved_agreement_for_the_chat_loads_it_without_asking()
    {
        store.StoredFor[ConsentCategory.Chat] = true;
        var cut = OpenWith(null, WebsiteId);

        cut.WaitForAssertion(() => Assert.Equal(1, ChatLoaded()));
        Assert.Empty(cut.FindAll("[data-testid='consent-banner']"));
    }

    [Fact]
    public void A_saved_refusal_for_the_chat_loads_nothing_and_does_not_ask_again()
    {
        store.StoredFor[ConsentCategory.Chat] = false;
        var cut = OpenWith(null, WebsiteId);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='consent-change']")));
        Assert.Equal(0, ChatLoaded());
        Assert.Empty(cut.FindAll("[data-testid='consent-banner']"));
    }

    // Ist die Statistik entschieden und der Chat neu, fragt der Hinweis nur nach dem Chat.
    [Fact]
    public void Only_the_undecided_service_is_asked()
    {
        store.StoredFor[ConsentCategory.Statistics] = false;
        var cut = OpenWith(Token, WebsiteId);

        cut.WaitForElement("[data-testid='consent-chat-accept']");
        Assert.Empty(cut.FindAll("[data-testid='consent-accept']"));
    }

    [Fact]
    public void Withdrawing_the_chat_resets_the_chat_session_and_reloads_the_page()
    {
        store.StoredFor[ConsentCategory.Chat] = true;
        var cut = OpenWith(null, WebsiteId);
        cut.WaitForElement("[data-testid='consent-change']").Click();

        cut.Find("[data-testid='consent-chat-decline']").Click();

        Assert.False(store.SavedFor(ConsentCategory.Chat));
        Assert.Single(module.Invocations["resetChat"]);
        var navigation = Assert.IsType<Bunit.TestDoubles.BunitNavigationManager>(Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>());
        Assert.Contains(navigation.History, h => h.Options.ForceLoad);
    }

    // Keine Nutzerdaten an Crisp: Das Skript setzt keine Daten (keine E-Mail, kein Name, keine Kennung); in den Chat
    // geht nur die Website-ID. Der Test prüft den Quelltext von consent.js.
    [Fact]
    public void The_chat_script_passes_no_user_data_to_crisp()
    {
        var js = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "consent.js"));

        Assert.Contains("https://client.crisp.chat/l.js", js);
        Assert.DoesNotContain("user:", js);
        Assert.DoesNotContain("\"set\"", js);
        Assert.DoesNotContain("session:merge", js);
        Assert.DoesNotContain("CRISP_TOKEN_ID", js);
    }

    // Crisp sieht die Adresse der Seite. Auf Seiten mit Token in der Adresse (Links aus Mails) lädt der Chat nicht.
    [Fact]
    public void The_chat_does_not_load_on_pages_whose_address_carries_a_secret()
    {
        var js = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "consent.js"));
        var routes = new[] { "/bestaetigen", "/email-bestaetigen", "/passwort-neu", "/einladung/" };

        foreach (var route in routes)
        {
            Assert.Contains($"\"{route}\"", js);
        }
        // Die Liste deckt jede Seite mit Token in der Adresse ab; kommt eine neue hinzu, muss sie hier stehen.
        var pages = Directory.EnumerateFiles(RepositoryFiles.Path("Shared", "Duofy.Shared", "Pages"), "*.razor")
            .Select(f => (File: f, Text: File.ReadAllText(f)))
            .Where(p => p.Text.Contains("token=", StringComparison.OrdinalIgnoreCase) || p.Text.Contains("{Token}", StringComparison.Ordinal))
            .Select(p => System.Text.RegularExpressions.Regex.Match(p.Text, "@page \"([^\"{]+)").Groups[1].Value)
            .ToList();
        Assert.NotEmpty(pages);
        Assert.All(pages, page => Assert.Contains(routes, r => r.TrimEnd('/') == page.TrimEnd('/')));
    }

    // Beim Widerruf löscht das Skript die Cookies des Chats selbst, statt sich auf Crisp zu verlassen.
    [Fact]
    public void Withdrawing_deletes_the_cookies_of_the_chat_itself()
    {
        var js = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "consent.js"));
        var reset = js[js.IndexOf("export function resetChat", StringComparison.Ordinal)..];

        Assert.Contains("crisp-client", reset);
        Assert.Contains("expires=Thu, 01 Jan 1970", reset);
        Assert.Contains("session:reset", reset);
    }

    [Fact]
    public void The_chat_script_loads_only_for_a_website_id_in_the_form_crisp_uses()
    {
        var js = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "consent.js"));

        Assert.Contains("[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", js);
    }

    [Fact]
    public void The_web_start_hands_the_website_id_from_the_setting_to_the_notice()
    {
        var routes = File.ReadAllText(RepositoryFiles.Path("Web", "Duofy.Web", "Components", "WebRoutes.razor"));

        Assert.Contains("ChatWebsiteId=\"@Configuration[\"Crisp:WebsiteId\"]\"", routes);
    }

    [Fact]
    public void The_app_does_not_use_the_notice_or_the_chat()
    {
        var sep = Path.DirectorySeparatorChar;
        foreach (var file in Directory.EnumerateFiles(RepositoryFiles.Path("Mobile", "Duofy"), "*.*", SearchOption.AllDirectories)
                     .Where(f => (f.EndsWith(".razor") || f.EndsWith(".cs") || f.EndsWith(".html"))
                                 && !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}")))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("crisp", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ConsentBanner", text);
        }
    }

    [Fact]
    public async Task Local_storage_keeps_the_chat_choice_under_its_own_name()
    {
        JSInterop.SetupVoid("localStorage.setItem", _ => true).SetVoidResult();
        var local = new LocalStorageConsentStore(JSInterop.JSRuntime);

        await local.SaveAsync(ConsentCategory.Chat, true);

        var write = Assert.Single(JSInterop.Invocations["localStorage.setItem"]);
        Assert.Equal("duofy-consent-chat", write.Arguments[0]);
    }

    private sealed class MemoryConsentStore : IConsentStore
    {
        public Dictionary<ConsentCategory, bool> StoredFor { get; } = [];
        public Dictionary<ConsentCategory, bool> AllSaved { get; } = [];
        public int Reads { get; private set; }

        // Für die Statistik wie bisher, damit die älteren Tests unverändert bleiben.
        public bool? Stored
        {
            set { if (value is { } v) StoredFor[ConsentCategory.Statistics] = v; else StoredFor.Remove(ConsentCategory.Statistics); }
        }

        public bool? Saved => SavedFor(ConsentCategory.Statistics);

        public bool? SavedFor(ConsentCategory category) => AllSaved.TryGetValue(category, out var v) ? v : null;

        public Task<bool?> LoadAsync(ConsentCategory category)
        {
            Reads++;
            return Task.FromResult<bool?>(StoredFor.TryGetValue(category, out var v) ? v : null);
        }

        public Task SaveAsync(ConsentCategory category, bool granted)
        {
            AllSaved[category] = granted;
            StoredFor[category] = granted;
            return Task.CompletedTask;
        }
    }
}
