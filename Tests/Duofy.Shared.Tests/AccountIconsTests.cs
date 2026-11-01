using AngleSharp.Dom;
using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Icons;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Duofy.Shared.Tests;

/// <summary>
/// Symbole für Konten (Issues 14 und 16): Standard je Art, am Konto wählbar, in Listen und in der Kontoauswahl von
/// Buchung, Vertrag und Plan-Posten mit Namen. Die Auswahl ist eine Gruppe von Optionsfeldern (Tastatur und Vorlese-Programm
/// bekommen sie von selbst), kein Auswahlfeld, denn darin lässt sich kein Symbol zeigen.
/// </summary>
public class AccountIconsTests : PageTestContext
{
    private readonly IRenderedComponent<MudDialogProvider> dialogs;

    public AccountIconsTests()
    {
        Render<MudPopoverProvider>();
        dialogs = Render<MudDialogProvider>();
    }

    private static IElement Group(IRenderedComponent<IComponent> cut, string testId) => cut.Find($"[data-testid='{testId}']");

    private static IReadOnlyList<string> Options(IRenderedComponent<IComponent> cut, string testId) =>
        Group(cut, testId).QuerySelectorAll("label.account-option").Select(o => o.QuerySelector(".account-option-name")!.TextContent.Trim()).ToList();

    private static string? Chosen(IRenderedComponent<IComponent> cut, string testId) =>
        Group(cut, testId).QuerySelectorAll("input[type='radio']").SingleOrDefault(i => i.HasAttribute("checked"))?.GetAttribute("value");

    private static string? IconOf(IElement option) => option.QuerySelector("[data-icon]")?.GetAttribute("data-icon");

    // --- Zuordnung -------------------------------------------------------------------------------------------

    // R1, "an einer Stelle als Name": jeder Name der API hat genau ein Zeichen, und das gibt es in der Sammlung.
    [Fact]
    public void Every_icon_name_of_the_api_maps_to_a_symbol_of_the_set_in_one_place()
    {
        foreach (var key in AccountIcons.All)
        {
            var symbol = AccountIconSet.SymbolName(key);
            Assert.Contains(symbol, TablerIcons.Names);
        }
        Assert.Equal(AccountIcons.All.Count, AccountIcons.All.Select(AccountIconSet.SymbolName).Distinct().Count());
        Assert.Equal(AccountIconSet.SymbolName(AccountIcons.Bank), AccountIconSet.SymbolName("unbekannt"));
    }

    // --- Entwurf ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_new_draft_shows_the_default_icon_of_its_type_and_sends_none_until_one_is_chosen()
    {
        var draft = AccountDraft.ForNew(new DateOnly(2026, 10, 21));

        Assert.Equal(AccountIcons.Bank, draft.Icon);
        Assert.Null(draft.ToCreateRequest().Icon);

        draft.SetType(AccountTypes.Savings);
        Assert.Equal(AccountIcons.Piggy, draft.Icon);
        Assert.Null(draft.ToCreateRequest().Icon);

        draft.SetIcon(AccountIcons.Wallet);
        draft.SetType(AccountTypes.Cash);
        Assert.Equal(AccountIcons.Wallet, draft.Icon);
        Assert.Equal(AccountIcons.Wallet, draft.ToCreateRequest().Icon);
    }

    [Fact]
    public void Choosing_the_default_icon_again_sends_none_and_changing_a_saved_account_pins_nothing()
    {
        var draft = AccountDraft.ForNew(new DateOnly(2026, 10, 21));
        draft.SetIcon(AccountIcons.Coins);
        draft.SetIcon(AccountIcons.Bank);
        Assert.Null(draft.ToCreateRequest().Icon);

        var saved = FakeAccountApi.Account("Spar", AccountTypes.Savings);
        var edit = AccountDraft.From(saved);
        edit.Name = "Neu";
        Assert.Equal(AccountIcons.Piggy, edit.Icon);
        Assert.Null(edit.ToUpdateRequest().Icon);

        edit.SetIcon(AccountIcons.Coins);
        Assert.Equal(AccountIcons.Coins, edit.ToUpdateRequest().Icon);
        edit.SetIcon(AccountIcons.Piggy);
        Assert.Equal("", edit.ToUpdateRequest().Icon);
    }

