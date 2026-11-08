using Bunit;
using Duofy.Shared.Layout;
using Duofy.Shared.Services;
using Duofy.Shared.Theme;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 109, R5 (geändert 07.11.2026): Im Browser öffnet ein Rechtslink einen neuen Tab, in der App öffnet eine äußere
/// Adresse den Browser des Telefons. Und Entscheidung 3: In der App verlässt ein Link die Registrierung nie; ein eigener
/// Text öffnet sich als Fenster über dem Formular.
/// </summary>
public abstract class LegalPlacesBase : PageTestContext
{
    protected LegalPlacesBase(bool app)
    {
        if (app) Services.AddSingleton<IAppHost, AppHost>();
        Services.AddSingleton<IThemePreferenceStore>(new NoThemeStore());
    }

    protected IRenderedComponent<Routes> Open(string path)
    {
        Navigation.NavigateTo(path);
        return Render<Routes>();
    }

    protected void AllThree()
    {
        LegalApiFake.Documents["imprint"] = "Impressumstext";
        LegalApiFake.Documents["privacy"] = "Datenschutztext";
        LegalApiFake.Documents["terms"] = "Bedingungstext";
    }

    private sealed class NoThemeStore : IThemePreferenceStore
    {
        public Task<ThemeChoice> LoadAsync() => Task.FromResult(ThemeChoice.Device);
        public Task SaveAsync(ThemeChoice choice) => Task.CompletedTask;
    }
}

public class LegalLinksOnTheWebTests : LegalPlacesBase
{
    public LegalLinksOnTheWebTests() : base(app: false) { }

    [Fact]
    public void Every_legal_link_of_the_line_opens_a_new_tab()
    {
        AllThree();
        LegalApiFake.Urls["imprint"] = "https://example.org/impressum";

        var cut = Open("/anmelden");

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("[data-testid='legal-links'] a").Count));
        Assert.All(cut.FindAll("[data-testid='legal-links'] a"), a =>
        {
            Assert.Equal("_blank", a.GetAttribute("target"));
            Assert.Contains("noopener", a.GetAttribute("rel"));
        });
    }
}

public class LegalLinksInTheAppTests : LegalPlacesBase
{
    public LegalLinksInTheAppTests() : base(app: true) { }

    // Ein Link mit target="_blank" erreicht UrlLoading der Ansicht nicht; ohne target lädt die Ansicht die Adresse, und
    // die App schickt sie an den Browser des Telefons.
    [Fact]
    public void An_external_link_has_no_target_so_the_app_can_hand_it_to_the_system_browser()
    {
        AllThree();
        LegalApiFake.Urls["imprint"] = "https://example.org/impressum";

        var cut = Open("/anmelden");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='legal-link-imprint']")));
        var imprint = cut.Find("[data-testid='legal-link-imprint']");
        Assert.Equal("https://example.org/impressum", imprint.GetAttribute("href"));
        Assert.Null(imprint.GetAttribute("target"));
    }

    // Entscheidung 3: Die Zeile unter dem Registrieren-Formular öffnet in der App den eigenen Text als Fenster.
    [Fact]
    public void On_the_registration_an_own_text_opens_above_the_form_and_the_form_stays()
    {
        AllThree();
        var cut = Open("/registrieren");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='legal-link-imprint']")));
        Field(cut, "register-first-name").Change("Anna");

        cut.Find("[data-testid='legal-link-imprint']").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='legal-overlay']")));
        Assert.Equal("/registrieren", CurrentPath);
        Assert.Equal("Impressumstext", cut.Find("[data-testid='legal-overlay-text']").TextContent.Trim());
        Assert.Equal("Anna", Field(cut, "register-first-name").GetAttribute("value"));
    }

    // Auf den anderen Seiten bleibt es ein einfacher Link auf die Seite des Servers.
    [Fact]
    public void Outside_the_registration_an_own_text_is_a_plain_link()
    {
        AllThree();
        var cut = Open("/anmelden");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='legal-link-imprint']")));

        Assert.Equal("/rechtliches/impressum", cut.Find("[data-testid='legal-link-imprint']").GetAttribute("href"));
        Assert.Empty(cut.FindAll("[data-testid='legal-overlay']"));
    }
}

