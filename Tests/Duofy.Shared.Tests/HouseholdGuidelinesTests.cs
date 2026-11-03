using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Issue #28 (Richtwerte je Person im Haushaltsplan): Die Übersicht des Haushaltsmonats zeigt je Mitglied Anteil, Richtwert
/// und Abweichung als Text, erklärt ruhig und macht sichtbar, wenn der gemeinsame Plan aufgeht und eine Person eine Lücke hat.
/// Die Daten kommen aus HouseholdPlanResponse.Members (Spec 27); sichtbar ist es für Mitglieder wie der übrige Haushaltsplan.
/// "Heute" ist der 21. Oktober 2026. Der echte Browser ist damit nicht gesehen.
/// </summary>
public class HouseholdGuidelinesTests : PageTestContext
{
    public HouseholdGuidelinesTests()
    {
    }

    private ActiveMember Member => Services.GetRequiredService<ActiveMember>();

    private static HouseholdMemberQuotaResponse Quota(string pot, int target, decimal actual) =>
        new(pot, target, actual, Math.Round(actual - target, 1));

    private static HouseholdMemberResponse Person(Guid id, string name, decimal income, decimal needs, decimal wants, decimal savings, bool hasIncome = true) =>
        new(id, name, income, hasIncome,
            [Quota(PotKeys.Needs, 50, needs), Quota(PotKeys.Wants, 30, wants), Quota(PotKeys.Savings, 20, savings)]);

