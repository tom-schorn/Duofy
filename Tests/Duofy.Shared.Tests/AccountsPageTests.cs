using System.Reflection;
using AngleSharp.Dom;
using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace Duofy.Shared.Tests;

/// <summary>Die Seite /konten: Liste mit Stand und Standardkonto, aufgelöste Konten, Anlegen und Ändern (Spec 58).</summary>
public class AccountsPageTests : PageTestContext
{
    // Dialoge gehen in ihrer Hülle auf; im echten Layout steht sie im Rahmen.
    private readonly IRenderedComponent<IComponent> dialogs;

    public AccountsPageTests()
    {
        dialogs = RenderDialogHost();
    }

    private IRenderedComponent<Accounts> OpenPage(params AccountResponse[] accounts)
    {
        AccountApiFake.Accounts.AddRange(accounts);
        var cut = Render<Accounts>();
        cut.WaitForAssertion(() => Assert.True(AccountApiFake.ListCalls > 0));
        return cut;
    }

    [Fact]
    public void The_page_is_reachable_at_konten_and_needs_a_sign_in()
    {
        Assert.Equal("/konten", typeof(Accounts).GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.NotNull(typeof(Accounts).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void The_list_shows_name_type_balance_and_the_default_star()
    {
        var giro = FakeAccountApi.Account("Giro", balance: 1234.5m, isDefault: true);
        var savings = FakeAccountApi.Account("Notgroschen", AccountTypes.Savings, balance: 5000m);

        var cut = OpenPage(giro, savings);

        cut.WaitForAssertion(() => Assert.Contains("Giro", Row(cut, giro).TextContent));
        Assert.Contains("Girokonto", Row(cut, giro).TextContent);
        Assert.Contains("1.234,50 €", Row(cut, giro).TextContent);
        Assert.Contains("Sparkonto", Row(cut, savings).TextContent);
        Assert.Equal("true", Star(cut, giro).GetAttribute("aria-pressed"));
        Assert.Equal("false", Star(cut, savings).GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Without_accounts_the_page_invites_to_create_one()
    {
        var cut = OpenPage();

        cut.WaitForAssertion(() => Assert.Contains("Du hast noch kein Konto.", cut.Markup));
    }

    // A5 (Seite)
    [Fact]
    public void The_star_marks_another_account_as_default()
    {
        var giro = FakeAccountApi.Account("Giro", isDefault: true);
        var cash = FakeAccountApi.Account("Bargeld", AccountTypes.Cash);
        var cut = OpenPage(giro, cash);

        Star(cut, cash).Click();

        cut.WaitForAssertion(() => Assert.Equal("true", Star(cut, cash).GetAttribute("aria-pressed")));
        Assert.Equal((cash.Id, new UpdateAccountRequest(IsDefault: true)), Assert.Single(AccountApiFake.Updated));
        Assert.Equal("false", Star(cut, giro).GetAttribute("aria-pressed"));
    }

    // A6 (Seite): Aufgelöste Konten sind eingeklappt.
    [Fact]
    public void Closing_moves_the_account_to_the_folded_away_closed_ones()
    {
        var giro = FakeAccountApi.Account("Giro", isDefault: true);
        var cut = OpenPage(giro);

        cut.Find($"[data-testid='account-close-{giro.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Konto auflösen?", dialogs.Markup));
        Assert.Empty(AccountApiFake.Updated);
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Auflösen").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll($"[data-testid='account-{giro.Id}']")));
        Assert.Equal(new UpdateAccountRequest(Active: false), Assert.Single(AccountApiFake.Updated).Request);
        Assert.Contains("Aufgelöste Konten (1)", cut.Markup);

        cut.Find("[data-testid='accounts-show-closed']").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"[data-testid='account-{giro.Id}']")));
        Assert.Contains("aufgelöst", Row(cut, giro).TextContent);
    }

    // Mangel S1 und S13: Auflösen fragt nach und der Knopf nennt sein Konto.
    [Fact]
    public void Closing_asks_first_names_the_account_and_keeps_it_when_cancelled()
    {
        var giro = FakeAccountApi.Account("Giro", isDefault: true);
        var cut = OpenPage(giro);

        Assert.Equal("Konto Giro auflösen", cut.Find($"[data-testid='account-close-{giro.Id}']").GetAttribute("aria-label"));
        cut.Find($"[data-testid='account-close-{giro.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Konto auflösen?", dialogs.Markup));
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Abbrechen").Click();

        dialogs.WaitForAssertion(() => Assert.DoesNotContain("Konto auflösen?", dialogs.Markup));
        Assert.Empty(AccountApiFake.Updated);
        Assert.NotEmpty(cut.FindAll($"[data-testid='account-{giro.Id}']"));
    }

    // A7 (Seite)
    [Fact]
    public void A_closed_account_can_be_reopened()
    {
        var old = FakeAccountApi.Account("Altes Konto", active: false);
        var cut = OpenPage(old);
        cut.Find("[data-testid='accounts-show-closed']").Click();

        cut.WaitForAssertion(() => cut.Find($"[data-testid='account-reopen-{old.Id}']").Click());

        cut.WaitForAssertion(() => Assert.Equal(new UpdateAccountRequest(Active: true), Assert.Single(AccountApiFake.Updated).Request));
        cut.WaitForAssertion(() => Assert.DoesNotContain("Aufgelöste Konten", cut.Markup));
        Assert.NotEmpty(cut.FindAll($"[data-testid='account-close-{old.Id}']"));
    }