public class RegistrationConsentOnTheWebTests : LegalPlacesBase
{
    public RegistrationConsentOnTheWebTests() : base(app: false) { }

    [Fact]
    public void A_consent_link_opens_a_new_tab_and_never_the_overlay()
    {
        AllThree();
        var cut = Open("/registrieren");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='register-link-privacy']")));

        var link = cut.Find("[data-testid='register-link-privacy']");

        Assert.Equal("_blank", link.GetAttribute("target"));
        Assert.Equal("/rechtliches/datenschutz", link.GetAttribute("href"));
        Assert.Empty(cut.FindAll("[data-testid='legal-overlay']"));
    }
}

public class RegistrationConsentInTheAppTests : LegalPlacesBase
{
    public RegistrationConsentInTheAppTests() : base(app: true) { }

    // Mit einer äußeren Adresse öffnet der Browser des Telefons; das Formular bleibt, kein Fenster, kein target.
    [Fact]
    public void With_an_external_address_the_link_is_left_to_the_system_browser()
    {
        AllThree();
        LegalApiFake.Urls["privacy"] = "https://example.org/datenschutz";
        var cut = Open("/registrieren");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='register-link-privacy']")));

        var link = cut.Find("[data-testid='register-link-privacy']");

        Assert.Equal("https://example.org/datenschutz", link.GetAttribute("href"));
        Assert.Null(link.GetAttribute("target"));
    }

    // Ohne äußere Adresse (Selbst-Hosting) öffnet der eigene Text ein Fenster über dem Formular.
    [Fact]
    public void Without_an_external_address_the_own_text_opens_above_the_form_and_the_entries_stay()
    {
        AllThree();
        var cut = Open("/registrieren");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='register-link-terms']")));
        Field(cut, "register-first-name").Change("Anna");
        Field(cut, "register-email").Change("anna@example.org");

        cut.Find("[data-testid='register-link-terms']").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='legal-overlay']")));
        Assert.Equal("/registrieren", CurrentPath);
        var overlay = cut.Find("[data-testid='legal-overlay']");
        Assert.Equal("dialog", overlay.GetAttribute("role"));
        Assert.Equal("true", overlay.GetAttribute("aria-modal"));
        Assert.Equal("Bedingungstext", cut.Find("[data-testid='legal-overlay-text']").TextContent.Trim());
        Assert.Contains("Nutzungsbedingungen", overlay.TextContent);
        Assert.Equal("Anna", Field(cut, "register-first-name").GetAttribute("value"));
        Assert.Equal("anna@example.org", Field(cut, "register-email").GetAttribute("value"));
    }

    [Fact]
    public void The_overlay_closes_with_its_button_and_with_escape_and_the_form_is_still_filled()
    {
        AllThree();
        var cut = Open("/registrieren");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='register-link-privacy']")));
        Field(cut, "register-first-name").Change("Anna");

        cut.Find("[data-testid='register-link-privacy']").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='legal-overlay']")));
        cut.Find("[data-testid='legal-overlay-close']").Click();
        Assert.Empty(cut.FindAll("[data-testid='legal-overlay']"));

        cut.Find("[data-testid='register-link-privacy']").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='legal-overlay']")));
        cut.Find("[data-testid='legal-overlay']").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.Empty(cut.FindAll("[data-testid='legal-overlay']"));

        Assert.Equal("Anna", Field(cut, "register-first-name").GetAttribute("value"));
    }

    [Fact]
    public void A_text_that_cannot_be_read_says_so_in_the_overlay()
    {
        AllThree();
        var cut = Open("/registrieren");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='register-link-privacy']")));
        LegalApiFake.Documents.Remove("privacy");

        cut.Find("[data-testid='register-link-privacy']").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='legal-overlay']")));
        cut.WaitForAssertion(() => Assert.Contains("gibt es auf diesem Server nicht", cut.Find("[data-testid='legal-overlay']").TextContent));
    }
}

