using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Layout;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Radzen-Umstellung R6: Die kleinen Bausteine Notice, NativeSelect, GrantReason und NotShared sind in Radzen
/// gebaut.
/// </summary>
public class BuildingBlockTests : PageTestContext
{
    private ActiveMember Member => Services.GetRequiredService<ActiveMember>();

    [Theory]
    [InlineData(NoticeSeverity.Success, "rz-success")]
    [InlineData(NoticeSeverity.Error, "rz-danger")]
    [InlineData(NoticeSeverity.Info, "rz-info")]
    [InlineData(NoticeSeverity.Warning, "rz-warning")]
    public void Notice_shows_its_text_as_a_radzen_alert_of_the_matching_kind(NoticeSeverity severity, string cssClass)
    {
        var cut = Render<Notice>(p => p.Add(n => n.Message, new NoticeMessage("Gespeichert.", severity)));

        var alert = cut.Find(".rz-alert");
        Assert.Contains(cssClass, alert.ClassList);
        Assert.Contains("Gespeichert.", alert.TextContent);
    }

    [Fact]
    public void Notice_without_a_message_shows_nothing()
    {
        var cut = Render<Notice>();

        Assert.Empty(cut.FindAll(".rz-alert"));
        Assert.Equal("", cut.Markup.Trim());
    }

    [Fact]
    public void Notice_has_no_close_button_because_the_message_goes_away_with_the_next_action()
    {
        var cut = Render<Notice>(p => p.Add(n => n.Message, new NoticeMessage("Fehler.", NoticeSeverity.Error)));

        Assert.Empty(cut.FindAll("button"));
    }

    [Fact]
    public void Native_select_stays_a_native_select_with_its_label_attributes_and_options()
    {
        var chosen = "";
        var cut = Render<NativeSelect>(p => p
            .Add(s => s.Label, "Konto")
            .Add(s => s.Value, "b")
            .Add(s => s.ValueChanged, EventCallback.Factory.Create<string>(this, v => chosen = v))
            .AddUnmatched("data-testid", "account-select")
            .AddChildContent("<option value=\"a\">Giro</option><option value=\"b\">Bar</option>"));

        var select = cut.Find("select[data-testid='account-select']");
        Assert.Equal("Konto", cut.Find("label").TextContent.Trim());
        Assert.Equal(select.Id, cut.Find("label").GetAttribute("for"));
        Assert.Equal(2, select.QuerySelectorAll("option").Length);

        select.Change("a");

        Assert.Equal("a", chosen);
    }

    [Fact]
    public void Native_select_takes_its_colours_and_shape_from_radzen_variables()
    {
        var cut = Render<NativeSelect>(p => p.AddChildContent("<option value=\"a\">Giro</option>"));

        Assert.Contains("native-select", cut.Find("select").ClassList);
        var css = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "app.css"));
        var rule = css[css.IndexOf(".native-select {", StringComparison.Ordinal)..];
        rule = rule[..rule.IndexOf('}')];
        Assert.Contains("var(--rz-base-400)", rule);
        Assert.Contains("var(--rz-input-border-radius)", rule);
    }

    [Fact]
    public async Task Grant_reason_names_the_person_and_carries_the_id_the_buttons_point_to()
    {
        await ChooseAnnaAsync();

        var cut = Render<GrantReason>(p => p.Add(r => r.Show, true));

        var reason = cut.Find("[data-testid='grant-reason']");
        Assert.Equal(GrantLock.ReasonId, reason.Id);
        Assert.Equal("Dafür hat dir Anna keine Freigabe gegeben.", reason.TextContent.Trim());
    }

    [Fact]
    public async Task Grant_reason_is_hidden_when_no_button_is_locked_or_nobody_else_is_chosen()
    {
        var own = Render<GrantReason>(p => p.Add(r => r.Show, true));
        Assert.Empty(own.FindAll("[data-testid='grant-reason']"));

        await ChooseAnnaAsync();
        var unlocked = Render<GrantReason>(p => p.Add(r => r.Show, false));
        Assert.Empty(unlocked.FindAll("[data-testid='grant-reason']"));
    }

    [Fact]
    public async Task Not_shared_says_the_area_is_not_shared_with_you()
    {
        await ChooseAnnaAsync();

        var cut = Render<NotShared>();

        Assert.Equal("Diese Person teilt diesen Bereich nicht mit dir.", cut.Find("[data-testid='not-shared']").TextContent.Trim());
    }

    [Fact]
    public async Task Not_shared_says_there_is_no_joint_view_for_the_household()
    {
        await ChooseAnnaAsync();
        Member.ChooseHousehold();

        var cut = Render<NotShared>();

        Assert.Equal("Für den Haushalt gibt es hier keine gemeinsame Sicht.", cut.Find("[data-testid='not-shared']").TextContent.Trim());
    }

    private async Task ChooseAnnaAsync()
    {
        HouseholdApiFake.Household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
        [
            FakeHouseholdApi.Anna(isMe: false) with { GrantsToMe = GrantLevels.None with { Plan = AccessLevelKeys.View } },
            FakeHouseholdApi.Ben(isMe: true),
        ]);
        await Member.LoadAsync();
        Member.Choose(FakeHouseholdApi.AnnaId);
    }
}