    [Fact]
    public void A_saved_account_with_its_own_icon_keeps_it_in_the_draft()
    {
        var saved = FakeAccountApi.Account("Karte", AccountTypes.Checking) with { Icon = AccountIcons.Card };

        var draft = AccountDraft.From(saved);

        Assert.Equal(AccountIcons.Card, draft.Icon);
        Assert.Null(draft.ToUpdateRequest().Icon);
    }

    // --- Kontodialog und Liste -------------------------------------------------------------------------------

    private IRenderedComponent<Accounts> OpenPage(params AccountResponse[] accounts)
    {
        AccountApiFake.Accounts.AddRange(accounts);
        var cut = Render<Accounts>();
        cut.WaitForAssertion(() => Assert.True(AccountApiFake.ListCalls > 0));
        return cut;
    }

    // R3 (Issue 14): Das Symbol erscheint in der Liste des Kontos.
    [Fact]
    public void The_account_list_shows_the_icon_of_each_account_with_the_default_of_its_type()
    {
        var giro = FakeAccountApi.Account("Giro", isDefault: true);
        var spar = FakeAccountApi.Account("Notgroschen", AccountTypes.Savings);
        var karte = FakeAccountApi.Account("Karte", AccountTypes.Checking) with { Icon = AccountIcons.Card };
        var alt = FakeAccountApi.Account("Alt", AccountTypes.Cash, active: false);

        var cut = OpenPage(giro, spar, karte, alt);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"[data-testid='account-{giro.Id}']")));
        Assert.Equal(AccountIcons.Bank, cut.Find($"[data-testid='account-{giro.Id}'] [data-icon]").GetAttribute("data-icon"));
        Assert.Equal(AccountIcons.Piggy, cut.Find($"[data-testid='account-{spar.Id}'] [data-icon]").GetAttribute("data-icon"));
        Assert.Equal(AccountIcons.Card, cut.Find($"[data-testid='account-{karte.Id}'] [data-icon]").GetAttribute("data-icon"));
        cut.Find("[data-testid='accounts-show-closed']").Click();
        Assert.Equal(AccountIcons.Cash, cut.Find($"[data-testid='account-{alt.Id}'] [data-icon]").GetAttribute("data-icon"));
    }

    // R2 (Issue 14)
    [Fact]
    public void The_account_dialog_offers_every_icon_by_name_starts_on_the_default_and_saves_the_choice()
    {
        var cut = OpenPage();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='accounts-add']")));
        cut.Find("[data-testid='accounts-add']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='account-icon']")));

        Assert.Equal(["Bank", "Sparschwein", "Bargeld", "Karte", "Geldbeutel", "Münzen"], Options(dialogs, "account-icon"));
        Assert.Equal(AccountIcons.Bank, Chosen(dialogs, "account-icon"));

        dialogs.Find("[data-testid='account-type']").Change(AccountTypes.Savings);
        Assert.Equal(AccountIcons.Piggy, Chosen(dialogs, "account-icon"));

        dialogs.Find("[data-testid='account-icon'] input[value='coins']").Change(true);
        Field(dialogs, "account-name").Change("Rücklage");
        dialogs.Find("[data-testid='account-save']").Click();

        cut.WaitForAssertion(() => Assert.Single(AccountApiFake.Created));
        Assert.Equal(AccountIcons.Coins, AccountApiFake.Created[0].Icon);
    }

    [Fact]
    public void Saving_a_new_account_without_a_choice_sends_no_icon()
    {
        var cut = OpenPage();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='accounts-add']")));
        cut.Find("[data-testid='accounts-add']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='account-icon']")));
        Field(dialogs, "account-name").Change("Giro");

        dialogs.Find("[data-testid='account-save']").Click();

        cut.WaitForAssertion(() => Assert.Single(AccountApiFake.Created));
        Assert.Null(AccountApiFake.Created[0].Icon);
    }

    [Fact]
    public void Editing_an_account_opens_with_its_icon_chosen()
    {
        var karte = FakeAccountApi.Account("Karte") with { Icon = AccountIcons.Card };
        var cut = OpenPage(karte);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"[data-testid='account-edit-{karte.Id}']")));

        cut.Find($"[data-testid='account-edit-{karte.Id}']").Click();

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='account-icon']")));
        Assert.Equal(AccountIcons.Card, Chosen(dialogs, "account-icon"));
    }

    // --- Kontoauswahl (Issue 16) -----------------------------------------------------------------------------

    private readonly AccountChoice giro = new(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true, Icon: AccountIcons.Bank);
    private readonly AccountChoice spar = new(Guid.NewGuid(), "Ein sehr langer Name für das Sparkonto von der Hausbank am Marktplatz", IsDefault: false, Active: true, Icon: AccountIcons.Piggy);
    private readonly AccountChoice alt = new(Guid.NewGuid(), "Altes Konto", IsDefault: false, Active: false, Icon: AccountIcons.Cash);

    private IRenderedComponent<AccountSelect> RenderSelect(Guid? value, Action<Guid?>? changed = null) =>
        Render<AccountSelect>(p => p
            .Add(x => x.Value, value)
            .Add(x => x.Accounts, [giro, spar, alt])
            .Add(x => x.EmptyText, "Standardkonto")
            .Add(x => x.Label, "Gezahlt von")
            .Add(x => x.TestId, "pick")
            .Add(x => x.ValueChanged, Microsoft.AspNetCore.Components.EventCallback.Factory.Create<Guid?>(this, id => changed?.Invoke(id))));

    // A1 (Issue 16): Symbol und Name
    [Fact]
    public void The_account_select_shows_icon_and_name_of_every_open_account_and_the_empty_choice()
    {
        var cut = RenderSelect(null);

        Assert.Equal(["Standardkonto", giro.Name, spar.Name], Options(cut, "pick"));
        var options = Group(cut, "pick").QuerySelectorAll("label.account-option").ToList();
        Assert.Null(IconOf(options[0]));
        Assert.Equal(AccountIcons.Bank, IconOf(options[1]));
        Assert.Equal(AccountIcons.Piggy, IconOf(options[2]));
        Assert.NotEmpty(options[1].QuerySelectorAll("svg"));
    }

    // A3 (Issue 16): Konten ohne Freigabe erscheinen wie bisher nicht (die Liste enthält sie nicht); aufgelöste nur, wenn gewählt.
    [Fact]
    public void A_closed_account_is_offered_only_while_it_is_the_chosen_one()
    {
        Assert.DoesNotContain(alt.Name, Options(RenderSelect(null), "pick"));
        Assert.Contains(alt.Name, Options(RenderSelect(alt.Id), "pick"));
    }

    [Fact]
    public void The_account_select_is_a_labelled_group_of_radio_buttons_with_a_shared_name_and_the_chosen_one_checked()
    {
        var cut = RenderSelect(spar.Id);

        var group = Group(cut, "pick");
        Assert.Equal("FIELDSET", group.TagName);
        Assert.Equal("Gezahlt von", group.QuerySelector("legend")!.TextContent.Trim());
        var radios = group.QuerySelectorAll("input[type='radio']").ToList();
        Assert.Equal(3, radios.Count);
        Assert.Single(radios.Select(r => r.GetAttribute("name")).Distinct());
        Assert.Equal(spar.Id.ToString(), Chosen(cut, "pick"));
    }

    [Fact]
    public void Choosing_an_option_reports_the_account_and_the_empty_choice_reports_null()
    {
        var reported = new List<Guid?>();
        var cut = RenderSelect(null, reported.Add);

        Group(cut, "pick").QuerySelector($"input[value='{giro.Id}']")!.Change(true);
        Group(cut, "pick").QuerySelector("input[value='']")!.Change(true);

        Assert.Equal(new Guid?[] { giro.Id, null }, reported);
    }

    // A4 (Issue 16): Lange Namen brechen um und werden nicht abgeschnitten.
    [Fact]
    public void A_long_name_is_written_in_full_so_that_it_can_wrap()
    {
        var cut = RenderSelect(null);

        Assert.Contains(spar.Name, Options(cut, "pick"));
        Assert.DoesNotContain("text-overflow", cut.Markup);
        Assert.Contains("account-option-name", cut.Markup);
    }

    private static string AppCss => File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "app.css"));

    [Fact]
    public void The_style_lets_the_name_break_and_marks_the_chosen_option_not_by_colour_alone_and_shows_the_focus()
    {
        var css = AppCss;
        var name = css[css.IndexOf(".account-option-name", StringComparison.Ordinal)..];
        name = name[..name.IndexOf('}', StringComparison.Ordinal)];

        Assert.Contains("overflow-wrap: anywhere", name);
        Assert.DoesNotContain("ellipsis", name);
        Assert.Contains(".account-option:has(input:checked)", css);
        Assert.Contains("font-weight", css[css.IndexOf(".account-option:has(input:checked)", StringComparison.Ordinal)..][..300]);
        Assert.Contains(".account-option:has(input:focus-visible)", css);
    }

    // A2 (Issue 16): gilt in Buchung, Vertrag und Plan-Posten
    [Fact]
    public async Task The_booking_dialog_offers_its_accounts_with_icons_and_remembers_the_choice()
    {
        Accounts.Accounts.Add(giro);
        Accounts.Accounts.Add(spar with { IsDefault = false });
        var cut = Render<Bookings>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='bookings-add']")));
        cut.Find("[data-testid='bookings-add']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='booking-account']")));

        Assert.Equal(giro.Id.ToString(), Chosen(dialogs, "booking-account"));
        Assert.Equal(AccountIcons.Piggy, IconOf(dialogs.Find($"[data-testid='booking-account'] input[value='{spar.Id}']").ParentElement!));

        dialogs.Find($"[data-testid='booking-account'] input[value='{spar.Id}']").Change(true);
        Assert.Equal(spar.Id.ToString(), Chosen(dialogs, "booking-account"));
        await Task.CompletedTask;
    }

    [Fact]
    public void The_transfer_dialog_offers_the_target_account_with_icons_too()
    {
        Accounts.Accounts.Add(giro);
        Accounts.Accounts.Add(spar with { IsDefault = false });
        var cut = Render<Bookings>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='bookings-add']")));
        cut.Find("[data-testid='bookings-add']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='booking-kind-transfer']")));

        dialogs.Find("[data-testid='booking-kind-transfer']").Click();

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='booking-counter-account']")));
        Assert.Equal(AccountIcons.Piggy, IconOf(dialogs.Find($"[data-testid='booking-counter-account'] input[value='{spar.Id}']").ParentElement!));
    }

    [Fact]
    public void The_plan_position_dialog_offers_the_paying_account_with_icons()
    {
        Accounts.Accounts.Add(giro);
        Accounts.Accounts.Add(spar with { IsDefault = false });
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10);
        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        OpenPlanTab(cut);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='plan-add']")));

        cut.Find("[data-testid='plan-add']").Click();

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='position-account']")));
        Assert.Equal(["Standardkonto", giro.Name, spar.Name], Options(dialogs, "position-account"));
        Assert.Equal(AccountIcons.Bank, IconOf(dialogs.Find($"[data-testid='position-account'] input[value='{giro.Id}']").ParentElement!));
    }

    [Fact]
    public void The_api_choices_carry_the_icon_of_the_account()
    {
        var karte = FakeAccountApi.Account("Karte") with { Icon = AccountIcons.Card };
        AccountApiFake.Accounts.Add(karte);
        var choices = new ApiAccountChoices(AccountApiFake);

        var result = choices.ListAsync().GetAwaiter().GetResult();

        Assert.Equal(AccountIcons.Card, Assert.Single(result.Value!).Icon);
    }
}