/// <summary>
/// Review 07.11. (Punkt 1): Im Browser öffnen auch die Rechtseinträge im Benutzermenü und auf der Seite Mehr einen neuen Tab;
/// in der App haben sie kein target (die Ansicht reicht eine äußere Adresse an den Browser des Telefons weiter).
/// </summary>
public abstract class LegalEntriesBase : LegalPlacesBase
{
    protected LegalEntriesBase(bool app) : base(app) { }

    protected IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> OpenUserMenu()
    {
        AllThree();
        LegalApiFake.Urls["privacy"] = "https://example.org/datenschutz";
        var host = RenderDialogHost();
        var menu = Render<UserMenu>();
        menu.WaitForAssertion(() => Assert.NotEmpty(menu.FindAll("[data-testid='user-menu-open']")));
        menu.Find("[data-testid='user-menu-open']").Click();
        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll("[data-testid='side-legal-privacy']")));
        return host;
    }

    protected IRenderedComponent<Duofy.Shared.Pages.Mehr> OpenMore()
    {
        AllThree();
        LegalApiFake.Urls["privacy"] = "https://example.org/datenschutz";
        var cut = Render<Duofy.Shared.Pages.Mehr>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='more-legal-privacy']")));
        return cut;
    }
}

public class LegalEntriesOnTheWebTests : LegalEntriesBase
{
    public LegalEntriesOnTheWebTests() : base(app: false) { }

    [Theory]
    [InlineData("side-legal-imprint")]
    [InlineData("side-legal-privacy")]
    [InlineData("side-legal-terms")]
    public void The_user_menu_entries_open_a_new_tab(string testId)
    {
        var host = OpenUserMenu();

        var entry = host.Find($"[data-testid='{testId}']");
        Assert.Equal("_blank", entry.GetAttribute("target"));
        Assert.Contains("noopener", entry.GetAttribute("rel"));
        Assert.Equal("a", entry.TagName.ToLowerInvariant());
    }

    [Fact]
    public void Other_user_menu_entries_stay_in_the_same_tab()
    {
        var host = OpenUserMenu();

        Assert.Null(host.Find("[data-testid='side-licenses']").GetAttribute("target"));
    }

    [Theory]
    [InlineData("more-legal-imprint")]
    [InlineData("more-legal-privacy")]
    [InlineData("more-legal-terms")]
    public void The_more_page_entries_open_a_new_tab(string testId)
    {
        var cut = OpenMore();

        var entry = cut.Find($"[data-testid='{testId}']");
        Assert.Equal("_blank", entry.GetAttribute("target"));
        Assert.Contains("noopener", entry.GetAttribute("rel"));
    }

    [Fact]
    public void Other_more_page_entries_stay_in_the_same_tab()
    {
        var cut = OpenMore();

        Assert.Null(cut.Find("[data-testid='more-licenses']").GetAttribute("target"));
    }
}

public class LegalEntriesInTheAppTests : LegalEntriesBase
{
    public LegalEntriesInTheAppTests() : base(app: true) { }

    [Theory]
    [InlineData("side-legal-imprint")]
    [InlineData("side-legal-privacy")]
    public void The_user_menu_entries_have_no_target(string testId)
    {
        var host = OpenUserMenu();

        Assert.Null(host.Find($"[data-testid='{testId}']").GetAttribute("target"));
    }

    [Theory]
    [InlineData("more-legal-imprint")]
    [InlineData("more-legal-privacy")]
    public void The_more_page_entries_have_no_target(string testId)
    {
        var cut = OpenMore();

        Assert.Null(cut.Find($"[data-testid='{testId}']").GetAttribute("target"));
    }
}

/// <summary>
/// Review 07.11. (Punkt 2): Das Fenster ist modal. Alles dahinter ist unbedienbar (inert), der Fokus bleibt im Fenster,
/// und beim Schließen kehrt er zum Link zurück, der es geöffnet hat. Beides erledigt legal-overlay.js; hier steht, dass
/// das Fenster es beim Öffnen und beim Schließen aufruft.
/// </summary>
public class LegalOverlayFocusTests : LegalPlacesBase
{
    private const string Module = "./_content/Duofy.Shared/legal-overlay.js";

