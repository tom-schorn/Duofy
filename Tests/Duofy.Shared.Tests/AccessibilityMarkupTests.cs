using Bunit;
using Duofy.Shared.Dialogs;
using Duofy.Shared.Layout;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Barrierefreiheit im Aufbau der Seiten (Prüfung vom 01.11.2026): Überschriften, Überspringen-Link, Beschriftung der
/// Auswahlfelder, Fehlerbalken und Pflichtfelder im Schritt-Dialog.
/// </summary>
public class AccessibilityMarkupTests : UiTestContext
{
    public AccessibilityMarkupTests()
    {
        Services.AddSingleton<IThemePreferenceStore>(new MemoryThemeStore());
        AddAuthorization();
    }

    private static string Page(string name) => File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "Pages", name + ".razor"));

    private static string LayoutFile(string name) => File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "Layout", name + ".razor"));

    [Fact]
    public void The_top_bar_has_no_title_and_no_heading()
    {
        var shell = LayoutFile("AppShell");
        var top = shell[shell.IndexOf("<header class=\"shell-top\"", StringComparison.Ordinal)..shell.IndexOf("</header>", StringComparison.Ordinal)];

        Assert.DoesNotContain("app-title", shell);
        Assert.DoesNotContain("<h", top.Replace("<header", ""), StringComparison.Ordinal);
    }

    // S1: Der Überspringen-Link kommt vor Leiste und Seitenleiste; das Ziel kann den Fokus annehmen.
    [Fact]
    public void A_skip_link_comes_first_and_moves_the_focus_to_the_content()
    {
        var cut = Render<MainLayout>(p => p.Add(l => l.Body, "<p>Seite</p>"));

        var first = cut.FindAll("a, button")[0];
        Assert.Equal("skip-link", first.GetAttribute("data-testid"));
        Assert.Equal("Zum Inhalt springen", first.TextContent.Trim());
        Assert.EndsWith("#main-content", first.GetAttribute("href"));
        Assert.Equal("-1", cut.Find("#main-content").GetAttribute("tabindex"));

        first.Click();

        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "Blazor._internal.domWrapper.focus");
    }

    // M4: Die Beschriftung gehört zum Feld, auch ohne mitgegebene Id.
    [Fact]
    public void A_select_is_connected_to_its_label()
    {
        var cut = Render<NativeSelect>(p => p.Add(s => s.Label, "Konto"));

        var id = cut.Find("select").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(id));
        Assert.Equal(id, cut.Find("label").GetAttribute("for"));
        Assert.Equal("Konto", cut.Find("label").TextContent);
    }

    [Fact]
    public void A_select_keeps_a_given_id_and_has_no_label_without_a_text()
    {
        var cut = Render<NativeSelect>(p => p.AddUnmatched("id", "mein-feld"));

        Assert.Equal("mein-feld", cut.Find("select").GetAttribute("id"));
        Assert.Empty(cut.FindAll("label"));
    }

    // S12: Der Fehlerbalken wird angesagt, sein Schließen ist ein echter Knopf mit Namen.
    [Theory]
    [InlineData("AppShell")]
    [InlineData("AuthLayout")]
    public void The_error_bar_is_announced_and_closes_with_a_real_button(string layout)
    {
        var source = LayoutFile(layout);

        Assert.Matches("""<div id="blazor-error-ui"[^>]* role="alert""", source);
        Assert.Matches("""<button type="button" class="dismiss" aria-label="@L\["Layout_Dismiss"\]">""", source);
        Assert.DoesNotContain("<span class=\"dismiss\"", source);
    }

    [Fact]
    public void The_field_script_sets_the_attributes_on_the_field()
    {
        var script = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "step-field.js"));

        Assert.Contains("export function markField", script);
        Assert.Contains("aria-required", script);
        Assert.Contains("aria-invalid", script);
    }

    [Theory]
    [InlineData("AccountSelect")]
    [InlineData("CommitmentPaymentMethodSelect")]
    public void A_group_of_options_can_carry_required_and_invalid(string name) =>
        Assert.Matches("""<fieldset [^>]*role="radiogroup""", Page(name));

    private static bool Marks(JSRuntimeInvocation invocation, string id, bool required, bool invalid) =>
        invocation.Identifier == "markField"
        && Equals(invocation.Arguments[0], id)
        && Equals(invocation.Arguments[1], required)
        && Equals(invocation.Arguments[2], invalid);

    // S7: Die Knöpfe einer Zeile nennen ihre Zeile; sonst hört man zehnmal dasselbe "Ändern".
    [Theory]
    [InlineData("AccountBookings", """L["Bookings_Edit", RowName(booking)]""")]
    [InlineData("AccountBookings", """L["Bookings_Delete", RowName(booking)]""")]
    [InlineData("Accounts", """L["Accounts_Edit", account.Name]""")]
    [InlineData("Accounts", """L[account.IsDefault ? "Accounts_IsDefault" : "Accounts_MakeDefault", account.Name]""")]
    [InlineData("Commitments", """L["Commitments_Edit", commitment.Name]""")]
    [InlineData("Plan", """L[done ? "Plan_Untick" : "Plan_Tick", position.Label]""")]
    [InlineData("Plan", """L["Plan_Edit", position.Label]""")]
    [InlineData("PlanPositionDialog", """L["Plan_Delete", Existing?.Label ?? ""]""")]
    [InlineData("Savings", """L["Savings_EditGoal", goal.Name]""")]
    [InlineData("Savings", """L["Savings_DeleteGoal", goal.Name]""")]
    [InlineData("Savings", """L["Savings_EditDebt", debt.Name]""")]
    [InlineData("Savings", """L["Savings_DeleteDebt", debt.Name]""")]
    public void Row_buttons_name_their_row(string page, string usage) => Assert.Contains(usage, Page(page));

    [Theory]
    [InlineData("Bookings_Edit", "Buchung {0} ändern")]
    [InlineData("Bookings_Delete", "Buchung {0} löschen")]
    [InlineData("Accounts_Edit", "Konto {0} ändern")]
    [InlineData("Accounts_MakeDefault", "{0} zum Standardkonto machen")]
    [InlineData("Accounts_IsDefault", "{0} ist das Standardkonto: beim Buchen vorgewählt")]
    [InlineData("Commitments_Edit", "Vertrag {0} ändern")]
    [InlineData("Plan_Tick", "{0} abhaken")]
    [InlineData("Plan_Untick", "Haken bei {0} entfernen")]
    [InlineData("Plan_Edit", "Posten {0} bearbeiten")]
    [InlineData("Plan_Delete", "Posten {0} löschen")]
    [InlineData("Savings_EditGoal", "Sparziel {0} ändern")]
    [InlineData("Savings_DeleteGoal", "Sparziel {0} löschen")]
    [InlineData("Savings_EditDebt", "Eintrag {0} ändern")]
    [InlineData("Savings_DeleteDebt", "Eintrag {0} löschen")]
    public void Row_button_texts_have_a_place_for_the_row(string key, string text)
    {
        var resources = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "Resources", "SharedResources.resx"));

        Assert.Contains($"""<data name="{key}"><value>{text}</value></data>""", resources);
    }

    [Fact]
    public void A_booking_row_is_named_by_title_and_date() =>
        Assert.Matches(@"RowName\(BookingResponse booking\) => .*Title\(booking\).*booking\.Date", Page("AccountBookings"));

    private sealed class MemoryThemeStore : IThemePreferenceStore
    {
        public Task<ThemeChoice> LoadAsync() => Task.FromResult(ThemeChoice.Device);

        public Task SaveAsync(ThemeChoice choice) => Task.CompletedTask;
    }
}
