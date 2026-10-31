using System.Text.RegularExpressions;
using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Layout;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 65, das App-Layout: untere Leiste, Plus-Knopf, Titel in der oberen Leiste und Formulare im
/// Vollbild. Die Tests gehen über den echten Router der App (Routes), damit das Layout wirklich greift;
/// der Browser behält MainLayout.
/// </summary>
public class AppLayoutTests : PageTestContext
{
    public AppLayoutTests()
    {
        // Wie in MauiProgram: Nur die App meldet AppHost an.
        Services.AddSingleton<IAppHost, AppHost>();
        Services.AddSingleton<IThemePreferenceStore>(new MemoryThemeStore());
        Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true));
    }

    private IRenderedComponent<Routes> Open(string path)
    {
        Navigation.NavigateTo(path);
        return Render<Routes>();
    }

    private static IReadOnlyList<(string Text, string? Href, bool Active)> BottomBar(IRenderedComponent<Routes> cut) =>
        cut.FindAll("[data-testid='bottom-bar'] a")
            .Select(a => (a.TextContent.Trim(), a.GetAttribute("href"), a.GetAttribute("aria-current") == "page"))
            .ToList();

    private static string Active(IRenderedComponent<Routes> cut) =>
        Assert.Single(BottomBar(cut), e => e.Active).Text;

    private static void WaitForPlus(IRenderedComponent<Routes> cut) =>
        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid='app-plus']").HasAttribute("disabled")));

    // A1
    [Fact]
    public void Bookings_open_inside_the_app_layout_with_four_entries_and_bookings_highlighted()
    {
        var cut = Open("/buchungen");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='bookings-month']")));
        Assert.Single(cut.FindComponents<AppLayout>());
        Assert.Empty(cut.FindComponents<MainLayout>());
        Assert.Equal(
            [("Start", "/", false), ("Plan", "/plan", false), ("Buchungen", "/buchungen", true), ("Mehr", "/mehr", false)],
            BottomBar(cut));
    }

    // A2
    [Fact]
    public void Mehr_leads_to_mehr_and_stays_highlighted_for_konten()
    {
        var cut = Open("/buchungen");
        cut.WaitForAssertion(() => Assert.NotEmpty(BottomBar(cut)));
        var mehr = BottomBar(cut).Single(e => e.Text == "Mehr");
        Assert.Equal("/mehr", mehr.Href);

        Navigation.NavigateTo(mehr.Href!);
        cut.WaitForAssertion(() => Assert.Equal("Mehr", Active(cut)));

        Navigation.NavigateTo("/konten");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='app-plus']")));
        Assert.Equal("Konten", cut.Find("[data-testid='app-title']").TextContent.Trim());
        Assert.Equal("Mehr", Active(cut));
    }

    // A3
    [Fact]
    public void Mehr_lists_konten_vertraege_konto_the_mode_switch_and_sign_out()
    {
        var cut = Open("/mehr");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='more-accounts']")));
        Assert.Equal("/konten", cut.Find("[data-testid='more-accounts']").GetAttribute("href"));
        Assert.Equal("/vertraege", cut.Find("[data-testid='more-contracts']").GetAttribute("href"));
        Assert.Equal("/konto", cut.Find("[data-testid='more-user-account']").GetAttribute("href"));
        Assert.Equal(3, cut.FindAll("[data-testid='more-mode'] [role='radio']").Count);
        Assert.Contains("Abmelden", cut.Find("[data-testid='more-sign-out']").TextContent);
    }

    [Fact]
    public void The_mode_switch_on_mehr_turns_dark_mode_on()
    {
        var cut = Open("/mehr");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='more-mode']")));

        cut.Find("[data-testid='more-mode'] [data-testid='theme-dark']").Click();

        cut.WaitForAssertion(() => Assert.True(cut.FindComponent<MudThemeProvider>().Instance.IsDarkMode));
    }

    [Fact]
    public void Sign_out_on_mehr_signs_out_and_leads_to_the_sign_in()
    {
        var cut = Open("/mehr");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='more-sign-out']")));

        cut.Find("[data-testid='more-sign-out']").Click();

        cut.WaitForAssertion(() => Assert.Equal("/anmelden", CurrentPath));
        Assert.Null(Tokens.Tokens);
    }

    // A4, seit Spec 66 (A17): Der Plan ersetzt den Hinweis, das Plus legt einen einmaligen Posten an.
    [Fact]
    public void Plan_shows_the_month_and_its_plus_button_adds_a_position()
    {
        var cut = Open("/plan/2026/10");
        WaitForPlus(cut);

        Assert.Empty(cut.FindAll("[data-testid='plan-note']"));
        Assert.Equal("Plan", cut.Find("[data-testid='app-title']").TextContent.Trim());
        Assert.Equal("Plan", Active(cut));
        cut.Find("[data-testid='app-plus']").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='position-label']")));
    }

    // A5
    [Fact]
    public void The_plus_button_opens_the_booking_dialog_on_bookings()
    {
        var cut = Open("/buchungen");
        WaitForPlus(cut);

        cut.Find("[data-testid='app-plus']").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='booking-amount']")));
    }

    [Fact]
    public void The_plus_button_opens_the_account_dialog_on_konten()
    {
        var cut = Open("/konten");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='app-plus']")));

        cut.Find("[data-testid='app-plus']").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='account-name']")));
    }

    [Fact]
    public void The_plus_button_on_start_opens_the_booking_dialog()
    {
        var cut = Open("/");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='app-plus']")));

        cut.Find("[data-testid='app-plus']").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='booking-amount']")));
    }

    // Spec 67, A16: Der Haushalt liegt unter "Mehr" und lässt "Mehr" leuchten.
    [Fact]
    public void The_household_page_keeps_mehr_highlighted()
    {
        var cut = Open("/haushalt");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='household-card']")));
        Assert.Equal("Haushalt", cut.Find("[data-testid='app-title']").TextContent.Trim());
        Assert.Equal("Mehr", Active(cut));
    }

    [Theory]
    [InlineData("/mehr", "more-accounts")]
    [InlineData("/konto", "account-names")]
    public void Pages_without_a_main_action_show_no_plus_button(string path, string readyMarker)
    {
        var cut = Open(path);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"[data-testid='{readyMarker}']")));
        Assert.Empty(cut.FindAll("[data-testid='app-plus']"));
    }

    [Fact]
    public void Leaving_a_page_with_a_plus_button_takes_the_button_away()
    {
        var cut = Open("/konten");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='app-plus']")));

        Navigation.NavigateTo("/mehr");

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='app-plus']")));
        Assert.Equal("Mehr", cut.Find("[data-testid='app-title']").TextContent.Trim());
    }

    // A6
    [Fact]
    public void In_the_app_the_booking_dialog_is_full_screen_with_close_and_save_in_its_header()
    {
        var cut = Open("/buchungen");
        WaitForPlus(cut);

        cut.Find("[data-testid='app-plus']").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-fullscreen")));
        var header = cut.Find("[data-testid='form-header']");
        Assert.NotNull(header.QuerySelector("[data-testid='form-close']"));
        Assert.Equal("Speichern", header.QuerySelector("[data-testid='booking-save']")!.TextContent.Trim());
        Assert.Contains("Neue Buchung", header.TextContent);
    }

    [Fact]
    public void In_the_app_the_header_saves_with_the_same_checks_and_closing_cancels()
    {
        BookingApiFake.CreateError = Duofy.Schemas.ErrorCodes.LinesSumMismatch;
        var cut = Open("/buchungen");
        WaitForPlus(cut);
        cut.Find("[data-testid='app-plus']").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='form-header']")));

        // Lehnt die API ab, steht der Fehler im Dialog wie im Browser.
        cut.Find("[data-testid='booking-save']").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='booking-error']")));

        cut.Find("[data-testid='form-close']").Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='form-header']")));
    }

    [Fact]
    public void In_the_browser_the_same_dialog_is_not_full_screen()
    {
        // Ohne AppHost-Anmeldung gilt der Standard aus AddDuofyUi: der Browser.
        using var web = new WebContext();
        web.Render<MudPopoverProvider>();
        var dialogs = web.Render<MudDialogProvider>();
        var page = web.Render<Bookings>();
        page.WaitForAssertion(() => Assert.False(page.Find("[data-testid='bookings-add']").HasAttribute("disabled")));

        page.Find("[data-testid='bookings-add']").Click();

        // Der Dialog steht im MudDialogProvider, nicht in der Seite.
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='booking-amount']")));
        Assert.Empty(dialogs.FindAll(".mud-dialog-fullscreen"));
        Assert.Empty(dialogs.FindAll("[data-testid='form-header']"));
        Assert.NotEmpty(dialogs.FindAll("[data-testid='booking-save']"));
    }

    // Spec 65 R4/R5/R6 für Verträge, nachdem Spec 64 gemerged ist.
    [Fact]
    public void The_plus_button_opens_the_contract_dialog_on_vertraege()
    {
        var cut = Open("/vertraege");
        WaitForPlus(cut);

        cut.Find("[data-testid='app-plus']").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='commitment-name']")));
    }

    [Fact]
    public void Vertraege_show_their_title_in_the_top_bar_only_once()
    {
        var cut = Open("/vertraege");

        cut.WaitForAssertion(() => Assert.Equal("Verträge", cut.Find("[data-testid='app-title']").TextContent.Trim()));
        Assert.Empty(cut.FindAll(".mud-typography-h4"));
        Assert.Empty(cut.FindAll("[data-testid='commitments-add']"));
    }

    // Spec 17: Der Vertragsdialog ist in der App der Schritt-Dialog (Spec 11). Auf dem Handy füllt er über das
    // Stylesheet den Bildschirm (bis 599 px, siehe StepDialogTests); Abbrechen steht im Kopf, Weiter unten.
    [Fact]
    public void In_the_app_the_contract_dialog_is_the_step_dialog_with_cancel_on_top_and_next_below()
    {
        var cut = Open("/vertraege");
        WaitForPlus(cut);

        cut.Find("[data-testid='app-plus']").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog.step-dialog")));
        Assert.Empty(cut.FindAll("[data-testid='form-header']"));
        var head = cut.Find(".step-dialog .mud-dialog-title");
        Assert.Contains("Neuer Vertrag", head.TextContent);
        Assert.Equal("Schritt 1 von 3: Was", head.QuerySelector("[data-testid='step-dialog-progress']")!.TextContent.Trim());
        Assert.Contains(head.QuerySelectorAll("button"), b => b.TextContent.Trim() == "Abbrechen");
        Assert.Contains(cut.FindAll(".step-dialog .mud-dialog-actions button"), b => b.TextContent.Trim() == "Weiter");
    }

    [Fact]
    public void In_the_app_the_contract_dialog_checks_each_step_and_cancelling_closes_it()
    {
        var cut = Open("/vertraege");
        WaitForPlus(cut);
        cut.Find("[data-testid='app-plus']").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog.step-dialog")));

        // Weiter prüft wie im Browser: ohne Name bleibt der Dialog in Schritt 1 und zeigt den Fehler am Feld.
        cut.FindAll(".step-dialog .mud-dialog-actions button").Single(b => b.TextContent.Trim() == "Weiter").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='commitment-name-field'] .step-field-error")));

        // Noch nichts eingegeben: Abbrechen schließt ohne Rückfrage.
        cut.Find(".step-dialog .mud-dialog-title").QuerySelectorAll("button").Single(b => b.TextContent.Trim() == "Abbrechen").Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-dialog.step-dialog")));
    }

    [Fact]
    public void In_the_browser_the_contract_dialog_is_not_full_screen()
    {
        using var web = new WebContext();
        web.Render<MudPopoverProvider>();
        var dialogs = web.Render<MudDialogProvider>();
        var page = web.Render<Commitments>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[data-testid='commitments-add']")));

        page.Find("[data-testid='commitments-add']").Click();

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='commitment-name']")));
        Assert.Empty(dialogs.FindAll(".mud-dialog-fullscreen"));
        Assert.Empty(dialogs.FindAll("[data-testid='form-header']"));
        Assert.NotEmpty(dialogs.FindAll(".mud-dialog.step-dialog"));
    }

    // A7
    [Fact]
    public void Konten_show_their_title_in_the_top_bar_and_the_bar_has_no_menu_button()
    {
        var cut = Open("/konten");

        cut.WaitForAssertion(() => Assert.Equal("Konten", cut.Find("[data-testid='app-title']").TextContent.Trim()));
        Assert.Empty(cut.FindAll("button[aria-label='Menü öffnen oder schließen']"));
        Assert.Empty(cut.FindComponents<MudDrawer>());
        Assert.Empty(cut.FindAll(".mode-switch"));
        // Der Titel steht nur einmal da: in der Leiste, nicht noch einmal als Überschrift der Seite.
        Assert.Empty(cut.FindAll(".mud-typography-h4"));
    }

    // A8 (seit Spec 10): Der Browser behält MainLayout, hat aber keine ausklappbare Seitenleiste mehr; die Navigation
    // sind die Seitenleiste (ab 1280 px) und die untere Leiste, beide aus derselben Liste.
    [Fact]
    public void The_browser_keeps_main_layout_with_side_bar_and_bottom_bar_and_no_drawer()
    {
        using var web = new WebContext();
        var layout = web.Render<MainLayout>(p => p.Add(l => l.Body, "<p>Seite</p>"));

        Assert.Empty(layout.FindComponents<MudDrawer>());
        Assert.Empty(layout.FindAll("button[aria-label='Menü öffnen oder schließen']"));
        Assert.NotEmpty(layout.FindAll("[data-testid='side-nav']"));
        Assert.NotEmpty(layout.FindAll("[data-testid='bottom-bar']"));
        Assert.NotEmpty(layout.FindAll(".mode-switch"));

        // Der Router des Browsers (WebRoutes.razor) bleibt bei MainLayout und kennt AppLayout nicht.
        var webRoutes = File.ReadAllText(RepositoryFiles.Path("Web", "Duofy.Web", "Components", "WebRoutes.razor"));
        Assert.Contains("DefaultLayout=\"typeof(MainLayout)\"", webRoutes);
        Assert.DoesNotContain("AppLayout", webRoutes);
    }

    [Fact]
    public void In_the_browser_the_pages_keep_their_own_heading_and_add_buttons()
    {
        using var web = new WebContext();
        var accounts = web.Render<Duofy.Shared.Pages.Accounts>();

        accounts.WaitForAssertion(() => Assert.NotEmpty(accounts.FindAll("[data-testid='accounts-add']")));
        Assert.NotEmpty(accounts.FindAll(".mud-typography-h4"));
    }

    // A9
    [Theory]
    [InlineData(".app-bottom-bar")]
    [InlineData(".app-fab.mud-fab")]
    public void Bottom_bar_and_plus_button_reserve_the_bottom_safe_area_inset(string selector)
    {
        var css = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "app.css"));

        var rule = Regex.Match(css, Regex.Escape(selector) + @"\s*\{(?<body>[^}]*)\}");

        Assert.True(rule.Success, $"{selector} fehlt in app.css");
        Assert.Contains("env(safe-area-inset-bottom", rule.Groups["body"].Value);
    }

    [Fact]
    public void The_layout_renders_the_elements_the_safe_area_styles_belong_to()
    {
        var cut = Open("/buchungen");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".app-fab")));
        Assert.NotEmpty(cut.FindAll("nav.app-bottom-bar"));
    }

    private sealed class MemoryThemeStore : IThemePreferenceStore
    {
        public Task<ThemeChoice> LoadAsync() => Task.FromResult(ThemeChoice.Device);

        public Task SaveAsync(ThemeChoice choice) => Task.CompletedTask;
    }

    // Ein zweiter Testunterbau ohne AppHost-Anmeldung: so läuft die Oberfläche im Browser.
    private sealed class WebContext : PageTestContext
    {
        public WebContext()
        {
            Services.AddSingleton<IThemePreferenceStore>(new MemoryThemeStore());
            Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true));
        }
    }

    // Spec 44, A1 und A6: Der Profil-Knopf steht in der oberen Leiste, auch wenn niemand etwas teilt und der
    // Haushalt nur aus einer Person besteht; dieselbe Komponente wie in der Seitenleiste des Browsers.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_member_switch_sits_in_the_top_bar_whether_or_not_somebody_shares(bool shares)
    {
        HouseholdApiFake.Household = HouseholdApiFake.Household! with
        {
            Members = shares
                ? [FakeHouseholdApi.Anna(), FakeHouseholdApi.Ben() with { GrantsToMe = GrantLevels.None with { Plan = AccessLevelKeys.View } }]
                : [FakeHouseholdApi.Ben(isMe: true)],
        };

        var cut = Open("/plan/2026/10");

        OpenPlanTab(cut);
        Assert.Equal(1, cut.FindAll(".mud-appbar [data-testid='member-switch']").Count);
        Assert.Single(cut.FindAll(".mud-appbar [data-testid='member-profile']"));
    }

    // Spec 68, R8: Das Plus der Startseite bucht für die gewählte Person; ohne deren Freigabe ist es ausgegraut.
    [Fact]
    public async Task The_start_plus_is_greyed_out_with_the_reason_while_anna_does_not_let_ben_book()
    {
        HouseholdApiFake.Household = HouseholdApiFake.Household! with
        {
            Members = [FakeHouseholdApi.Anna(isMe: false) with { GrantsToMe = GrantLevels.None with { Book = AccessLevelKeys.View } }, FakeHouseholdApi.Ben(isMe: true)],
        };
        var member = Services.GetRequiredService<ActiveMember>();
        await member.LoadAsync();
        member.Choose(FakeHouseholdApi.AnnaId);

        var cut = Open("/");

        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find("[data-testid='app-plus']").GetAttribute("aria-disabled")));
        Assert.Equal(GrantLock.ReasonId, cut.Find("[data-testid='app-plus']").GetAttribute("aria-describedby"));
        Assert.Equal("Dafür hat dir Anna keine Freigabe gegeben.", cut.Find($"#{GrantLock.ReasonId}").TextContent.Trim());

        cut.Find("[data-testid='app-plus']").Click();

        Assert.Empty(cut.FindAll("[data-testid='booking-save']"));
    }
}
