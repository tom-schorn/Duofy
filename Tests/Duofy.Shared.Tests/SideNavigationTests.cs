using System.Text.RegularExpressions;
using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Layout;
using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 10, die Seitennavigation für den Desktop: ab 1280 px links eine Seitenleiste mit dem vollen Menü (Tom,
/// 01.11.2026), darunter die untere Leiste mit vier Zielen und "Mehr". Ein echter Browser fehlt; ob eine Leiste
/// sichtbar ist, steht im Text des Stylesheets, was sie zeigt, in der gezeichneten Struktur.
/// </summary>
public class SideNavigationTests : PageTestContext
{
    public SideNavigationTests()
    {
        Services.AddSingleton<IAppHost, AppHost>();
        Services.AddSingleton<IThemePreferenceStore>(new MemoryThemeStore());
        Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true));
    }

    private static string Css() => File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "duofy.css"));

    private static string Rule(string css, string selector)
    {
        var match = Regex.Match(css, @"(?<![\w.-])" + Regex.Escape(selector) + @"\s*\{([^}]*)\}");
        Assert.True(match.Success, $"Regel '{selector}' fehlt");
        return match.Groups[1].Value;
    }

    private static string DesktopMedia(string css)
    {
        // Ein Bereich darf an mehreren Stellen stehen (Werte oben, Regeln bei ihren Bausteinen); gelesen wird alles zusammen.
        var found = new System.Text.StringBuilder();
        var from = 0;
        while ((from = css.IndexOf($"@media (min-width: {WidthRanges.DesktopMin}px)", from, StringComparison.Ordinal)) >= 0)
        {
            var open = css.IndexOf('{', from);
            var depth = 0;
            var i = open;
            for (; i < css.Length; i++)
            {
                if (css[i] == '{') depth++;
                else if (css[i] == '}' && --depth == 0) break;
            }
            found.Append(css, open + 1, i - open - 1).AppendLine();
            from = i;
        }
        Assert.True(found.Length > 0, "Desktop-Bereich fehlt");
        return found.ToString();
    }

    private static IReadOnlyList<(string Text, string? Href)> Entries<T>(IRenderedComponent<T> cut, string testId)
        where T : Microsoft.AspNetCore.Components.IComponent =>
        cut.FindAll($"[data-testid='{testId}'] a").Select(a => (Label(a), a.GetAttribute("href"))).ToList();

    private static string Label(AngleSharp.Dom.IElement entry) => entry.QuerySelector("span")!.TextContent.Trim();

    // Das Benutzermenü öffnen; seine Einträge stehen in der Hülle für Dialoge und Menüs.
    private IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> OpenUserMenu()
    {
        var host = RenderDialogHost();
        var menu = Render<UserMenu>();
        menu.WaitForAssertion(() => Assert.NotEmpty(menu.FindAll("[data-testid='user-menu-open']")));
        menu.Find("[data-testid='user-menu-open']").Click();
        return host;
    }

    // A1 (R1): Beide Layouts zeichnen beide Leisten; welche man sieht, entscheidet die Breite.
    [Fact]
    public void The_web_layout_draws_the_side_bar_and_the_bottom_bar()
    {
        var cut = Render<MainLayout>();

        Assert.Single(cut.FindAll("[data-testid='side-nav']"));
        Assert.Single(cut.FindAll("[data-testid='bottom-bar']"));
    }

    [Fact]
    public void The_app_layout_draws_the_side_bar_and_the_bottom_bar()
    {
        var cut = Render<AppLayout>();

        Assert.Single(cut.FindAll("[data-testid='side-nav']"));
        Assert.Single(cut.FindAll("[data-testid='bottom-bar']"));
    }

    // A14 (R12, Tom, 03.11.2026): Genau diese fünf; die Seitenleiste zeigt direkt, was unten hinter "Mehr" liegt.
    [Fact]
    public void A14_the_bottom_bar_has_the_five_targets_of_the_human()
    {
        (string, string?)[] expected = [("Start", "/"), ("Plan", "/plan"), ("Haushalt", "/haushaltsplan"), ("Sparen", "/sparen"), ("Mehr", "/mehr")];
        Assert.Equal(expected.Select(e => e.Item2), AppDestinations.All.Select(d => d.Href));

        var cut = Render<MainLayout>();

        Assert.Equal(expected, Entries(cut, "bottom-bar"));
    }

    [Fact]
    public void The_side_bar_shows_everything_directly_and_no_more_entry()
    {
        var cut = Render<MainLayout>();

        cut.WaitForAssertion(() => Assert.Equal(
            AppDestinations.Side.Select(d => d.Href),
            Entries(cut, "side-nav").Select(e => e.Href)));
        Assert.Contains(("Start", "/"), Entries(cut, "side-nav"));
        Assert.Contains(("Meine Planung", "/plan"), Entries(cut, "side-nav"));
        Assert.Contains(("Verträge", "/vertraege"), Entries(cut, "side-nav"));
        Assert.DoesNotContain(Entries(cut, "side-nav"), e => e.Href == "/mehr");
        Assert.DoesNotContain(Entries(cut, "side-nav"), e => e.Text == "Mitglieder" || e.Href == "/haushalt");
    }

    // A13 (R11): Der Haushalt steht in der Seitenleiste unter seinem Namen.
    [Fact]
    public void A13_the_household_entry_carries_the_name_of_the_household()
    {
        var cut = Render<MainLayout>();

        cut.WaitForAssertion(() => Assert.Contains(("Haushalt von Anna", "/haushaltsplan"), Entries(cut, "side-nav")));
    }

    [Fact]
    public void The_user_menu_shows_admin_only_to_admins()
    {
        var host = OpenUserMenu();
        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll("[data-testid='side-licenses']")));
        Assert.Empty(host.FindAll("[data-testid='side-admin']"));
    }

    [Fact]
    public void The_user_menu_leads_admins_to_the_administration()
    {
        Users.Me = Users.Me with { IsInstanceAdmin = true };

        var host = OpenUserMenu();

        host.WaitForAssertion(() => Assert.Equal("/admin", host.Find("[data-testid='side-admin']").GetAttribute("href")));
    }

    [Fact]
    public void The_user_menu_lists_the_legal_texts_of_the_server()
    {
        LegalApiFake.Documents["imprint"] = "Max Muster";

        var host = OpenUserMenu();

        host.WaitForAssertion(() => Assert.Equal("/rechtliches/impressum", host.Find("[data-testid='side-legal-imprint']").GetAttribute("href")));
        Assert.Empty(host.FindAll("[data-testid='side-legal-privacy']"));
    }

    // Review 07.11.: Mit einer äußeren Adresse führt der Eintrag im Benutzermenü dorthin, nicht zur Seite des Servers.
    [Fact]
    public void The_user_menu_uses_the_external_address_of_a_text_when_there_is_one()
    {
        LegalApiFake.Documents["imprint"] = "Max Muster";
        LegalApiFake.Urls["privacy"] = "https://example.org/datenschutz";

        var host = OpenUserMenu();

        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll("[data-testid='side-legal-privacy']")));
        Assert.Equal("https://example.org/datenschutz", host.Find("[data-testid='side-legal-privacy']").GetAttribute("href"));
        Assert.Equal("/rechtliches/impressum", host.Find("[data-testid='side-legal-imprint']").GetAttribute("href"));
    }

    [Fact]
    public void Sign_out_in_the_side_bar_signs_out_and_leads_to_the_sign_in()
    {
        var host = OpenUserMenu();
        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll("[data-testid='side-sign-out']")));

        host.Find("[data-testid='side-sign-out']").Click();

        host.WaitForAssertion(() => Assert.Equal("/anmelden", CurrentPath));
        Assert.Null(Tokens.Tokens);
    }

    // A13 (R11, Tom, 03.11.2026: "1 hätte geriecht"): ein Knopf für hell und dunkel in der oberen Leiste, in App und Browser gleich.
    [Fact]
    public void A13_the_app_shows_one_theme_toggle_in_the_top_bar()
    {
        var app = Render<AppLayout>();

        app.WaitForAssertion(() => Assert.Single(app.FindAll("[data-testid='theme-toggle']")));
        Assert.Single(app.FindAll(".shell-top [data-testid='theme-toggle']"));
    }

    [Fact]
    public void A13_the_browser_shows_one_theme_toggle_in_the_top_bar_only()
    {
        var web = Render<MainLayout>();

        web.WaitForAssertion(() => Assert.Single(web.FindAll("[data-testid='theme-toggle']")));
        Assert.Single(web.FindAll(".shell-top [data-testid='theme-toggle']"));
        Assert.Empty(web.FindAll(".shell-side [data-testid='theme-toggle']"));
    }

    [Fact]
    public void The_marker_follows_the_page_when_the_address_changes()
    {
        Navigation.NavigateTo("/plan");
        var cut = Render<SideNavigation>();

        Navigation.NavigateTo("/konten");

        cut.WaitForAssertion(() =>
            Assert.Equal("Konten", Label(Assert.Single(cut.FindAll("a"), a => a.GetAttribute("aria-current") == "page"))));
    }

    // Unterseiten lassen ihren Eintrag leuchten; "/konto" ist nicht "/konten".
    [Theory]
    [InlineData("/haushalt", "Haushalt von Anna")]
    [InlineData("/haushalt/freigaben", "Haushalt von Anna")]
    [InlineData("/haushalt/richtwerte", "Haushalt von Anna")]
    [InlineData("/haushaltsplan/2026/11", "Haushalt von Anna")]
    [InlineData("/plan/2026/11", "Meine Planung")]
    [InlineData("/konten", "Konten")]
    public void Sub_pages_mark_their_entry(string path, string label)
    {
        Navigation.NavigateTo(path);

        var cut = Render<SideNavigation>();

        cut.WaitForAssertion(() =>
            Assert.Equal(label, Label(Assert.Single(cut.FindAll("a"), a => a.GetAttribute("aria-current") == "page"))));
    }

    // Was im Benutzermenü steht, hat in der Liste keinen Eintrag; dort leuchtet dann nichts.
    [Theory]
    [InlineData("/konto/loeschen")]
    [InlineData("/lizenzen")]
    public void Pages_of_the_user_menu_mark_no_entry(string path)
    {
        Navigation.NavigateTo(path);

        var cut = Render<SideNavigation>();

        Assert.Empty(cut.FindAll("a[aria-current='page']"));
    }

    // Spec 51, R3: Keine Leiste führt mehr zu den Buchungen.
    [Fact]
    public void No_menu_has_an_entry_for_bookings()
    {
        Assert.DoesNotContain(AppDestinations.All.Concat(AppDestinations.Side), d => d.Href == "/buchungen");
        var more = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "Pages", "Mehr.razor"));
        Assert.DoesNotContain("/buchungen", more);
    }

    // Spec 51, R1, mit Spec 82 R12: Verträge liegen unter "Mehr" und lassen es leuchten; Sparen hat ein eigenes Ziel.
    [Theory]
    [InlineData("/sparen", "Sparen")]
    [InlineData("/vertraege", "Mehr")]
    [InlineData("/haushaltsplan", "Haushalt")]
    [InlineData("/haushaltsplan/2026/11", "Haushalt")]
    [InlineData("/haushalt", "Haushalt")]
    public void The_bottom_bar_marks_the_right_target(string path, string label)
    {
        Navigation.NavigateTo(path);

        var cut = Render<BottomNavigation>();

        Assert.Equal(label, Label(Assert.Single(cut.FindAll("a"), a => a.GetAttribute("aria-current") == "page")));
    }

    // Unten leuchtet für dieselbe Seite weiter "Mehr".
    [Fact]
    public void The_bottom_bar_still_marks_more_for_the_accounts_page()
    {
        Navigation.NavigateTo("/konten");

        var cut = Render<BottomNavigation>();

        Assert.Equal("Mehr", Label(Assert.Single(cut.FindAll("a"), a => a.GetAttribute("aria-current") == "page")));
    }

    // A5 (R5)
    [Fact]
    public void An_additional_destination_appears_in_both_bars()
    {
        IReadOnlyList<(AppDestination Destination, string Href, string LabelKey, string Icon)> list =
            [.. AppDestinations.All, (AppDestination.More, "/extra", "Nav_Extra", "star")];

        var side = Render<SideNavigation>(p => p.Add(c => c.Destinations, list));
        var bottom = Render<BottomNavigation>(p => p.Add(c => c.Destinations, list));

        Assert.Contains(side.FindAll("[data-testid='side-nav'] a"), a => a.GetAttribute("href") == "/extra");
        Assert.Equal(list.Count, bottom.FindAll("a").Count);
        Assert.Equal("/extra", bottom.FindAll("a").Last().GetAttribute("href"));
    }

    // A5 (R5): Weder die Seitenleiste noch die untere Leiste kennen ein Ziel selbst; beide lesen nur die Liste.
    [Theory]
    [InlineData("SideNavigation.razor")]
    [InlineData("BottomNavigation.razor")]
    public void The_bars_do_not_name_destinations_themselves(string file)
    {
        var markup = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "Layout", file));

        Assert.DoesNotContain("\"/plan\"", markup);
        Assert.DoesNotContain("\"/buchungen\"", markup);
        Assert.DoesNotContain("Nav_Plan", markup);
    }

    // A13 (R11): Die Seitenleiste hat oben die Marke, dann die Ziele und unten das Profil-Menü mit der Auswahl der Person,
    // auch wenn die Person allein ist; ohne Wahl zeigt das Profil die E-Mail statt einer Ansicht.
    [Fact]
    public void A13_the_side_bar_has_the_profile_menu_at_the_bottom_even_when_alone()
    {
        HouseholdApiFake.Household = HouseholdApiFake.Household! with { Members = [FakeHouseholdApi.Ben(isMe: true)] };

        var cut = Render<MainLayout>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".shell-side [data-testid='member-profile']")));
        var order = cut.Find(".shell-side").Children.Select(c => c.GetAttribute("data-testid") ?? c.ClassName).ToList();
        Assert.Equal(["shell-brand", "side-nav", "user-menu"], order);
        Assert.DoesNotContain("Ansicht", cut.Find(".shell-side [data-testid='member-profile']").TextContent);
    }

    // Ohne Fuß zeigt die Seitenleiste nur die Ziele.
    [Fact]
    public void The_side_navigation_without_a_footer_shows_only_the_destinations()
    {
        var side = Render<SideNavigation>();

        Assert.Empty(side.FindAll("[data-testid='member-profile']"));
    }

    private sealed class MemoryThemeStore : IThemePreferenceStore
    {
        public Task<ThemeChoice> LoadAsync() => Task.FromResult(ThemeChoice.Device);

        public Task SaveAsync(ThemeChoice choice) => Task.CompletedTask;
    }
}