    private async Task<IRenderedComponent<Plan>> OpenAsync(
        IReadOnlyList<HouseholdMemberResponse> members, decimal needs, decimal wants, decimal savings)
    {
        var household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
            [FakeHouseholdApi.Anna(isMe: true), FakeHouseholdApi.Ben()]);
        HouseholdApiFake.Household = household;
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10) with
        {
            Quotas = [new PlanQuotaResponse(PotKeys.Needs, 50, needs), new PlanQuotaResponse(PotKeys.Wants, 30, wants), new PlanQuotaResponse(PotKeys.Savings, 20, savings)],
            Members = members,
        };
        await Member.LoadAsync();
        Member.ChooseHousehold();
        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        OpenReviewTab(cut);
        return cut;
    }

    private static string Text(IRenderedComponent<Plan> cut, string testId) => cut.Find($"[data-testid='{testId}']").TextContent;

    // Die Beispielzahlen aus Spec 27: Haushalt 50,0, Anna 60,0, Ben 35,0 bei Grundbedarf (Ziel 50).
    private Task<IRenderedComponent<Plan>> OpenSpecExampleAsync() =>
        OpenAsync(
            [
                Person(FakeHouseholdApi.AnnaId, "Anna", 3000m, 60.0m, 25.0m, 15.0m),
                Person(FakeHouseholdApi.BenId, "Ben", 2000m, 35.0m, 35.0m, 30.0m),
            ],
            needs: 50.0m, wants: 30.0m, savings: 20.0m);

    // Je Mitglied ein Block mit Namen, Einnahmen und je Topf Anteil, Richtwert und Abweichung als Text.
    [Fact]
    public async Task Each_member_has_a_block_with_share_guideline_and_deviation_as_text()
    {
        var cut = await OpenSpecExampleAsync();

        var anna = Text(cut, $"guideline-person-{FakeHouseholdApi.AnnaId}");
        Assert.Contains("Anna", anna);
        var needs = Text(cut, $"guideline-row-{FakeHouseholdApi.AnnaId}-needs");
        Assert.Contains("60,0 %", needs);
        Assert.Contains("50 %", needs);
        Assert.Contains("10,0 Punkte darüber", needs);

        var bensNeeds = Text(cut, $"guideline-row-{FakeHouseholdApi.BenId}-needs");
        Assert.Contains("35,0 %", bensNeeds);
        Assert.Contains("15,0 Punkte darunter", bensNeeds);
    }

    // Ein Topf auf dem Richtwert sagt es in Worten.
    [Fact]
    public async Task A_share_on_the_guideline_says_so_in_words()
    {
        var cut = await OpenAsync([Person(FakeHouseholdApi.AnnaId, "Anna", 3000m, 50.0m, 30.0m, 20.0m)], 50.0m, 30.0m, 20.0m);

        Assert.Contains("wie der Richtwert", Text(cut, $"guideline-row-{FakeHouseholdApi.AnnaId}-needs"));
    }

    // Der gemeinsame Plan geht auf, und eine Person hat eine Lücke: beides steht in einem ruhigen Satz.
    [Fact]
    public async Task It_says_when_the_joint_plan_fits_and_one_person_is_off_the_guideline()
    {
        var cut = await OpenSpecExampleAsync();

        Assert.Contains("Der gemeinsame Plan geht auf", Text(cut, "guideline-summary"));
        var anna = Text(cut, $"guideline-note-{FakeHouseholdApi.AnnaId}");
        Assert.Contains("Anna liegt bei Grundbedarf 10,0 Punkte über dem Richtwert", anna);
        Assert.Contains("gemeinsam geht der Plan trotzdem auf", anna);
        var ben = Text(cut, $"guideline-note-{FakeHouseholdApi.BenId}");
        Assert.Contains("Ben liegt bei Grundbedarf 15,0 Punkte unter dem Richtwert", ben);
    }

    // Eine Person innerhalb der Toleranz bekommt keinen Hinweis.
    [Fact]
    public async Task A_person_close_to_the_guideline_gets_no_note()
    {
        var cut = await OpenAsync(
            [Person(FakeHouseholdApi.AnnaId, "Anna", 3000m, 52.0m, 29.0m, 19.0m)], 52.0m, 29.0m, 19.0m);

        Assert.Empty(cut.FindAll($"[data-testid='guideline-note-{FakeHouseholdApi.AnnaId}']"));
        Assert.Contains("Der gemeinsame Plan geht auf", Text(cut, "guideline-summary"));
    }

    // Weicht der Haushalt selbst ab, steht es ohne "geht auf" und nennt den Topf.
    [Fact]
    public async Task When_the_household_itself_is_off_the_summary_names_the_pot()
    {
        var cut = await OpenAsync(
            [Person(FakeHouseholdApi.AnnaId, "Anna", 3000m, 70.0m, 20.0m, 10.0m)], 70.0m, 20.0m, 10.0m);

        var summary = Text(cut, "guideline-summary");
        Assert.DoesNotContain("geht auf", summary);
        Assert.Contains("Grundbedarf", summary);
        Assert.Contains("vom Richtwert", summary);
    }

    // Ohne Einnahmen gibt es keine Anteile; es steht ein Satz statt Nullen.
    [Fact]
    public async Task A_member_without_income_gets_a_sentence_instead_of_shares()
    {
        var cut = await OpenAsync(
            [
                Person(FakeHouseholdApi.AnnaId, "Anna", 3000m, 50.0m, 30.0m, 20.0m),
                Person(FakeHouseholdApi.BenId, "Ben", 0m, 0m, 0m, 0m, hasIncome: false),
            ],
            50.0m, 30.0m, 20.0m);

        var ben = Text(cut, $"guideline-person-{FakeHouseholdApi.BenId}");
        Assert.Contains("keine Einnahmen", ben);
        Assert.Empty(cut.FindAll($"[data-testid^='guideline-row-{FakeHouseholdApi.BenId}']"));
        Assert.Empty(cut.FindAll($"[data-testid='guideline-note-{FakeHouseholdApi.BenId}']"));
    }

    // Wo niemand Einnahmen hat, gibt es nichts zu erklären.
    [Fact]
    public async Task Nobody_with_income_shows_one_calm_sentence()
    {
        var cut = await OpenAsync([Person(FakeHouseholdApi.AnnaId, "Anna", 0m, 0m, 0m, 0m, hasIncome: false)], 0m, 0m, 0m);

        Assert.Contains("keine Einnahmen", Text(cut, "guideline-summary"));
        Assert.DoesNotContain("geht auf", Text(cut, "guideline-summary"));
    }

    // Keine Rechtsaussage, kein Alarm: ein Hinweis nennt den Richtwert einen Anhaltspunkt, die Abweichung trägt keine Warnfarbe.
    [Fact]
    public async Task The_guideline_is_a_pointer_and_a_deviation_has_no_alarm()
    {
        var cut = await OpenSpecExampleAsync();

        Assert.Contains("Anhaltspunkt", Text(cut, "guideline-intro"));
        Assert.Empty(cut.FindAll("[data-testid='household-guidelines'] [role='alert']"));
        Assert.Empty(cut.FindAll("[data-testid='household-guidelines'] .over"));
    }

    // Jede Tabelle hat eine Beschriftung und Kopfzellen für Vorlese-Programme.
    [Fact]
    public async Task Each_person_table_is_labelled_for_screen_readers()
    {
        var cut = await OpenSpecExampleAsync();

        var table = cut.Find($"[data-testid='guideline-person-{FakeHouseholdApi.AnnaId}'] table");
        Assert.Contains("Anna", table.QuerySelector("caption")!.TextContent);
        Assert.Equal(["Topf", "Anteil", "Richtwert", "Abweichung"], table.QuerySelectorAll("thead th").Select(h => h.TextContent.Trim()));
    }

    // Die Sicht ist nur lesend und gehört dem Haushaltsplan: eine einzelne Person sieht sie nicht.
    [Fact]
    public async Task A_personal_month_has_no_household_guidelines()
    {
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10);
        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        OpenReviewTab(cut);

        Assert.Empty(cut.FindAll("[data-testid='household-guidelines']"));
    }

    // Die Rechenregel allein: Toleranz, Seite der Abweichung, Lücke.
    [Theory]
    [InlineData(5.0, GuidelineSide.Over, false)]
    [InlineData(5.1, GuidelineSide.Over, true)]
    [InlineData(-5.0, GuidelineSide.Under, false)]
    [InlineData(-5.1, GuidelineSide.Under, true)]
    [InlineData(0.0, GuidelineSide.Equal, false)]
    [InlineData(0.04, GuidelineSide.Equal, false)]
    public void The_rule_calls_a_deviation_over_under_or_equal_and_outside_only_beyond_five_points(double deviation, GuidelineSide side, bool outside)
    {
        var row = GuidelineView.Row(new HouseholdMemberQuotaResponse(PotKeys.Needs, 50, 50m + (decimal)deviation, (decimal)deviation));

        Assert.Equal((side, outside), (row.Side, row.Outside));
    }
}
