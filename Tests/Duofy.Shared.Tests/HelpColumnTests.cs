using System.Net;
using System.Text.RegularExpressions;
using Bunit;
using Duofy.Shared.Layout;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Die Erklärspalte am PC (Spec 82, R13, A15): je Seite die freigegebenen Abschnitte, im Monat je Reiter. Ob sie bei 1440 Pixeln
/// sichtbar und bei 1024 Pixeln ausgeblendet ist, entscheidet das Stylesheet; das prüft InterfaceRulesTests
/// (A14_A21_phone_has_the_bottom_bar_and_tablet_the_collapsed_side_bar) und die Bilder im Bericht.
/// </summary>
public class HelpColumnTests : PageTestContext
{
    public HelpColumnTests()
    {
        Services.AddSingleton<IAppHost, AppHost>();
        Services.AddSingleton<IThemePreferenceStore>(new MemoryThemeStore());
        Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true));
    }

    private IRenderedComponent<Routes> Open(string path)
    {
        Navigation.NavigateTo(path);
        return Render<Routes>();
    }

    private static string Topic(IRenderedComponent<Routes> cut) =>
        cut.Find("[data-testid='help-column']").GetAttribute("data-topic")!;

    [Theory]
    [InlineData("/", HelpTopics.Start)]
    [InlineData("/plan", HelpTopics.Plans)]
    [InlineData("/plan/2026/10", HelpTopics.Month)]
    [InlineData("/haushaltsplan", HelpTopics.HouseholdPlan)]
    [InlineData("/haushaltsplan/2026/10", HelpTopics.HouseholdPlan)]
    [InlineData("/haushalt", HelpTopics.Household)]
    [InlineData("/haushalt/freigaben", HelpTopics.Grants)]
    [InlineData("/haushalt/richtwerte", HelpTopics.HouseholdPlan)]
    [InlineData("/vertraege", HelpTopics.Commitments)]
    [InlineData("/konten", HelpTopics.Accounts)]
    [InlineData("/sparen", HelpTopics.Savings)]
    [InlineData("/konto", HelpTopics.UserAccount)]
    [InlineData("/admin", HelpTopics.Admin)]
    [InlineData("/mehr", null)]
    [InlineData("/lizenzen", null)]
    [InlineData("/konto/loeschen", null)]
    public void Every_address_has_its_topic(string path, string? topic)
    {
        Assert.Equal(topic, HelpTopics.ForPath(path.TrimStart('/')));
    }

    // A15: Die Spalte steht neben dem Inhalt, nicht in ihm, mit Überschrift und den Abschnitten des Themas.
    [Fact]
    public void A15_the_accounts_page_has_the_help_column_next_to_the_content()
    {
        var cut = Open("/konten");

        cut.WaitForAssertion(() => Assert.Equal(HelpTopics.Accounts, Topic(cut)));
        var column = cut.Find("[data-testid='help-column']");
        Assert.Equal("ASIDE", column.TagName);
        Assert.Null(column.Closest("main"));
        Assert.Equal(HelpTopics.Count[HelpTopics.Accounts], column.QuerySelectorAll(".help-entry").Length);
        Assert.Equal("hide", column.GetAttribute("data-print"));
    }

    [Fact]
    public void Mehr_has_no_help_column()
    {
        var cut = Open("/mehr");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='more-list']")));
        Assert.Empty(cut.FindAll("[data-testid='help-column']"));
    }

    // R13: Im Monat erklärt die Spalte den offenen Reiter.
    [Fact]
    public void The_month_explains_the_open_tab()
    {
        var cut = Open("/plan/2026/10");
        cut.WaitForAssertion(() => Assert.Equal(HelpTopics.Month, Topic(cut)));

        cut.Find("[data-testid='month-tab-bookings']").Click();
        cut.WaitForAssertion(() => Assert.Equal(HelpTopics.Bookings, Topic(cut)));
        Assert.Contains("Ausgaben stehen rot mit Minus", cut.Find("[data-testid='help-column']").TextContent);

        cut.Find("[data-testid='month-tab-review']").Click();
        cut.WaitForAssertion(() => Assert.Equal(HelpTopics.Review, Topic(cut)));
        Assert.Contains("Die Tageskurve", cut.Find("[data-testid='help-column']").TextContent);
    }

    // Ein Reiter des Monats bleibt nicht als Thema der nächsten Seite stehen.
    [Fact]
    public void The_next_page_does_not_keep_the_topic_of_a_month_tab()
    {
        var cut = Open("/plan/2026/10");
        cut.WaitForAssertion(() => Assert.Equal(HelpTopics.Month, Topic(cut)));
        cut.Find("[data-testid='month-tab-review']").Click();
        cut.WaitForAssertion(() => Assert.Equal(HelpTopics.Review, Topic(cut)));

        Navigation.NavigateTo("/konten");

        cut.WaitForAssertion(() => Assert.Equal(HelpTopics.Accounts, Topic(cut)));
    }

    // R13: Jedes Thema hat alle seine Texte; keiner fehlt in den Ressourcen.
    [Fact]
    public void Every_topic_has_a_title_and_a_body_for_each_section()
    {
        var resources = Resources();
        foreach (var (topic, count) in HelpTopics.Count)
        {
            for (var n = 1; n <= count; n++)
            {
                Assert.False(string.IsNullOrWhiteSpace(resources.GetValueOrDefault($"Help_{topic}_{n}_Title")), $"{topic} {n} Titel");
                Assert.False(string.IsNullOrWhiteSpace(resources.GetValueOrDefault($"Help_{topic}_{n}_Body")), $"{topic} {n} Text");
            }
            Assert.False(resources.ContainsKey($"Help_{topic}_{count + 1}_Title"), $"{topic}: mehr Texte als gezählt");
        }
    }

    // R13 (Tom, 03.11.2026: "Ja, so einbauen"): Die Texte sind wörtlich die freigegebenen, ohne die Herkunft in Klammern.
    [Fact]
    public void The_texts_are_the_approved_ones()
    {
        var approved = File.ReadAllText(RepositoryFiles.Path("docs", "specs", "82-blueprint-foundation", "erklaerspalte-texte.md")).Replace("\r\n", "\n");
        var expected = Regex.Matches(approved, @"\*\*(.+?)\*\*\n(.+)")
            .Select(m => (m.Groups[1].Value.Trim(), Regex.Replace(m.Groups[2].Value, @"\s*\((?:[^()]|\([^()]*\))*\)\s*$", "").Trim()))
            .ToList();
        var resources = Resources();
        var built = HelpTopics.Count
            .SelectMany(t => Enumerable.Range(1, t.Value).Select(n => (resources[$"Help_{t.Key}_{n}_Title"], resources[$"Help_{t.Key}_{n}_Body"])))
            .ToList();

        Assert.Equal(expected.Count, built.Count);
        Assert.Equal(expected.OrderBy(e => e.Item1 + e.Item2, StringComparer.Ordinal), built.OrderBy(e => e.Item1 + e.Item2, StringComparer.Ordinal));
    }

    // Spec 93, A6: Der Text zu Verplanbar nennt die Regel, dass Ausgaben ohne Posten abgezogen werden.
    [Fact]
    public void The_free_amount_text_says_that_expenses_without_a_position_are_subtracted()
    {
        var body = Resources()["Help_month_2_Body"];

        Assert.Contains("Ausgaben ohne Posten werden abgezogen", body);
        Assert.Contains("Einnahmen ohne Posten kommen dazu", body);
    }

    // Spec 93, A8 (R7): Der Text zu Verplanbar sagt in einem Satz, dass jeder Posten mit dem Geplanten oder dem höheren
    // Gebuchten zählt, auch abgehakt.
    [Fact]
    public void The_free_amount_text_says_that_a_position_counts_with_the_planned_or_the_higher_booked_amount()
    {
        var body = Resources()["Help_month_2_Body"];

        Assert.Contains("Jeder Posten zählt mit dem geplanten Betrag oder, wenn mehr gebucht ist, mit dem gebuchten, auch abgehakt.", body);
    }

    private static Dictionary<string, string> Resources()
    {
        var resx = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "Resources", "SharedResources.resx"));
        return Regex.Matches(resx, "<data name=\"(Help_[^\"]+)\"[^>]*>\\s*<value>(.*?)</value>", RegexOptions.Singleline)
            .ToDictionary(m => m.Groups[1].Value, m => WebUtility.HtmlDecode(m.Groups[2].Value));
    }

    private sealed class MemoryThemeStore : IThemePreferenceStore
    {
        public Task<ThemeChoice> LoadAsync() => Task.FromResult(ThemeChoice.Device);

        public Task SaveAsync(ThemeChoice choice) => Task.CompletedTask;
    }
}
