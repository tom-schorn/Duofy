using System.Globalization;
using System.Text.RegularExpressions;
using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Duofy.Shared.Pages.Month;

namespace Duofy.Shared.Tests;

/// <summary>
/// Issue #94, R3 (A3): Jeder Topf hat auf jeder Seite genau eine Farbe, die aus den Variablen --pot-needs, --pot-wants und
/// --pot-savings kommt, hell und dunkel mit Kontrast 3 : 1 für Balken. Der Name steht immer daneben; überzogen zeigt das Wort.
/// </summary>
public partial class PotColourTests : PageTestContext
{
    private static readonly string[] Pots = [PotKeys.Needs, PotKeys.Wants, PotKeys.Savings];

    private static string Css() => File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "duofy.css"));

    [Fact]
    public void A3_each_pot_uses_one_colour_on_every_page()
    {
        // Startseite, Plan-Übersicht und Monat legen ihre Farben über dieselben Klassen fest, und die kennen nur die Topf-Variablen.
        var css = Css();
        foreach (var pot in Pots)
        {
            foreach (var selector in new[] { $".bar-fill--{pot}", $".dot--{pot}", $".donut-tone--{pot}" })
            {
                var rule = Regex.Match(css, Regex.Escape(selector) + @"\s*\{([^}]*)\}").Groups[1].Value;
                Assert.Contains($"var(--pot-{pot})", rule);
                Assert.DoesNotContain("--chart-", rule);
            }
        }

        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with
        {
            Overview = new PlanOverviewResponse(3000m, 0m, 100m, Pots.Select(p => new PlanPotResponse(p, 100m, 50m)).ToList(), []),
        };
        var start = Render<Start>();
        start.WaitForAssertion(() => Assert.NotEmpty(start.FindAll("[data-testid='start-pots']")));
        var overview = Render<PlanOverview>();
        overview.WaitForAssertion(() => Assert.NotEmpty(overview.FindAll("[data-testid='plan-month-pot-2026-10-needs']")));
        var month = Render<PotSection>(p => p.Add(x => x.Pot, PotKeys.Needs).Add(x => x.Sum, 50m).Add(x => x.Limit, 100m));

        foreach (var pot in Pots.Where(p => p != PotKeys.Needs))
        {
            Assert.Contains($"dot--{pot}", start.Markup);
            Assert.Contains($"bar-fill--{pot}", start.Markup);
            Assert.Contains($"dot--{pot}", overview.Markup);
            Assert.Contains($"bar-fill--{pot}", overview.Markup);
        }
        foreach (var page in new[] { start.Markup, overview.Markup, month.Markup })
        {
            Assert.Contains("dot--needs", page);
            Assert.Contains("bar-fill--needs", page);
            Assert.Contains("Grundbedarf", page);
        }
    }

    [Fact]
    public void An_overspent_pot_shows_a_red_end_and_the_word_on_every_page()
    {
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with
        {
            Overview = new PlanOverviewResponse(3000m, 0m, 100m, Pots.Select(p => new PlanPotResponse(p, 100m, 130m)).ToList(), []),
        };

        var start = Render<Start>();
        start.WaitForAssertion(() => Assert.NotEmpty(start.FindAll("[data-testid='start-pots']")));
        var overview = Render<PlanOverview>();
        overview.WaitForAssertion(() => Assert.NotEmpty(overview.FindAll("[data-testid='plan-month-pot-2026-10-needs']")));
        var month = Render<PotSection>(p => p.Add(x => x.Pot, PotKeys.Needs).Add(x => x.Sum, 130m).Add(x => x.Limit, 100m));

        foreach (var page in new[] { start.Markup, overview.Markup, month.Markup })
        {
            Assert.Contains("bar-fill--over-end", page);
            Assert.Contains("überzogen", page);
        }
        Assert.Matches(@"\.bar-fill--over-end\s*\{[^}]*var\(--destructive\)", Css());
    }

    [Fact]
    public void The_month_marks_a_pot_overspent_by_booked_against_planned_not_planned_against_the_quota()
    {
        // Review M1: geplant 400 liegt unter der Quote 500, gebucht 450 liegt über dem Geplanten: der Balken endet rot.
        var position = FakePlanApi.Position("Miete", 400m, PotKeys.Needs, booked: 450m);
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with
        {
            Positions = [position],
            Overview = new PlanOverviewResponse(1000m, 400m, 600m, [new PlanPotResponse(PotKeys.Needs, 400m, 450m)], []),
        };

        var month = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        OpenPlanTab(month);
        month.WaitForAssertion(() => Assert.NotEmpty(month.FindAll("[data-testid='pot-section-needs']")));

        var section = month.Find("[data-testid='pot-section-needs']").InnerHtml;
        Assert.Contains("bar-fill--over-end", section);
        Assert.Contains("überzogen", section);
    }

    [Fact]
    public void The_month_shows_no_red_end_when_booked_stays_within_planned_even_above_the_quota()
    {
        var position = FakePlanApi.Position("Miete", 600m, PotKeys.Needs, booked: 550m);
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with
        {
            Positions = [position],
            Overview = new PlanOverviewResponse(1000m, 600m, 400m, [new PlanPotResponse(PotKeys.Needs, 600m, 550m)], []),
        };

        var month = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        OpenPlanTab(month);
        month.WaitForAssertion(() => Assert.NotEmpty(month.FindAll("[data-testid='pot-section-needs']")));

        var section = month.Find("[data-testid='pot-section-needs']").InnerHtml;
        Assert.DoesNotContain("bar-fill--over-end", section);
        Assert.DoesNotContain("überzogen", section);
    }

    // Constraint: 3 : 1 für Balken gegen Karte und Hintergrund, hell und dunkel.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_pot_colours_reach_three_to_one_against_card_and_background(bool dark)
    {
        var theme = dark ? ThemeValues.Dark : ThemeValues.Light;

        foreach (var pot in Pots)
        {
            var value = theme[$"--pot-{pot}"];
            var colour = value.StartsWith("var(", StringComparison.Ordinal) ? theme[value[4..^1]] : value;
            foreach (var surface in new[] { theme["--card"], theme["--background"] })
            {
                Assert.True(Contrast(colour, surface) >= 3.0, $"{pot} {colour} auf {surface} in {(dark ? "dunkel" : "hell")}: {Contrast(colour, surface):0.00}");
            }
        }
    }

    private static double Contrast(string a, string b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(string hex)
    {
        double Channel(int at)
        {
            var c = int.Parse(hex.AsSpan(1 + at * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(0) + 0.7152 * Channel(1) + 0.0722 * Channel(2);
    }
}
