using System.Reflection;
using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Layout;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>Die Seite /einladung/{token} (Spec 67, R5, R6): Vorschau, Anmelden oder Registrieren, Beitreten.</summary>
public class InvitationPageTests : PageTestContext
{
    private IRenderedComponent<Invitation> Open(string token = "abc_DEF-1")
    {
        Navigation.NavigateTo($"/einladung/{token}");
        var cut = Render<Invitation>(p => p.Add(x => x.Token, token));
        cut.WaitForAssertion(() => Assert.Contains(token, HouseholdApiFake.Previewed));
        return cut;
    }

    [Fact]
    public void The_page_is_reachable_without_sign_in()
    {
        Assert.Equal("/einladung/{Token}", typeof(Invitation).GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.Null(typeof(Invitation).GetCustomAttribute<AuthorizeAttribute>());
    }

    // A18
    [Fact]
    public void A_signed_in_person_sees_the_preview_and_joins()
    {
        // Ben ist in keinem Haushalt.
        HouseholdApiFake.Household = null;
        HouseholdApiFake.Joined = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
            [FakeHouseholdApi.Anna(isMe: false), FakeHouseholdApi.Ben(isMe: true)]);
        var cut = Open();

        cut.WaitForAssertion(() => Assert.Contains("Anna Muster lädt dich in den Haushalt „Haushalt von Anna“ ein.", cut.Markup));
        Assert.Contains("Die Einladung gilt bis 04.11.2026.", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid='invitation-sign-in']"));

        cut.Find("[data-testid='invitation-join']").Click();

        cut.WaitForAssertion(() => Assert.Equal("/haushalt", CurrentPath));
        Assert.Equal(["abc_DEF-1"], HouseholdApiFake.Accepted);

        // Die Seite Haushalt zeigt danach Annas Haushalt mit Anna als Gründerin.
        var household = Render<Household>();
        household.WaitForAssertion(() => Assert.Contains("Gründer", household.Find($"[data-testid='household-member-{FakeHouseholdApi.AnnaId}']").TextContent));
        Assert.Contains("Haushalt von Anna", household.Find("[data-testid='household-name']").TextContent);
    }

    // A6, A18: Ohne Anmeldung gibt es die Vorschau und den Weg zum Anmelden, der hierher zurückführt.
    [Fact]
    public void Without_sign_in_the_page_offers_sign_in_and_registration_that_come_back()
    {
        AddAuthorization().SetNotAuthorized();
        var cut = Open();

        cut.WaitForAssertion(() => Assert.Contains("Anna Muster lädt dich in den Haushalt „Haushalt von Anna“ ein.", cut.Markup));
        Assert.Empty(cut.FindAll("[data-testid='invitation-join']"));
        // Ohne Anmeldung gibt es keinen eigenen Haushalt zu fragen.
        Assert.Equal(0, HouseholdApiFake.MineCalls);
        Assert.Equal("/anmelden?returnUrl=%2Feinladung%2Fabc_DEF-1", cut.Find("[data-testid='invitation-sign-in']").GetAttribute("href"));
        Assert.Equal("/registrieren?returnUrl=%2Feinladung%2Fabc_DEF-1", cut.Find("[data-testid='invitation-register']").GetAttribute("href"));
    }

    // A9 (Seite)
    [Fact]
    public void An_expired_invitation_says_so_and_offers_no_join()
    {
        HouseholdApiFake.PreviewError = ErrorCodes.InvitationExpired;

        var cut = Open();

        cut.WaitForAssertion(() => Assert.Contains("Diese Einladung ist abgelaufen.", cut.Markup));
        Assert.Empty(cut.FindAll("[data-testid='invitation-join']"));
    }

    // A21: Wer schon in einem Haushalt ist, sieht Beitreten ausgegraut mit dem Grund, statt angeboten und dann abgelehnt zu werden.
    [Fact]
    public void Someone_in_a_household_sees_beitreten_greyed_out_with_the_reason()
    {
        var cut = Open();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='invitation-join-reason']")));

        var join = cut.Find("[data-testid='invitation-join']");

        Assert.Contains("Beitreten", join.TextContent);
        Assert.Equal("true", join.GetAttribute("aria-disabled"));
        // Nicht disabled: So bleibt der Knopf im Fokus, und ein Screenreader liest den Grund vor.
        Assert.False(join.HasAttribute("disabled"));
        var reason = cut.Find("[data-testid='invitation-join-reason']");
        Assert.Equal("Du bist schon in einem Haushalt. Tritt zuerst aus, dann kannst du gründen oder beitreten.", reason.TextContent.Trim());
        Assert.Equal(reason.Id, join.GetAttribute("aria-describedby"));

        join.Click();

        Assert.Empty(HouseholdApiFake.Accepted);
        Assert.Equal("/einladung/abc_DEF-1", CurrentPath);
    }

    // Spec 69, A12: Nach dem Beitreten bietet der Umschalter den Haushalt an, ohne Neustart.
    [Fact]
    public void Joining_refreshes_the_switch()
    {
        HouseholdApiFake.Household = null;
        HouseholdApiFake.Joined = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
            [FakeHouseholdApi.Anna(isMe: false), FakeHouseholdApi.Ben(isMe: true)]);
        var member = Services.GetRequiredService<ActiveMember>();
        var switcher = Render<MemberSwitch>();
        switcher.WaitForAssertion(() => Assert.Equal(1, HouseholdApiFake.MineCalls));
        switcher.Find("[data-testid='member-profile']").Click();
        Assert.Equal("true", switcher.Find("[data-testid='member-choice-household']").GetAttribute("aria-disabled"));
        var cut = Open();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='invitation-join']")));

        cut.Find("[data-testid='invitation-join']").Click();

        cut.WaitForAssertion(() => Assert.Equal("/haushalt", CurrentPath));
        Assert.True(member.CanShowHousehold);
        switcher.WaitForAssertion(() => Assert.Equal("button", switcher.Find("[data-testid='member-choice-household']").TagName.ToLowerInvariant()));
    }

    // A8 (Seite): Trat Ben zur selben Zeit anderswo bei, lehnt die API trotzdem ab; die Seite sagt, warum.
    [Fact]
    public void A_refused_join_shows_why_and_stays()
    {
        HouseholdApiFake.Household = null;
        HouseholdApiFake.Error = ErrorCodes.AlreadyInHousehold;
        var cut = Open();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='invitation-join']")));

        cut.Find("[data-testid='invitation-join']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Du bist schon in einem Haushalt.", cut.Markup));
        Assert.Equal("/einladung/abc_DEF-1", CurrentPath);
    }
}
