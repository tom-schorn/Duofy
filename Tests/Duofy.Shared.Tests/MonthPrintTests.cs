using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;

namespace Duofy.Shared.Tests;

/// <summary>
/// Das Druckbild des Monats (Spec 82, R20, A22). Ob der Browser es wirklich als A4 mit Seitenzahlen setzt, belegt das PDF im Bericht;
/// hier: Inhalt des Druckbilds und die Regeln des Stylesheets.
/// </summary>
public class MonthPrintTests : PageTestContext
{
    public MonthPrintTests()
    {
        Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true));
    }

    private static string Css => File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "duofy.css")).Replace("\r\n", "\n");

    // A22: Kopf, Kennzahlen und je Topf eine Tabelle mit allen Posten, ohne Navigation, Knopf und Sankey.
    [Fact]
    public void A22_the_print_view_has_head_figures_and_a_table_per_pot_with_every_position_but_no_buttons()
    {
        var wage = FakePlanApi.Position("Lohn", 2000m, PotKeys.Income, booked: 2000m, done: true, day: 29);
        var rent = FakePlanApi.Position("Miete", 900m, day: 2);
        var power = FakePlanApi.Position("Strom", 80m, booked: 30m, day: 15);
        var fun = FakePlanApi.Position("Kino", 20m, PotKeys.Wants, day: 20);
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with
        {
            Positions = [wage, rent, power, fun],
            Overview = new PlanOverviewResponse(2000m, 1000m, 1000m,
                [new PlanPotResponse(PotKeys.Needs, 980m, 30m), new PlanPotResponse(PotKeys.Wants, 20m, 0m), new PlanPotResponse(PotKeys.Savings, 0m, 0m)], []),
        };

        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-print']")));
        var print = cut.Find("[data-testid='month-print']");

        Assert.Contains("Oktober 2026", print.QuerySelector("h1")!.TextContent);
        Assert.Contains("Meine Planung", print.QuerySelector(".print-subject")!.TextContent);
        var figures = print.QuerySelector("[data-testid='month-print-metrics']")!.TextContent;
        Assert.Contains("2.000,00 €", figures);
        Assert.Contains("1.000,00 €", figures);
        Assert.Equal(["income", "needs", "wants"], print.QuerySelectorAll("section[data-testid^='month-print-pot-']")
            .Select(s => s.GetAttribute("data-testid")!["month-print-pot-".Length..]));
        var needs = print.QuerySelector("[data-testid='month-print-pot-needs']")!;
        Assert.Equal(2, needs.QuerySelectorAll("tbody tr").Length);
        Assert.Contains("02.10.", needs.TextContent);
        Assert.Contains("teilweise", needs.TextContent);
        Assert.Contains("offen", needs.TextContent);
        Assert.Contains("erledigt", print.QuerySelector("[data-testid='month-print-pot-income']")!.TextContent);
        Assert.Empty(print.QuerySelectorAll("button, nav, a, svg, [role='tab']"));
        Assert.Null(print.Closest("[data-print='hide']"));
    }

    // A22: Beim Drucken steht nur das Druckbild auf der Seite, auf dem Bildschirm ist es unsichtbar; A4 hochkant mit Seitenzahlen.
    [Fact]
    public void A22_the_stylesheet_prints_only_the_print_view_on_a4_with_page_numbers()
    {
        var css = Css;
        Assert.Contains(".month-print { display: none; }", css);
        Assert.Contains("size: A4 portrait;", css);
        Assert.Contains("counter(page)", css);
        var print = css[css.IndexOf("@media print {", StringComparison.Ordinal)..];
        Assert.Contains(".shell-content:has(.month-print) > :not(.month-print) { display: none !important; }", print);
        Assert.Contains(".print-table tr { break-inside: avoid; }", print);
        Assert.Contains(".print-table thead { display: table-header-group; }", print);
        Assert.Contains("[data-print=\"hide\"] { display: none !important; }", print);
    }
}
