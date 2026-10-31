using System.Text.RegularExpressions;
using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Layout;
using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 10, die Seitennavigation für den Desktop: ab 1280 px links eine Seitenleiste, darunter die untere
/// Leiste, beide aus derselben Liste der Ziele. Ein echter Browser fehlt; ob eine Leiste sichtbar ist, steht
/// im Text des Stylesheets, was sie zeigt, in der gezeichneten Struktur.
/// </summary>
public class SideNavigationTests : PageTestContext
{
    public SideNavigationTests()
    {
        Services.AddSingleton<IAppHost, AppHost>();
        Services.AddSingleton<IThemePreferenceStore>(new MemoryThemeStore());
        Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true));
    }

    private static string Css() => File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "app.css"));

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
        cut.FindAll($"[data-testid='{testId}'] a").Select(a => (a.TextContent.Trim(), a.GetAttribute("href"))).ToList();

    // A1 (R1): Das Stylesheet zeigt unter dem Desktop nur die untere Leiste und ab dem Desktop nur die Seitenleiste.
    [Fact]
    public void From_1280px_the_side_bar_is_shown_and_the_bottom_bar_is_hidden()
    {
        var css = Css();
        Assert.Contains("display: none", Rule(css, ".app-side-nav"));

        var desktop = DesktopMedia(css);
        Assert.Matches(@"display:\s*flex", Rule(desktop, ".app-side-nav"));
        Assert.Contains("display: none", Rule(desktop, ".app-bottom-bar"));
        Assert.DoesNotContain("display: none", Rule(css, ".app-bottom-bar"));
    }

    // A1 (R1): Beide Layouts zeichnen beide Leisten; welche man sieht, entscheidet die Breite.
    [Fact]
    public void The_web_layout_draws_the_side_bar_and_the_bottom_bar()
    {
        var cut = Render<MainLayout>();

        Assert.Single(cut.FindAll("[data-testid='side-nav']"));
        Assert.Single(cut.FindAll("[data-testid='bottom-bar']"));
        Assert.Empty(cut.FindComponents<MudDrawer>());
    }

    [Fact]
    public void The_app_layout_draws_the_side_bar_and_the_bottom_bar()
    {
        var cut = Render<AppLayout>();

        Assert.Single(cut.FindAll("[data-testid='side-nav']"));
        Assert.Single(cut.FindAll("[data-testid='bottom-bar']"));
    }

    // A2 (R2)
    [Fact]
    public void Both_bars_show_the_same_entries_in_the_same_order_as_the_list()
    {
        (string, string?)[] expected = [("Start", "/"), ("Plan", "/plan"), ("Buchungen", "/buchungen"), ("Mehr", "/mehr")];
        Assert.Equal(expected.Select(e => e.Item2), AppDestinations.All.Select(d => d.Href));

        var cut = Render<MainLayout>();

        Assert.Equal(expected, Entries(cut, "side-nav"));
        Assert.Equal(expected, Entries(cut, "bottom-bar"));
    }

    // A3 (R3)
    [Fact]
    public void The_open_page_is_marked_in_the_side_bar()
    {
        Navigation.NavigateTo("/plan");

        var cut = Render<SideNavigation>();

        var active = Assert.Single(cut.FindAll("a"), a => a.GetAttribute("aria-current") == "page");
        Assert.Equal("Plan", active.TextContent.Trim());
        Assert.Contains("app-side-item-active", active.ClassList);
        // Die Markierung sieht man: Balken am Rand und fetter Text, nicht nur die Farbe.
        var marker = Rule(Css(), ".app-side-item-active");
        Assert.Contains("border-left", marker);
        Assert.Contains("font-weight", marker);
    }

    [Fact]
    public void The_marker_follows_the_page_when_the_address_changes()
    {
        Navigation.NavigateTo("/plan");
        var cut = Render<SideNavigation>();

        Navigation.NavigateTo("/konten");

        cut.WaitForAssertion(() =>
            Assert.Equal("Mehr", Assert.Single(cut.FindAll("a"), a => a.GetAttribute("aria-current") == "page").TextContent.Trim()));
    }

    // A4 (R4)
    [Fact]
    public void All_entries_are_links_in_list_order_and_keep_the_focus_outline()
    {
        var cut = Render<SideNavigation>();

        var links = cut.FindAll("[data-testid='side-nav'] a");
        Assert.Equal(AppDestinations.All.Select(d => d.Href), links.Select(a => a.GetAttribute("href")));
        Assert.All(links, a => Assert.True(a.GetAttribute("tabindex") is null or "0"));

        var css = Css();
        Assert.Contains("outline: 3px solid", Rule(css, ":focus-visible"));
        Assert.DoesNotContain("outline: none", Rule(css, ".app-side-item"));
        Assert.DoesNotContain("outline: 0", Rule(css, ".app-side-item"));
    }

    // A4 (R4): Jeder Eintrag ist eine Tippfläche von mindestens 44 px.
    [Fact]
    public void Side_entries_are_at_least_44px_high()
    {
        Assert.Contains("min-height: var(--tap-min)", Rule(Css(), ".app-side-item"));
    }

    // A5 (R5)
    [Fact]
    public void An_additional_destination_appears_in_both_bars()
    {
        IReadOnlyList<(AppDestination Destination, string Href, string LabelKey, string Icon)> list =
            [.. AppDestinations.All, (AppDestination.More, "/extra", "Nav_Extra", MudBlazor.Icons.Material.Filled.Star)];

        var side = Render<SideNavigation>(p => p.Add(c => c.Destinations, list));
        var bottom = Render<BottomNavigation>(p => p.Add(c => c.Destinations, list));

        Assert.Equal(list.Count, side.FindAll("a").Count);
        Assert.Equal(list.Count, bottom.FindAll("a").Count);
        Assert.Equal("/extra", side.FindAll("a").Last().GetAttribute("href"));
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

    // Der Umschalter "wessen Zahlen" (Spec 68) wohnt im Browser in der oberen Leiste, nicht in der Seitenleiste.
    [Fact]
    public void The_web_layout_keeps_the_member_switch_in_the_app_bar_when_somebody_shares()
    {
        HouseholdApiFake.Household = HouseholdApiFake.Household! with
        {
            Members = [FakeHouseholdApi.Anna(), FakeHouseholdApi.Ben() with { GrantsToMe = GrantLevels.None with { Book = AccessLevelKeys.View } }],
        };

        var cut = Render<MainLayout>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-appbar [data-testid='member-switch']")));
    }

    // Spec 44, A6 (R5): Unten in der Seitenleiste steht der Profil-Knopf, auch wenn die Person allein ist, und
    // zwar nach den Zielen.
    [Fact]
    public void The_web_layout_puts_the_profile_button_at_the_bottom_of_the_side_navigation_even_when_alone()
    {
        HouseholdApiFake.Household = HouseholdApiFake.Household! with { Members = [FakeHouseholdApi.Ben(isMe: true)] };

        var cut = Render<MainLayout>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='side-nav'] [data-testid='member-profile']")));
        var nav = cut.Find("[data-testid='side-nav']");
        Assert.Contains("member-profile", nav.LastElementChild!.InnerHtml);
        Assert.Equal("Ich", cut.Find("[data-testid='side-nav'] [data-testid='member-profile']").TextContent.Trim());
    }

    // Ohne Fuß zeigt die Seitenleiste nur die Ziele.
    [Fact]
    public void The_side_navigation_without_a_footer_shows_only_the_destinations()
    {
        var side = Render<SideNavigation>();

        Assert.Empty(side.FindAll("[data-testid='member-profile']"));
    }

    // Spec 44, A6 (R5): Ab dem Desktop steht der Knopf unten in der Seitenleiste und das Menü geht nach oben auf;
    // die obere Leiste zeigt ihn dann nicht doppelt.
    [Fact]
    public void The_stylesheet_hides_the_top_bar_profile_on_desktop_and_opens_the_side_menu_upwards()
    {
        var desktop = DesktopMedia(Css());

        Assert.Contains("display: none", Rule(desktop, ".mud-appbar .member-profile"));
        Assert.Contains("margin-top: auto", Rule(Css(), ".app-side-footer"));
        Assert.Contains("bottom: 100%", Rule(Css(), ".member-profile-up .member-menu"));
    }

    private sealed class MemoryThemeStore : IThemePreferenceStore
    {
        public Task<ThemeChoice> LoadAsync() => Task.FromResult(ThemeChoice.Device);

        public Task SaveAsync(ThemeChoice choice) => Task.CompletedTask;
    }
}