    public LegalOverlayFocusTests() : base(app: true) { }

    [Fact]
    public void Opening_the_overlay_hands_the_panel_to_the_focus_script_and_closing_releases_it()
    {
        var module = JSInterop.SetupModule(Module);
        module.Mode = JSRuntimeMode.Loose;
        AllThree();
        var cut = Open("/registrieren");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='register-link-terms']")));

        cut.Find("[data-testid='register-link-terms']").Click();

        cut.WaitForAssertion(() => Assert.Single(module.Invocations, i => i.Identifier == "open"));
        var panel = (Microsoft.AspNetCore.Components.ElementReference)module.Invocations.Single(i => i.Identifier == "open").Arguments[0]!;
        Assert.Equal(cut.Find("[data-testid='legal-overlay']").GetAttribute("blazor:elementreference"), panel.Id);
        Assert.Empty(module.Invocations.Where(i => i.Identifier == "close"));

        cut.Find("[data-testid='legal-overlay-close']").Click();

        cut.WaitForAssertion(() => Assert.Single(module.Invocations, i => i.Identifier == "close"));
    }

    [Fact]
    public void Escape_also_releases_the_focus_script()
    {
        var module = JSInterop.SetupModule(Module);
        module.Mode = JSRuntimeMode.Loose;
        AllThree();
        var cut = Open("/registrieren");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='register-link-privacy']")));
        cut.Find("[data-testid='register-link-privacy']").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='legal-overlay']")));

        cut.Find("[data-testid='legal-overlay']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        cut.WaitForAssertion(() => Assert.Single(module.Invocations, i => i.Identifier == "close"));
    }

    [Fact]
    public void The_clicked_link_is_handed_to_the_focus_script_as_the_opener()
    {
        var module = JSInterop.SetupModule(Module);
        module.Mode = JSRuntimeMode.Loose;
        AllThree();
        var cut = Open("/registrieren");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='register-link-terms']")));

        cut.Find("[data-testid='register-link-terms']").Click();

        cut.WaitForAssertion(() => Assert.Single(module.Invocations, i => i.Identifier == "open"));
        var opener = (Microsoft.AspNetCore.Components.ElementReference)module.Invocations.Single(i => i.Identifier == "open").Arguments[1]!;
        Assert.False(string.IsNullOrEmpty(opener.Id));
        var panel = (Microsoft.AspNetCore.Components.ElementReference)module.Invocations.Single(i => i.Identifier == "open").Arguments[0]!;
        Assert.NotEqual(panel.Id, opener.Id);
    }

    // Scheitert das Skript, bleibt die Seite dahinter bedienbar; dann darf das Fenster sich nicht als modal ausgeben.
    [Fact]
    public void A_failing_focus_script_leaves_the_dialog_without_aria_modal()
    {
        // Das Öffnen im Skript scheitert (der Import scheitert auf demselben Weg in denselben Fangblock).
        var module = JSInterop.SetupModule(Module);
        module.Mode = JSRuntimeMode.Loose;
        module.SetupVoid("open", _ => true).SetException(new Microsoft.JSInterop.JSException("kein Modul"));
        AllThree();
        var cut = Open("/registrieren");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='register-link-terms']")));

        cut.Find("[data-testid='register-link-terms']").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='legal-overlay']")));
        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid='legal-overlay']").HasAttribute("aria-modal")));
    }

    // Die Logik selbst steht in der Skriptdatei: alles außer dem Zweig mit dem Fenster wird inert, der Fokus merkt sich den Öffner.
    [Fact]
    public void The_script_makes_everything_around_the_panel_inert_and_returns_the_focus()
    {
        var script = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "legal-overlay.js"));

        Assert.Contains("export function open", script);
        Assert.Contains("export function close", script);
        Assert.Contains(".inert = true", script);
        Assert.Contains(".inert = false", script);
        Assert.Contains("document.activeElement", script);
        Assert.Contains("back.focus()", script);
    }
}