    // A3
    [Fact]
    public void In_the_create_dialog_savings_turns_the_switch_off_and_checking_and_cash_turn_it_on()
    {
        var cut = OpenPage();
        cut.Find("[data-testid='accounts-add']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Neues Konto", dialogs.Markup));
        Assert.True(SwitchIsOn());

        dialogs.Find("[data-testid='account-type']").Change(AccountTypes.Savings);
        dialogs.WaitForAssertion(() => Assert.False(SwitchIsOn()));

        dialogs.Find("[data-testid='account-type']").Change(AccountTypes.Cash);
        dialogs.WaitForAssertion(() => Assert.True(SwitchIsOn()));

        dialogs.Find("[data-testid='account-type']").Change(AccountTypes.Checking);
        dialogs.WaitForAssertion(() => Assert.True(SwitchIsOn()));
    }

    // A1 (Seite)
    [Fact]
    public void Saving_the_create_dialog_posts_the_account_and_lists_it()
    {
        var cut = OpenPage();
        cut.Find("[data-testid='accounts-add']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Neues Konto", dialogs.Markup));

        Input(dialogs, "account-name").Change("Giro");
        Input(dialogs, "account-opening-balance").Change("1250");
        Input(dialogs, "account-opening-date").Change("2026-10-01");
        Input(dialogs, "account-iban").Change("de89 3704 0044 0532 0130 00");
        dialogs.Find("[data-testid='account-save']").Click();

        cut.WaitForAssertion(() => Assert.Single(AccountApiFake.Created));
        Assert.Equal(
            new CreateAccountRequest("Giro", AccountTypes.Checking, 1250m, new DateOnly(2026, 10, 1), true, "de89 3704 0044 0532 0130 00"),
            AccountApiFake.Created[0]);
        cut.WaitForAssertion(() => Assert.Contains("Giro", cut.Markup));
        Assert.Contains("Das Konto wurde gespeichert.", cut.Markup);
    }

    // A8 (Seite): Der Fehler der API steht im Dialog, der Dialog bleibt offen.
    [Fact]
    public void An_error_of_the_api_is_shown_in_the_dialog()
    {
        AccountApiFake.Error = ErrorCodes.IbanInvalid;
        var cut = OpenPage();
        cut.Find("[data-testid='accounts-add']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Neues Konto", dialogs.Markup));

        Input(dialogs, "account-name").Change("Giro");
        dialogs.Find("[data-testid='account-save']").Click();

        dialogs.WaitForAssertion(() => Assert.Contains("Die IBAN stimmt nicht.", dialogs.Markup));
        Assert.Contains("Neues Konto", dialogs.Markup);
    }

    [Fact]
    public void Editing_opens_the_dialog_with_the_saved_values_and_saves_with_patch()
    {
        var giro = FakeAccountApi.Account("Giro", balance: 100m, isDefault: true);
        var cut = OpenPage(giro);

        cut.Find($"[data-testid='account-edit-{giro.Id}']").Click();

        dialogs.WaitForAssertion(() => Assert.Contains("Konto ändern", dialogs.Markup));
        Assert.Equal("Giro", Input(dialogs, "account-name").GetAttribute("value"));
        Input(dialogs, "account-name").Change("Hauptkonto");
        dialogs.Find("[data-testid='account-save']").Click();

        cut.WaitForAssertion(() => Assert.Single(AccountApiFake.Updated));
        var (id, request) = AccountApiFake.Updated[0];
        Assert.Equal(giro.Id, id);
        Assert.Equal("Hauptkonto", request.Name);
        Assert.Equal(100m, request.OpeningBalance);
        cut.WaitForAssertion(() => Assert.Contains("Hauptkonto", cut.Markup));
    }
    [Fact]
    public void The_page_and_the_dialog_are_built_from_the_blocks_and_the_default_star_is_filled()
    {
        var giro = FakeAccountApi.Account("Giro", isDefault: true);
        var other = FakeAccountApi.Account("Notgroschen", AccountTypes.Savings);
        var cut = OpenPage(giro, other);
        cut.WaitForAssertion(() => Assert.Contains("Giro", Row(cut, giro).TextContent));

        Assert.Contains("data-row-frame", Row(cut, giro).ClassList);
        Assert.Contains("icon-filled", Star(cut, giro).QuerySelector("svg")!.ClassList);
        Assert.DoesNotContain("icon-filled", Star(cut, other).QuerySelector("svg")!.ClassList);

        cut.Find("[data-testid='accounts-add']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Neues Konto", dialogs.Markup));
        Assert.NotEmpty(dialogs.FindAll($"label[for='{Input(dialogs, "account-opening-balance").Id}']"));
        Assert.Equal("date", Input(dialogs, "account-opening-date").GetAttribute("type"));
    }

    // --- Hilfen -------------------------------------------------------------

    private static IElement Row(IRenderedComponent<Accounts> cut, AccountResponse account) =>
        cut.Find($"[data-testid='account-{account.Id}']");

    private static IElement Star(IRenderedComponent<Accounts> cut, AccountResponse account) =>
        cut.Find($"[data-testid='account-default-{account.Id}']");

    private bool SwitchIsOn() => dialogs.Find("[data-testid='account-counts-as-available']").GetAttribute("aria-checked") == "true";

    private static IElement Input(IRenderedComponent<IComponent> cut, string testId)
    {
        var element = cut.Find($"[data-testid='{testId}']");
        return element.TagName is "INPUT" or "TEXTAREA" ? element : element.QuerySelector("input,textarea")!;
    }
}
