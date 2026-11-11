using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Duofy.Schemas;
using Duofy.Shared.Layout;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Der Rahmen der App (Spec 65, Spec 82): untere Leiste, Titel in der oberen Leiste, Knöpfe im Seitenkopf wie im Browser.
/// Die Tests gehen über den echten Router der App (Routes), damit das Layout wirklich greift; der Browser behält MainLayout.
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
            .Select(a => (a.QuerySelector("span")!.TextContent.Trim(), a.GetAttribute("href"), a.GetAttribute("aria-current") == "page"))
            .ToList();

    private static string Active(IRenderedComponent<Routes> cut) =>
        Assert.Single(BottomBar(cut), e => e.Active).Text;

    private static void WaitForButton(IRenderedComponent<Routes> cut, string testId) =>
        cut.WaitForAssertion(() => Assert.False(cut.Find($"[data-testid='{testId}']").HasAttribute("disabled")));

    // A1
    [Fact]
    public void Accounts_open_inside_the_app_layout_with_five_entries_and_more_highlighted()
    {
        var cut = Open("/konten");

        cut.WaitForAssertion(() => Assert.NotEmpty(BottomBar(cut)));
        Assert.Single(cut.FindComponents<AppLayout>());
        Assert.Empty(cut.FindComponents<MainLayout>());
        Assert.Equal(
            [("Start", "/", false), ("Plan", "/plan", false), ("Haushalt", "/haushaltsplan", false), ("Sparen", "/sparen", false), ("Mehr", "/mehr", true)],
            BottomBar(cut));
    }

    // A2
    [Fact]
    public void Mehr_leads_to_mehr_and_stays_highlighted_for_konten()
    {
        var cut = Open("/");
        cut.WaitForAssertion(() => Assert.NotEmpty(BottomBar(cut)));
        var mehr = BottomBar(cut).Single(e => e.Text == "Mehr");
        Assert.Equal("/mehr", mehr.Href);

        Navigation.NavigateTo(mehr.Href!);
        cut.WaitForAssertion(() => Assert.Equal("Mehr", Active(cut)));

        Navigation.NavigateTo("/konten");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='accounts-add']")));
        Assert.Equal("Konten", cut.Find("h1").TextContent.Trim());
        Assert.Equal("Mehr", Active(cut));
    }

    // A3
    [Fact]
    public void Mehr_lists_the_profile_konten_vertraege_konto_and_sign_out_but_no_household_and_no_mode_switch()
    {
        var cut = Open("/mehr");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='more-accounts']")));
        Assert.Equal("/konten", cut.Find("[data-testid='more-accounts']").GetAttribute("href"));
        Assert.Equal("/vertraege", cut.Find("[data-testid='more-contracts']").GetAttribute("href"));
        Assert.Equal("/konto", cut.Find("[data-testid='more-user-account']").GetAttribute("href"));
        Assert.Empty(cut.FindAll("[data-testid='more-mode']"));
        Assert.Empty(cut.FindAll("[data-testid='more-household'], [data-testid='more-savings']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='more-profile'] [data-testid='member-choice-me']"));
        Assert.Single(cut.FindAll("[data-testid='theme-toggle']"));
        Assert.Contains("Abmelden", cut.Find("[data-testid='more-sign-out']").TextContent);
    }

    [Fact]
    public void The_one_theme_toggle_of_the_app_turns_dark_mode_on()
    {
        var cut = Open("/mehr");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='theme-toggle']")));

        cut.Find("[data-testid='theme-toggle']").Click();

        cut.WaitForAssertion(() => Assert.True(IsDarkMode(cut)));
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

    // A4, seit Spec 66 (A17): Der Plan ersetzt den Hinweis, der Knopf im Seitenkopf legt einen einmaligen Posten an.
    [Fact]
    public void Plan_shows_the_month_and_its_head_button_adds_a_position()
    {
        var cut = Open("/plan/2026/10");
        WaitForButton(cut, "plan-add");

        Assert.Empty(cut.FindAll("[data-testid='plan-note']"));
        Assert.Equal("Oktober 2026", cut.Find("h1").TextContent.Trim());
        Assert.Equal("Plan", Active(cut));
        cut.Find("[data-testid='plan-add']").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='position-label']")));
    }

    // A5
    [Fact]
    public void The_old_address_of_the_bookings_page_opens_the_current_month()
    {
        var cut = Open("/buchungen");

        cut.WaitForAssertion(() => Assert.EndsWith("/plan/2026/10", Navigation.Uri));
        cut.WaitForAssertion(() => Assert.Equal("Plan", Active(cut)));
    }

    [Fact]
    public void The_head_button_opens_the_account_dialog_on_konten()
    {
        var cut = Open("/konten");
        WaitForButton(cut, "accounts-add");

        cut.Find("[data-testid='accounts-add']").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='account-name']")));
    }

    [Fact]
    public void The_booking_button_on_start_opens_the_booking_dialog()
    {
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10);
        var cut = Open("/");
        WaitForButton(cut, "start-new-booking");

        cut.Find("[data-testid='start-new-booking']").Click();

        // Spec 51, A6: Der Knopf führt in den laufenden Monat, dort geht der Dialog auf; die Adresse ist danach sauber.
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='booking-amount']")));
        Assert.EndsWith("/plan/2026/10", Navigation.Uri);
        Assert.Equal("Plan", Active(cut));
    }

    // Spec 51, R2: Ohne Haushalt führt das Ziel "Haushalt" hierher, deshalb leuchtet es auch hier.
    [Fact]
    public void The_household_page_marks_the_household_target()
    {
        var cut = Open("/haushalt");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='household-card']")));
        Assert.Equal("Haushalt von Anna", cut.Find("h1").TextContent.Trim());
        Assert.Equal("Haushalt", Active(cut));
    }

    // Toms Entscheidung vom 02.11.: kein runder Plus-Knopf, auch nicht am Handy.
    [Theory]
    [InlineData("/", "start-new-booking")]
    [InlineData("/plan/2026/10", "plan-add")]
    [InlineData("/konten", "accounts-add")]
    [InlineData("/vertraege", "commitments-add")]
    [InlineData("/sparen", "savings-goal-add")]
    [InlineData("/mehr", "more-accounts")]
    [InlineData("/konto", "account-names")]
    public void No_page_shows_a_round_plus_button(string path, string readyMarker)
    {
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10);
        var cut = Open(path);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"[data-testid='{readyMarker}']")));
        Assert.Empty(cut.FindAll("[data-testid='app-plus']"));
        Assert.Empty(cut.FindAll(".app-fab"));
    }

    // A13 (R11, Tom: "Generell immer doppelte titel"): Jede Seite nennt ihren Titel genau einmal, auch nach dem Wechsel.
    [Theory]
    [InlineData("/konten", "Konten")]
    [InlineData("/mehr", "Mehr")]
    [InlineData("/vertraege", "Verträge")]
    [InlineData("/plan", "Meine Planung")]
    [InlineData("/sparen", "Sparen")]
    public void A13_every_page_names_its_title_exactly_once(string path, string title)
    {
        var cut = Open("/");
        Navigation.NavigateTo(path);

        cut.WaitForAssertion(() => Assert.Equal(title, cut.Find("h1").TextContent.Trim()));
        Assert.Empty(cut.FindAll("[data-testid='app-title']"));
        var main = cut.Find("main").TextContent + cut.Find(".shell-top").TextContent;
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(main, System.Text.RegularExpressions.Regex.Escape(title)).Count);
    }

    // A6
    [Fact]
    public void In_the_app_the_booking_dialog_is_the_step_dialog_with_cancel_in_its_head_and_next_below()
    {
        // Seit Issue #21 hat der Buchungsdialog das Dialog-Gerüst (Spec 11): Abbrechen im Kopf, Weiter unten.
        // Auf dem Handy füllt das Gerüst den Bildschirm über das Stylesheet (max-width 599 px), nicht über die Bibliothek.
        var cut = Open("/plan/2026/10?buchen=1");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".step-dialog")));
        var head = cut.Find(".step-dialog-head");
        Assert.Equal("Abbrechen", head.QuerySelector("[data-testid='step-dialog-cancel']")!.TextContent.Trim());
        Assert.Contains("Neue Buchung", head.TextContent);
        Assert.Equal("Weiter", cut.Find("[data-testid='booking-next']").TextContent.Trim());
        Assert.Empty(cut.FindAll("[data-testid='form-header']"));
    }

    [Fact]
    public void In_the_app_the_dialog_saves_with_the_same_checks_and_cancel_closes()
    {
        BookingApiFake.CreateError = Duofy.Schemas.ErrorCodes.LinesSumMismatch;
        var cut = Open("/plan/2026/10?buchen=1");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='booking-next']")));

        // Lehnt die API ab, steht der Fehler im Dialog wie im Browser.
        BookingDialogSteps.ToLast(cut);
        cut.Find("[data-testid='booking-save']").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='booking-error']")));

        cut.Find("[data-testid='step-dialog-cancel']").Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".step-dialog")));
    }

    // Spec 65 R4/R5/R6 für Verträge, nachdem Spec 64 gemerged ist.
    [Fact]
    public void The_head_button_opens_the_contract_dialog_on_vertraege()
    {
        var cut = Open("/vertraege");
        WaitForButton(cut, "commitments-add");

        cut.Find("[data-testid='commitments-add']").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='commitment-name']")));
    }

    // Spec 82: App und Browser zeigen dieselbe Seite, mit Überschrift und Knopf im Seitenkopf.
    [Fact]
    public void Vertraege_show_their_heading_and_button_in_the_page()
    {
        var cut = Open("/vertraege");

        cut.WaitForAssertion(() => Assert.Equal("Verträge", cut.Find("h1").TextContent.Trim()));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='commitments-add']")));
    }

    [Fact]
    public void In_the_app_the_contract_dialog_checks_each_step_and_cancelling_closes_it()
    {
        var cut = Open("/vertraege");
        WaitForButton(cut, "commitments-add");
        cut.Find("[data-testid='commitments-add']").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(DialogSelector + " .step-dialog")));

        // Weiter prüft wie im Browser: ohne Name bleibt der Dialog in Schritt 1 und zeigt den Fehler am Feld.
        cut.FindAll(".step-dialog .step-dialog-actions button").Single(b => b.TextContent.Trim() == "Weiter").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='commitment-name-field'] .step-field-error")));

        // Noch nichts eingegeben: Abbrechen schließt ohne Rückfrage.
        cut.Find(".step-dialog .step-dialog-head").QuerySelectorAll("button").Single(b => b.TextContent.Trim() == "Abbrechen").Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(DialogSelector + " .step-dialog")));
    }

    // A8 (seit Spec 10): Der Browser behält MainLayout, hat aber keine ausklappbare Seitenleiste mehr; die Navigation
    // sind die Seitenleiste (ab Tablet, am PC ausgeklappt) und die untere Leiste am Handy (fünf Ziele).
    [Fact]
    public async Task The_browser_keeps_main_layout_with_side_bar_and_bottom_bar_and_no_drawer()
    {
        await using var web = new WebContext();
        var layout = web.Render<MainLayout>(p => p.Add(l => l.Body, "<p>Seite</p>"));

        Assert.Empty(layout.FindAll("button[aria-label='Menü öffnen oder schließen']"));
        Assert.NotEmpty(layout.FindAll("[data-testid='side-nav']"));
        Assert.NotEmpty(layout.FindAll("[data-testid='bottom-bar']"));
        Assert.Single(layout.FindAll("[data-testid='theme-toggle']"));

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
        Assert.NotEmpty(accounts.FindAll("h1:not(.visually-hidden)"));
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

        public IRenderedComponent<IComponent> Dialogs() => RenderDialogHost();

        public IRenderedComponent<IComponent> OverlayHost() => RenderDialogHost();
    }

    // Spec 68, R8: Der Knopf der Startseite bucht für die gewählte Person; ohne deren Freigabe ist er ausgegraut.
    [Fact]
    public async Task The_start_button_is_greyed_out_with_the_reason_while_anna_does_not_let_ben_book()
    {
        HouseholdApiFake.Household = HouseholdApiFake.Household! with
        {
            Members = [FakeHouseholdApi.Anna(isMe: false) with { GrantsToMe = GrantLevels.None with { Plan = AccessLevelKeys.View, Book = AccessLevelKeys.View } }, FakeHouseholdApi.Ben(isMe: true)],
        };
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10);
        var member = Services.GetRequiredService<ActiveMember>();
        await member.LoadAsync();
        member.Choose(FakeHouseholdApi.AnnaId);

        var cut = Open("/");

        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find("[data-testid='start-new-booking']").GetAttribute("aria-disabled")));
        Assert.Equal(GrantLock.ReasonId, cut.Find("[data-testid='start-new-booking']").GetAttribute("aria-describedby"));
        Assert.Equal("Dafür hat dir Anna keine Freigabe gegeben.", cut.Find($"#{GrantLock.ReasonId}").TextContent.Trim());

        cut.Find("[data-testid='start-new-booking']").Click();

        Assert.Empty(cut.FindAll("[data-testid='booking-save']"));
        Assert.EndsWith("/", Navigation.Uri);
    }

    // Mangel S9: Auch das Layout der App hat genau ein main-Element für den Seiteninhalt.
    [Fact]
    public void The_app_layout_has_one_main_element_for_the_page_content()
    {
        var cut = Render<AppLayout>();

        var main = Assert.Single(cut.FindAll("main"));
        Assert.Equal("main-content", main.Id);
    }
}
