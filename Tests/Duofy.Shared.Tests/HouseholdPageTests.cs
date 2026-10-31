using System.Reflection;
using AngleSharp.Dom;
using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Layout;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Duofy.Shared.Tests;

/// <summary>Die Seite /haushalt (Spec 67): Gründen, Mitglieder, Knöpfe des Gründers, Einladen, Entfernen, Austreten.</summary>
public class HouseholdPageTests : PageTestContext
{
    private readonly IRenderedComponent<MudDialogProvider> dialogs;

    public HouseholdPageTests()
    {
        Render<MudPopoverProvider>();
        dialogs = Render<MudDialogProvider>();
    }

    private IRenderedComponent<Household> OpenPage()
    {
        var cut = Render<Household>();
        cut.WaitForAssertion(() => Assert.True(HouseholdApiFake.MineCalls > 0));
        cut.WaitForAssertion(() => Assert.Contains(HouseholdApiFake.Household!.Name, cut.Markup));
        return cut;
    }

    private IRenderedComponent<Household> OpenWithoutHousehold()
    {
        HouseholdApiFake.Household = null;
        var cut = Render<Household>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='household-found']")));
        return cut;
    }

    /// <summary>Der Zustand des Umschalters "wessen Zahlen" (Spec 68, R8; Spec 69, R9), den die Seite nach Änderungen auffrischt.</summary>
    private ActiveMember Member => Services.GetRequiredService<ActiveMember>();

    private static IElement Row(IRenderedComponent<Household> cut, Guid userId) =>
        cut.Find($"[data-testid='household-member-{userId}']");

    /// <summary>Die Module eines QR-Codes: das Attribut d des Pfads im SVG.</summary>
    internal static string Modules(string svg) =>
        System.Text.RegularExpressions.Regex.Match(svg, "<path[^>]* d=\"([^\"]+)\"").Groups[1].Value;

    private static bool Has(IRenderedComponent<IComponent> cut, string testId) =>
        cut.FindAll($"[data-testid='{testId}']").Count > 0;

    [Fact]
    public void The_page_is_reachable_at_haushalt_and_needs_a_sign_in()
    {
        Assert.Equal("/haushalt", typeof(Household).GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.NotNull(typeof(Household).GetCustomAttribute<AuthorizeAttribute>());
    }

    // A16
    [Fact]
    public void Mehr_leads_to_the_household()
    {
        var cut = Render<Mehr>();

        Assert.Equal("/haushalt", cut.Find("[data-testid='more-household']").GetAttribute("href"));
        Assert.Contains("Haushalt", cut.Find("[data-testid='more-household']").TextContent);
    }

    // A16
    [Fact]
    public void Without_a_household_the_page_offers_founding_and_the_paste_field()
    {
        var cut = OpenWithoutHousehold();

        Assert.Contains("Haushalt gründen", cut.Find("[data-testid='household-found']").TextContent);
        Assert.NotEmpty(cut.FindAll("[data-testid='household-found-name']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='household-paste']"));
        Assert.Empty(cut.FindAll("[data-testid='household-card']"));
        Assert.Equal(0, HouseholdApiFake.ListInvitationsCalls);
        Assert.DoesNotContain("Etwas ist schiefgelaufen", cut.Markup);
    }

    // A1, A16 (Seite)
    [Fact]
    public void Founding_with_a_name_shows_the_new_household()
    {
        var cut = OpenWithoutHousehold();

        Field(cut, "household-found-name").Change("Familie Muster");
        cut.Find("[data-testid='household-found']").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='household-card']")));
        Assert.Equal(["Familie Muster"], HouseholdApiFake.Founded);
        Assert.Contains("Familie Muster", cut.Find("[data-testid='household-name']").TextContent);
        Assert.Empty(cut.FindAll("[data-testid='household-found']"));
    }

    // Spec 69, A12: Nach dem Gründen kennt der Umschalter den neuen Haushalt, ohne Neustart.
    [Fact]
    public async Task Founding_refreshes_the_switch()
    {
        var cut = OpenWithoutHousehold();
        await Member.LoadAsync();
        Assert.Null(Member.Household);

        cut.Find("[data-testid='household-found']").Click();

        cut.WaitForAssertion(() => Assert.Equal("Haushalt von Anna", Member.Household?.Name));
    }

    // A1 (Seite): Ein leeres Feld lässt der API den vorgegebenen Namen.
    [Fact]
    public void Founding_without_a_name_leaves_the_default_to_the_api()
    {
        var cut = OpenWithoutHousehold();

        cut.Find("[data-testid='household-found']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Haushalt von Anna", cut.Markup));
        Assert.Equal([null], HouseholdApiFake.Founded);
    }

    [Fact]
    public void A_refused_founding_shows_why()
    {
        HouseholdApiFake.Error = ErrorCodes.AlreadyInHousehold;
        var cut = OpenWithoutHousehold();

        cut.Find("[data-testid='household-found']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Du bist schon in einem Haushalt.", cut.Markup));
    }

    // A16
    [Fact]
    public void The_founder_sees_name_members_roles_du_and_the_founder_buttons()
    {
        HouseholdApiFake.Household = HouseholdApiFake.Household! with { Members = [FakeHouseholdApi.Anna(), FakeHouseholdApi.Ben()] };

        var cut = OpenPage();

        Assert.Contains("Haushalt von Anna", cut.Markup);
        Assert.Contains("2 Mitglieder", cut.Markup);
        Assert.Contains("Grundbedarf 50 %", cut.Markup);
        var anna = Row(cut, FakeHouseholdApi.AnnaId).TextContent;
        Assert.Contains("Anna Muster", anna);
        Assert.Contains("(du)", anna);
        Assert.Contains("Gründer", anna);
        Assert.Contains("anna@example.org", anna);
        var ben = Row(cut, FakeHouseholdApi.BenId).TextContent;
        Assert.Contains("Ben Beispiel", ben);
        Assert.Contains("Mitglied", ben);
        Assert.DoesNotContain("(du)", ben);
        Assert.Contains("Einladen", cut.Find("[data-testid='household-invite']").TextContent);
        Assert.Contains("Richtwerte", cut.Find("[data-testid='household-quotas']").TextContent);
        Assert.True(Has(cut, $"household-member-remove-{FakeHouseholdApi.BenId}"));
        Assert.False(Has(cut, $"household-member-remove-{FakeHouseholdApi.AnnaId}"));
        // Mit anderen im Haushalt ist Austreten ausgegraut (A20), Auflösen gibt es nicht.
        Assert.Equal("true", cut.Find("[data-testid='household-leave']").GetAttribute("aria-disabled"));
        Assert.False(Has(cut, "household-dissolve"));
    }

    // A20: Der Knopf bleibt sichtbar und erreichbar, ist aber ausgegraut und sagt, warum.
    [Fact]
    public void The_founder_with_others_sees_austreten_greyed_out_with_the_reason()
    {
        HouseholdApiFake.Household = HouseholdApiFake.Household! with { Members = [FakeHouseholdApi.Anna(), FakeHouseholdApi.Ben()] };
        var cut = OpenPage();

        var leave = cut.Find("[data-testid='household-leave']");

        Assert.Contains("Austreten", leave.TextContent);
        Assert.Equal("true", leave.GetAttribute("aria-disabled"));
        // Nicht disabled: So bleibt der Knopf im Fokus, und ein Screenreader liest den Grund vor.
        Assert.False(leave.HasAttribute("disabled"));
        var reason = cut.Find("[data-testid='household-leave-reason']");
        Assert.Equal("Als Gründer kannst du erst austreten, wenn du allein im Haushalt bist.", reason.TextContent.Trim());
        Assert.Equal(reason.Id, leave.GetAttribute("aria-describedby"));

        leave.Click();

        Assert.DoesNotContain("Haushalt verlassen?", dialogs.Markup);
        Assert.Equal(0, HouseholdApiFake.Leaves);
    }

    // A23: Bei 10 offenen Einladungen ist Einladen ausgegraut und sagt, warum, statt angeboten und dann abgelehnt zu werden.
    [Fact]
    public void With_ten_open_invitations_einladen_is_greyed_out_with_the_reason()
    {
        for (var i = 0; i < 10; i++)
        {
            HouseholdApiFake.Invitations.Add(new InvitationResponse(Guid.NewGuid(), $"token{i}",
                new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero), $"{FakeHouseholdApi.PublicUrl}/einladung/token{i}"));
        }
        var cut = OpenPage();
        cut.WaitForAssertion(() => Assert.Equal(10, cut.FindAll("[data-testid^='household-invitation-revoke-']").Count));

        var invite = cut.Find("[data-testid='household-invite']");

        Assert.Contains("Einladen", invite.TextContent);
        Assert.Equal("true", invite.GetAttribute("aria-disabled"));
        Assert.False(invite.HasAttribute("disabled"));
        var reason = cut.Find("[data-testid='household-invite-reason']");
        Assert.Equal("Es gibt schon 10 offene Einladungen. Ziehe eine zurück, bevor du eine neue anlegst.", reason.TextContent.Trim());
        Assert.Equal(reason.Id, invite.GetAttribute("aria-describedby"));

        invite.Click();

        Assert.Equal(0, HouseholdApiFake.CreatedInvitations);
    }

    // A23: Unter 10 offenen Einladungen gibt es Einladen ohne Grund darunter.
    [Fact]
    public void Below_ten_open_invitations_einladen_is_offered()
    {
        HouseholdApiFake.Invitations.Add(new InvitationResponse(Guid.NewGuid(), "token0",
            new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero), $"{FakeHouseholdApi.PublicUrl}/einladung/token0"));
        var cut = OpenPage();

        Assert.False(cut.Find("[data-testid='household-invite']").HasAttribute("aria-disabled"));
        Assert.False(Has(cut, "household-invite-reason"));
    }

    // A20: Ist Ben gegangen, bietet die Seite dem Gründer Auflösen an.
    [Fact]
    public void After_ben_left_the_founder_is_offered_to_dissolve()
    {
        HouseholdApiFake.Household = HouseholdApiFake.Household! with { Members = [FakeHouseholdApi.Anna(), FakeHouseholdApi.Ben()] };
        var cut = OpenPage();
        Assert.False(Has(cut, "household-dissolve"));

        cut.Find($"[data-testid='household-member-remove-{FakeHouseholdApi.BenId}']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Ben entfernen?", dialogs.Markup));
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Entfernen").Click();

        cut.WaitForAssertion(() => Assert.True(Has(cut, "household-dissolve")));
        Assert.False(Has(cut, "household-leave"));
        Assert.False(Has(cut, "household-leave-reason"));
    }

    // A16
    [Fact]
    public void A_member_sees_the_rows_read_only()
    {
        HouseholdApiFake.Household = HouseholdApiFake.Household! with
        {
            Members = [FakeHouseholdApi.Anna(isMe: false), FakeHouseholdApi.Ben(isMe: true)],
        };

        var cut = OpenPage();

        Assert.Contains("Gründer", Row(cut, FakeHouseholdApi.AnnaId).TextContent);
        Assert.Contains("Ben Beispiel", Row(cut, FakeHouseholdApi.BenId).TextContent);
        Assert.False(Has(cut, "household-invite"));
        Assert.False(Has(cut, "household-quotas"));
        Assert.False(Has(cut, "household-edit"));
        Assert.False(Has(cut, $"household-member-remove-{FakeHouseholdApi.AnnaId}"));
        Assert.Equal(0, HouseholdApiFake.ListInvitationsCalls);
        Assert.True(Has(cut, "household-leave"));
        Assert.False(Has(cut, "household-dissolve"));
    }

    // A17
    [Fact]
    public void Einladen_shows_the_link_its_qr_code_and_shares_it()
    {
        var cut = OpenPage();

        cut.Find("[data-testid='household-invite']").Click();

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='invite-link']")));
        var token = Assert.Single(HouseholdApiFake.Invitations).Token;
        // Der Link kommt fertig aus der API, mit der öffentlichen Adresse des Servers (A19).
        var link = $"https://duofy.example.de/einladung/{token}";
        Assert.Equal(link, dialogs.Find("[data-testid='invite-link']").TextContent.Trim());
        // Der QR-Code zeichnet genau die Module, die QRCoder für diesen Link berechnet, und keinen anderen.
        var drawn = dialogs.Find("[data-testid='invite-qr'] path").GetAttribute("d");
        Assert.Equal(Modules(InvitationQr.Svg(link)), drawn);
        Assert.NotEqual(Modules(InvitationQr.Svg(link + "x")), drawn);

        dialogs.Find("[data-testid='invite-share']").Click();

        cut.WaitForAssertion(() => Assert.Equal(("Einladung in Haushalt von Anna", link), Assert.Single(ShareFake.Shared)));
    }

    // A19: Ohne öffentliche Adresse legt die API keine Einladung an; die Seite sagt, was fehlt.
    [Fact]
    public void Without_a_public_address_the_page_says_what_is_missing()
    {
        HouseholdApiFake.Error = ErrorCodes.PublicUrlMissing;
        var cut = OpenPage();

        cut.Find("[data-testid='household-invite']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Für Einladungen fehlt die öffentliche Adresse des Servers.", cut.Markup));
        Assert.Empty(dialogs.FindAll("[data-testid='invite-link']"));
    }

    [Fact]
    public void A_refused_invitation_shows_why()
    {
        HouseholdApiFake.Error = ErrorCodes.TooManyInvitations;
        var cut = OpenPage();

        cut.Find("[data-testid='household-invite']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Es gibt schon 10 offene Einladungen.", cut.Markup));
        Assert.Empty(dialogs.FindAll("[data-testid='invite-link']"));
    }

    // A3 (Seite)
    [Fact]
    public void Open_invitations_are_listed_and_revoked_after_asking_once()
    {
        HouseholdApiFake.Invitations.Add(new InvitationResponse(Guid.NewGuid(), "offen", new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero), $"{FakeHouseholdApi.PublicUrl}/einladung/offen"));
        var invitation = HouseholdApiFake.Invitations[0];
        var cut = OpenPage();
        cut.WaitForAssertion(() => Assert.Contains("Gültig bis 04.11.2026", cut.Markup));

        cut.Find($"[data-testid='household-invitation-revoke-{invitation.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Einladung zurückziehen?", dialogs.Markup));
        Assert.Empty(HouseholdApiFake.Revoked);
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Zurückziehen").Click();

        cut.WaitForAssertion(() => Assert.Equal([invitation.Id], HouseholdApiFake.Revoked));
        cut.WaitForAssertion(() => Assert.DoesNotContain("Gültig bis 04.11.2026", cut.Markup));
    }

    // A18
    [Fact]
    public void A_pasted_link_opens_the_invitation_page()
    {
        var cut = OpenWithoutHousehold();

        Field(cut, "household-paste").Change("https://duofy.example/einladung/abc_DEF-1");
        cut.Find("[data-testid='household-paste-open']").Click();

        cut.WaitForAssertion(() => Assert.Equal("/einladung/abc_DEF-1", CurrentPath));
    }

    [Fact]
    public void Something_else_pasted_is_explained()
    {
        var cut = OpenWithoutHousehold();

        Field(cut, "household-paste").Change("https://duofy.example/haushalt");
        cut.Find("[data-testid='household-paste-open']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Das ist kein Einladungslink.", cut.Markup));
        Assert.Equal("/", CurrentPath);
    }

    // A10 (Seite)
    [Fact]
    public void Richtwerte_saves_quotas_and_name()
    {
        var cut = OpenPage();

        cut.Find("[data-testid='household-quotas']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='household-dialog-save']")));
        Field(dialogs, "household-dialog-name").Change("Familie Muster");
        Field(dialogs, "household-dialog-quota-needs").Change("60");
        Field(dialogs, "household-dialog-quota-wants").Change("20");
        dialogs.Find("[data-testid='household-dialog-save']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Grundbedarf 60 %", cut.Markup));
        Assert.Contains("Familie Muster", cut.Markup);
        Assert.Equal(new UpdateHouseholdRequest("Familie Muster", 60, 20, 20), Assert.Single(HouseholdApiFake.Updates));
    }

    [Fact]
    public void Wrong_quotas_stay_in_the_dialog_with_the_reason()
    {
        HouseholdApiFake.Error = ErrorCodes.QuotaInvalid;
        var cut = OpenPage();

        cut.Find("[data-testid='household-quotas']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='household-dialog-save']")));
        dialogs.Find("[data-testid='household-dialog-save']").Click();

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='household-dialog-error']")));
        Assert.Contains("Grundbedarf 50 %", cut.Markup);
    }

    // A12 (Seite)
    [Fact]
    public void Leaving_asks_once_and_then_offers_founding_again()
    {
        HouseholdApiFake.Household = HouseholdApiFake.Household! with
        {
            Members = [FakeHouseholdApi.Anna(isMe: false), FakeHouseholdApi.Ben(isMe: true)],
        };
        var cut = OpenPage();

        cut.Find("[data-testid='household-leave']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Haushalt verlassen?", dialogs.Markup));
        Assert.Equal(0, HouseholdApiFake.Leaves);
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Austreten").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='household-found']")));
        Assert.Equal(1, HouseholdApiFake.Leaves);
        Assert.Contains("Du bist ausgetreten", cut.Markup);
    }

    // Spec 69, A12: Nach dem Austreten bietet der Umschalter keinen Haushalt mehr an, und die gewählte Haushaltssicht ist weg.
    [Fact]
    public void Leaving_refreshes_the_switch_and_drops_the_household_view()
    {
        HouseholdApiFake.Household = HouseholdApiFake.Household! with
        {
            Members = [FakeHouseholdApi.Anna(isMe: false), FakeHouseholdApi.Ben(isMe: true)],
        };
        var cut = OpenPage();
        var switcher = Render<MemberSwitch>();
        switcher.WaitForAssertion(() => Assert.True(Member.CanShowHousehold));
        Member.ChooseHousehold();
        Assert.True(Member.IsHousehold);

        cut.Find("[data-testid='household-leave']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Haushalt verlassen?", dialogs.Markup));
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Austreten").Click();

        cut.WaitForAssertion(() => Assert.Null(Member.Household));
        Assert.False(Member.CanShowHousehold);
        Assert.False(Member.IsHousehold);
        switcher.Find("[data-testid='member-profile']").Click();
        switcher.WaitForAssertion(() => Assert.Equal("true", switcher.Find("[data-testid='member-choice-household']").GetAttribute("aria-disabled")));
        Assert.Equal("Ich", switcher.Find("[data-testid='member-profile']").TextContent.Trim());
    }

    // Spec 69, A12: Nach dem Auflösen gibt es für den Umschalter keinen Haushalt mehr.
    [Fact]
    public async Task Dissolving_refreshes_the_switch()
    {
        var cut = OpenPage();
        await Member.LoadAsync();
        Assert.NotNull(Member.Household);

        cut.Find("[data-testid='household-dissolve']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Haushalt auflösen?", dialogs.Markup));
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Auflösen").Click();

        cut.WaitForAssertion(() => Assert.Null(Member.Household));
    }

    // Spec 69, A12: Ist Ben entfernt, ist Anna allein; der Umschalter bietet keinen Haushalt mehr an.
    [Fact]
    public async Task Removing_refreshes_the_switch_and_drops_the_household_view()
    {
        HouseholdApiFake.Household = HouseholdApiFake.Household! with { Members = [FakeHouseholdApi.Anna(), FakeHouseholdApi.Ben()] };
        var cut = OpenPage();
        await Member.LoadAsync();
        Member.ChooseHousehold();
        Assert.True(Member.IsHousehold);

        cut.Find($"[data-testid='household-member-remove-{FakeHouseholdApi.BenId}']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Ben entfernen?", dialogs.Markup));
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Entfernen").Click();

        cut.WaitForAssertion(() => Assert.False(Member.CanShowHousehold));
        Assert.False(Member.IsHousehold);
        Assert.Single(Member.Household!.Members);
    }

    // A12 (Seite): Allein löst der Gründer den Haushalt auf.
    [Fact]
    public void The_founder_alone_dissolves_after_asking_once()
    {
        var cut = OpenPage();

        Assert.False(Has(cut, "household-leave"));
        cut.Find("[data-testid='household-dissolve']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Haushalt auflösen?", dialogs.Markup));
        Assert.Equal(0, HouseholdApiFake.Leaves);
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Auflösen").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='household-found']")));
        Assert.Equal(1, HouseholdApiFake.Leaves);
        Assert.Contains("Der Haushalt ist aufgelöst.", cut.Markup);
    }

    [Fact]
    public void A_refused_dissolving_shows_why()
    {
        HouseholdApiFake.Error = ErrorCodes.FounderCannotLeave;
        var cut = OpenPage();

        cut.Find("[data-testid='household-dissolve']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Haushalt auflösen?", dialogs.Markup));
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Auflösen").Click();

        cut.WaitForAssertion(() => Assert.Contains("Du hast den Haushalt gegründet.", cut.Markup));
    }

    // A13 (Seite)
    [Fact]
    public void Removing_ben_asks_once()
    {
        HouseholdApiFake.Household = HouseholdApiFake.Household! with { Members = [FakeHouseholdApi.Anna(), FakeHouseholdApi.Ben()] };
        var cut = OpenPage();

        cut.Find($"[data-testid='household-member-remove-{FakeHouseholdApi.BenId}']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Ben entfernen?", dialogs.Markup));
        Assert.Empty(HouseholdApiFake.Removed);
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Entfernen").Click();

        cut.WaitForAssertion(() => Assert.Equal([FakeHouseholdApi.BenId], HouseholdApiFake.Removed));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll($"[data-testid='household-member-{FakeHouseholdApi.BenId}']")));
        Assert.Contains("Ben ist nicht mehr im Haushalt.", cut.Markup);
    }
}

/// <summary>
/// Die Seite /haushalt in der App: Der Link zeigt auf die öffentliche Adresse aus der API, nicht auf den Server,
/// mit dem die App gerade verbunden ist (A19).
/// </summary>
public class HouseholdPageInAppTests : PageTestContext
{
    private readonly IRenderedComponent<MudDialogProvider> dialogs;

    public HouseholdPageInAppTests()
    {
        Services.AddSingleton<IAppHost, AppHost>();
        Render<MudPopoverProvider>();
        dialogs = Render<MudDialogProvider>();
    }

    [Fact]
    public void The_link_points_to_the_public_address_and_not_to_the_connected_server()
    {
        var cut = Render<Household>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='household-invite']")));

        cut.Find("[data-testid='household-invite']").Click();

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='invite-link']")));
        var token = Assert.Single(HouseholdApiFake.Invitations).Token;
        Assert.Equal($"https://duofy.example.de/einladung/{token}", dialogs.Find("[data-testid='invite-link']").TextContent.Trim());
    }
}
