using System.Text.RegularExpressions;
using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Layout;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components;

namespace Duofy.Shared.Tests;

/// <summary>
/// Ab 1280 px nutzen die Hauptseiten die Breite (Tom, 01.11.2026): Blöcke in Spalten, Listen als Tabellen mit Spalten.
/// Am Handy und Tablet ändert sich nichts: Die Seiten behalten ihre Bausteine, alles Neue steht im Desktop-Bereich des
/// Stylesheets, und die Kopfzeilen der Tabellen sind darunter ausgeblendet. Ein echter Browser fehlt hier; die
/// Sichtprüfung steht im Bericht.
/// </summary>
public class DesktopWidthTests : PageTestContext
{
    private static string Css() => File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "app.css"));

    /// <summary>Teilt das Stylesheet in den Desktop-Bereich und den ganzen Rest.</summary>
    private static (string Desktop, string Other) Split(string css)
    {
        var desktop = new System.Text.StringBuilder();
        var rest = new System.Text.StringBuilder();
        var position = 0;
        var query = $"@media (min-width: {WidthRanges.DesktopMin}px)";
        int from;
        while ((from = css.IndexOf(query, position, StringComparison.Ordinal)) >= 0)
        {
            rest.Append(css, position, from - position);
            var open = css.IndexOf('{', from);
            var depth = 0;
            var i = open;
            for (; i < css.Length; i++)
            {
                if (css[i] == '{') depth++;
                else if (css[i] == '}' && --depth == 0) break;
            }
            desktop.Append(css, open + 1, i - open - 1).AppendLine();
            position = i + 1;
        }
        rest.Append(css, position, css.Length - position);
        return (desktop.ToString(), rest.ToString());
    }

    // Ein Merge kann eine schließende Klammer verschlucken; dann gelten alle Regeln danach nicht mehr.
    [Fact]
    public void The_stylesheet_has_balanced_braces()
    {
        var css = Regex.Replace(Css(), @"/\*.*?\*/", "", RegexOptions.Singleline);

        Assert.Equal(css.Count(c => c == '{'), css.Count(c => c == '}'));
        Assert.DoesNotContain("<<<<<<<", css);
        Assert.DoesNotContain(">>>>>>>", css);
    }

    // Die Startseite nutzt die Breite wie die anderen Hauptseiten und heißt im Tab wie die Marke.
    [Fact]
    public void The_start_page_is_wide_and_named_after_the_brand()
    {
        var cut = Render<Start>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".page-wide .start-greeting")));
        var markup = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "Pages", "Start.razor"));
        Assert.Contains("<PageTitle>@Brand.Name</PageTitle>", markup);
    }

    // Am Handy dürfen die Kästen der Startseite nicht breiter als die Spalte werden, und vier Tabs müssen passen.
    [Fact]
    public void Start_boxes_and_month_tabs_stay_inside_a_narrow_screen()
    {
        var (_, other) = Split(Css());

        Assert.Contains("grid-template-columns: minmax(0, 1fr)", Regex.Match(other, @"\.start-grid\s*\{([^}]*)\}").Groups[1].Value);
        Assert.Contains("flex-wrap: wrap", Regex.Match(other, @"\.month-tabs\s*\{([^}]*)\}").Groups[1].Value);
    }

    [Fact]
    public void The_month_review_puts_its_two_charts_in_columns()
    {
        var markup = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "Pages", "Month", "MonthReview.razor"));

        Assert.Contains("class=\"month-sections\"", markup);
    }

    // Seiten mit Spalten sagen das mit "page-wide"; alle anderen, zum Beispiel Formulare, bleiben schmal.
    [Fact]
    public void Only_pages_marked_as_wide_use_the_larger_content_width()
    {
        var (desktop, _) = Split(Css());

        Assert.Contains($"max-width: {WidthRanges.ContentMaxWidth}px", Regex.Match(desktop, @"\.app-page\s*\{([^}]*)\}").Groups[1].Value);
        Assert.Contains($"max-width: {WidthRanges.WideContentMaxWidth}px", Regex.Match(desktop, @"\.app-page:has\(\.page-wide\)\s*\{([^}]*)\}").Groups[1].Value);
    }

    // Am Handy und Tablet ändert sich nichts: Die Spalten gibt es nur im Desktop-Bereich.
    [Theory]
    [InlineData(".data-table")]
    [InlineData(".data-row")]
    [InlineData(".data-cells")]
    [InlineData(".plan-layout")]
    [InlineData(".plan-groups")]
    [InlineData(".month-sections")]
    [InlineData(".bookings-top")]
    [InlineData(".household-columns")]
    [InlineData(".line-table-text")]
    [InlineData(".month-nav")]
    public void The_columns_exist_only_from_1280px(string selector)
    {
        var (desktop, rest) = Split(Css());

        Assert.Contains(selector, desktop);
        Assert.DoesNotMatch(Regex.Escape(selector) + @"[\s,{:.>]", rest);
    }

    [Fact]
    public void The_table_heads_are_hidden_below_1280px_and_hidden_from_screen_readers()
    {
        var (desktop, rest) = Split(Css());

        Assert.Contains("display: none", Regex.Match(rest, @"\.data-table-head\s*\{([^}]*)\}").Groups[1].Value);
        Assert.Matches(@"\.data-table-head[^{]*\{[^}]*display:\s*grid", desktop);
    }

    private static void AssertHead(IRenderedComponent<IComponent> cut, params string[] labels)
    {
        var head = cut.Find(".data-table-head");
        Assert.Equal("true", head.GetAttribute("aria-hidden"));
        Assert.Equal(labels, head.Children.Select(c => c.TextContent.Trim()).Where(t => t.Length > 0));
        Assert.NotEmpty(cut.FindAll(".page-wide"));
    }

    [Fact]
    public void Accounts_are_a_table_with_columns()
    {
        AccountApiFake.Accounts.Add(FakeAccountApi.Account("Giro", balance: 12m, isDefault: true));

        var cut = Render<Accounts>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".data-row")));
        AssertHead(cut, "Name", "Art", "Stand");
        Assert.NotEmpty(cut.FindAll(".data-row .data-column-4"));
    }

    [Fact]
    public void Contracts_are_a_table_and_their_line_reads_as_before()
    {
        CommitmentApiFake.Commitments.Add(FakeCommitmentApi.Commitment("Miete", 890m, nextDue: new DateOnly(2026, 11, 1)));

        var cut = Render<Commitments>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".data-row")));
        AssertHead(cut, "Name", "Art", "Abstand", "Nächste Zahlung", "Betrag");
        // Die Zeile unter dem Namen liest sich auf dem Handy wie bisher; am PC sind ihre Teile Spalten.
        Assert.Matches(@"^.+ · .+ · nächste Zahlung 01\.11\.2026$", cut.Find(".data-row .data-cells .data-cells").TextContent.Trim());
    }

    [Fact]
    public void Bookings_are_a_table_with_their_controls_in_one_row()
    {
        var giro = FakeAccountApi.Account("Giro");
        Accounts.Accounts.Add(new AccountChoice(giro.Id, "Giro", IsDefault: true, Active: true));
        BookingApiFake.Bookings.Add(FakeBookingApi.Expense(giro.Id, new DateOnly(2026, 10, 5), 11m, FakeCategoryApi.Groceries, "Einkauf"));

        var cut = Render<Bookings>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".data-row")));
        AssertHead(cut, "Datum", "Bezeichnung", "Details", "Betrag");
        Assert.NotEmpty(cut.FindAll(".bookings-top [data-testid='bookings-month']"));
        Assert.NotEmpty(cut.FindAll(".bookings-top [data-testid='bookings-sum-income']"));
    }

    [Fact]
    public void Household_shows_card_and_members_in_columns()
    {
        var cut = Render<Household>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".data-row")));
        AssertHead(cut, "Name", "E-Mail", "Rolle");
        Assert.NotEmpty(cut.FindAll(".household-columns [data-testid='household-card']"));
    }

    [Fact]
    public void The_list_of_months_is_marked_as_wide()
    {
        var cut = Render<PlanOverview>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".plan-months.page-wide")));
    }

    private IRenderedComponent<Plan> OpenMonth()
    {
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10);
        var cut = Render<Plan>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-overview']")));
        return cut;
    }

    [Fact]
    public void The_month_overview_puts_its_sections_in_columns()
    {
        var cut = OpenMonth();

        // Die Breite hängt am Bereich der Tabs, damit sie beim Wechsel des Tabs nicht springt.
        Assert.NotEmpty(cut.FindAll("[role='tabpanel'].page-wide"));

        Assert.NotEmpty(cut.FindAll(".month-sections.page-wide [data-testid='month-pots']"));
        Assert.NotEmpty(cut.FindAll(".month-sections [data-testid='month-due']"));
    }

    [Fact]
    public void The_plan_tab_has_positions_beside_sums_and_carry_over()
    {
        var cut = OpenMonth();

        cut.Find("[data-testid='month-tab-plan']").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='plan-sums']")));

        Assert.NotEmpty(cut.FindAll(".plan-layout.page-wide > .plan-layout-side[data-testid='plan-sums']"));
        Assert.NotEmpty(cut.FindAll(".plan-layout > .plan-layout-side[data-testid='plan-carry-over']"));
        Assert.NotEmpty(cut.FindAll(".plan-layout > .plan-groups"));
    }

    // Bezeichnung und Nebentext stehen am PC nebeneinander; der gelesene Text der Zeile bleibt gleich.
    [Fact]
    public void A_line_table_row_keeps_its_text_and_has_a_wrapper_for_the_columns()
    {
        var cut = Render<Duofy.Shared.Blocks.LineTable>(p => p
            .Add(c => c.Caption, "Liste")
            .Add(c => c.Rows, [new Duofy.Shared.Blocks.LineTableRow("Miete", 850m, Detail: "Giro")]));

        var text = cut.Find("th .line-table-text");
        Assert.Contains("Miete", text.TextContent);
        Assert.Equal("Giro", text.QuerySelector(".line-table-detail")!.TextContent);
    }
}
