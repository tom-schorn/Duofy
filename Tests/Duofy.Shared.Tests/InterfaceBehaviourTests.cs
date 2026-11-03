using Bunit;
using Duofy.Shared.Blocks;
using Duofy.Shared.Dialogs;
using Duofy.Shared.Layout;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Verhalten, das seine Tests beim Umbau (Spec 82) mit den alten Bausteinen verloren hat: Meldungen, Lizenzseite, Sprunglink,
/// Überschriften, Rücksprung des Fokus und der Wechsel des Geräts zwischen hell und dunkel.
/// </summary>
public class InterfaceBehaviourTests : PageTestContext
{
    public InterfaceBehaviourTests()
    {
        Services.AddSingleton<IAppHost, AppHost>();
        Services.AddSingleton<IThemePreferenceStore>(new MemoryThemeStore());
        Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true));
    }

    // ---- Meldungen ----

    [Fact]
    public void A_notice_without_a_message_shows_nothing()
    {
        var cut = Render<Notice>(p => p.Add(n => n.Message, null));

        Assert.Empty(cut.FindAll("[role='alert']"));
        Assert.Empty(cut.FindAll("[data-testid='notice']"));
    }

    // Jede Art ist eine eigene Meldung mit ihrem Text; Vorlese-Programme sagen sie an.
    [Theory]
    [InlineData(NoticeSeverity.Success)]
    [InlineData(NoticeSeverity.Error)]
    [InlineData(NoticeSeverity.Warning)]
    [InlineData(NoticeSeverity.Info)]
    public void A_notice_shows_its_text_as_an_alert(NoticeSeverity severity)
    {
        var cut = Render<Notice>(p => p.Add(n => n.Message, new NoticeMessage("Gespeichert", severity)));

        Assert.Equal("Gespeichert", cut.Find("[role='alert']").TextContent.Trim());
    }

    [Fact]
    public void The_kinds_of_a_notice_look_different()
    {
        string Classes(NoticeSeverity severity) =>
            Render<Notice>(p => p.Add(n => n.Message, new NoticeMessage("Text", severity))).Find("[role='alert']").ClassName!;

        var all = new[] { NoticeSeverity.Success, NoticeSeverity.Error, NoticeSeverity.Warning, NoticeSeverity.Info }.Select(Classes).ToList();

        Assert.Equal(all.Count, all.Distinct().Count());
    }

    // ---- Linientabelle ----

    // Jede Zeile hat Bezeichnung und Betrag; die Tabelle ist für Vorlese-Programme benannt.
    [Fact]
    public void A_line_table_shows_label_and_amount_per_row_and_is_named()
    {
        var cut = Render<LineTable>(p => p.Add(t => t.Caption, "Fällig").Add(t => t.Rows, [new LineTableRow("Miete", 890m), new LineTableRow("Strom", 80.5m)]));

        Assert.Equal("Fällig", cut.Find("caption").TextContent.Trim());
        var rows = cut.FindAll("tbody tr");
        Assert.Equal(2, rows.Count);
        Assert.Contains("Miete", rows[0].TextContent);
        Assert.Contains("890,00 €", rows[0].TextContent);
        Assert.Contains("80,50 €", rows[1].TextContent);
    }

    // Die Spalten für vorn und hinten gibt es nur, wenn eine Zeile sie braucht; der Nebentext steht unter der Bezeichnung.
    [Fact]
    public void A_line_table_row_may_carry_detail_and_trailing_content()
    {
        RenderFragment trailing = builder => builder.AddContent(0, "hinten");
        var plain = Render<LineTable>(p => p.Add(t => t.Caption, "Ohne").Add(t => t.Rows, [new LineTableRow("Miete", 890m)]));
        var full = Render<LineTable>(p => p.Add(t => t.Caption, "Mit").Add(t => t.Rows, [new LineTableRow("Miete", 890m, "am 2.", Trailing: trailing)]));

        Assert.Empty(plain.FindAll(".line-table-trail"));
        Assert.Equal("hinten", full.Find(".line-table-trail").TextContent.Trim());
        Assert.Equal("am 2.", full.Find(".line-table-detail").TextContent.Trim());
    }

    // ---- Lizenzseite ----

    // Die Seite nennt, was wirklich mitgeliefert wird, und ist ohne Anmeldung lesbar.
    [Theory]
    [InlineData("Blazor Blueprint", "Apache-2.0")]
    [InlineData("BlazorBlueprint.Icons.Lucide", "MIT")]
    [InlineData("Lucide", "ISC")]
    [InlineData("TailwindMerge.NET", "MIT")]
    [InlineData("Geist", "OFL-1.1")]
    public void The_licence_page_shows_the_notice_of_every_new_building_block(string name, string licence)
    {
        var cut = Render<Licenses>();

        var notice = cut.Find($"[data-testid='license-{name}']").TextContent;
        Assert.Contains(name, notice);
        Assert.Contains(licence, notice);
    }

    [Fact]
    public void The_licence_page_needs_no_sign_in() =>
        Assert.Empty(typeof(Licenses).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true));

    // ---- Rahmen ----

    // Sprunglink und sein Ziel stehen in App und Browser im selben Rahmen.
    [Fact]
    public void The_shell_has_the_skip_link_and_its_target()
    {
        var cut = Render<AppLayout>();

        var link = cut.Find(".skip-link");
        Assert.EndsWith("#" + SkipLink.TargetId, link.GetAttribute("href"));
        Assert.Single(cut.FindAll($"main#{SkipLink.TargetId}"));
    }

    // A13: Die Überschrift der Seite ist die einzige Hauptüberschrift, und die obere Leiste nennt keinen zweiten Titel.
    [Fact]
    public void A13_the_page_title_stands_once_and_the_top_bar_has_none()
    {
        Navigation.NavigateTo("/mehr");
        var cut = Render<Routes>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='more-accounts']")));
        Assert.Single(cut.FindAll("h1"));
        Assert.Empty(cut.FindAll("[data-testid='app-title']"));
        Assert.DoesNotContain("Mehr", cut.Find(".shell-top").TextContent);
    }

    // A13: Die Auswahl der Person sitzt im Profil-Menü unten in der Seitenleiste, nicht in der oberen Leiste.
    [Fact]
    public void A13_the_person_choice_sits_in_the_profile_menu_and_not_in_the_top_bar()
    {
        var cut = Render<MainLayout>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".shell-side [data-testid='user-menu-open']")));
        Assert.Empty(cut.FindAll(".shell-top [data-testid^='member-']"));
        cut.Find("[data-testid='user-menu-open']").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='member-choice-me']")));
    }

    // Ein Fenster-Dialog hat Titel und X; Schließen mit dem X heißt: nichts geändert.
    [Fact]
    public async Task Closing_a_window_dialog_with_the_x_counts_as_nothing_changed()
    {
        var host = RenderDialogHost();
        var forms = Services.GetRequiredService<FormDialogs>();
        Services.GetRequiredService<NavigationManager>();

        var opened = forms.OpenAsync<FormDialogDemo, bool>("Titel");
        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll(DialogSelector)));
        Assert.Equal("Titel", host.Find(DialogSelector + " h2").TextContent.Trim());

        host.FindAll(DialogSelector + " button").Last().Click();

        var outcome = await opened;
        Assert.False(outcome.Saved);
    }

    // Der Fehlerbalken ist im Rahmen, aber nur das Stylesheet zeigt ihn, und zwar erst im echten Fehlerfall (R3).
    [Fact]
    public void The_error_bar_is_hidden_until_blazor_shows_it()
    {
        var css = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "duofy.css"));
        var rule = css[css.IndexOf("#blazor-error-ui {", StringComparison.Ordinal)..];

        Assert.Contains("display: none", rule[..rule.IndexOf('}')]);
    }

    // ---- Pflichtfelder und Fokus der Dialoge (A12) ----

    private const string FocusModule = "./_content/Duofy.Shared/dialog-focus.js";

    // A12 (R9): Der Fokus geht beim Öffnen in den Dialog und beim Schließen zurück zum Auslöser, in dieser Reihenfolge.
    [Fact]
    public async Task A12_a_dialog_takes_the_focus_and_gives_it_back_to_the_trigger()
    {
        var focus = JSInterop.SetupModule(FocusModule);
        var host = RenderDialogHost();
        var forms = Services.GetRequiredService<FormDialogs>();

        var opened = forms.OpenStepAsync<StepDialogDemoDialog, string>("Vertrag anlegen",
            new FormParameters<StepDialogDemoDialog>().Add(d => d.Title, "Vertrag anlegen"));

        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll(".step-dialog")));
        Assert.Equal(["rememberTrigger", "focusFirstField"], focus.Invocations.Select(i => i.Identifier).ToArray());

        host.Find(".step-dialog-head").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        await opened;

        host.WaitForAssertion(() => Assert.Empty(host.FindAll(".step-dialog")));
        Assert.Equal("restoreTrigger", focus.Invocations.Last().Identifier);
    }

    // A12: Esc schließt auch einen einfachen Dialog und gibt den Fokus zurück.
    [Fact]
    public async Task A12_escape_closes_a_step_dialog_from_inside_its_fields()
    {
        var host = RenderDialogHost();
        var forms = Services.GetRequiredService<FormDialogs>();

        var opened = forms.OpenStepAsync<StepDialogDemoDialog, string>("Vertrag anlegen",
            new FormParameters<StepDialogDemoDialog>().Add(d => d.Title, "Vertrag anlegen"));
        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll(".step-dialog-steps")));

        host.Find(".step-dialog-steps").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        var outcome = await opened;
        Assert.False(outcome.Saved);
    }

    // Pflichtfeld: Weiter bleibt im Schritt, und der Fehler steht am Feld, angesagt und mit dem Feld verbunden.
    [Fact]
    public void A_required_field_stops_next_and_names_its_error_under_the_field()
    {
        var host = RenderDialogHost();
        var forms = Services.GetRequiredService<FormDialogs>();
        _ = forms.OpenStepAsync<StepDialogDemoDialog, string>("Vertrag anlegen",
            new FormParameters<StepDialogDemoDialog>().Add(d => d.Title, "Vertrag anlegen"));
        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll(".step-dialog-actions")));
        Assert.Empty(host.FindAll(".step-field-error"));

        host.FindAll(".step-dialog-actions button").Single(b => b.TextContent.Trim() == "Weiter").Click();

        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll(".step-field-error")));
        var error = host.Find(".step-field-error");
        Assert.Equal("alert", error.GetAttribute("role"));
        Assert.Equal("demo-name-input-error", error.Id);
        Assert.Equal("demo-name-input-error", host.Find("#demo-name-input").GetAttribute("aria-describedby"));
        Assert.Contains("Schritt 1", host.Find("[data-testid='step-dialog-progress']").TextContent);
    }

    // Die Beschriftung jedes Felds zeigt auf ihr Feld; Vorlese-Programme nennen es so beim Namen.
    [Theory]
    [InlineData("commitment-amount-input")]
    public void A_money_field_in_a_dialog_is_named_by_its_label(string inputId)
    {
        var host = RenderDialogHost();
        var forms = Services.GetRequiredService<FormDialogs>();
        _ = forms.OpenStepAsync<CommitmentDialog, bool>("Vertrag anlegen",
            new FormParameters<CommitmentDialog>().Add(d => d.Title, "Vertrag anlegen"));

        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll($"#{inputId}")));

        Assert.False(string.IsNullOrWhiteSpace(host.Find($"label[for='{inputId}']").TextContent));
    }

    // ---- Das Gerät wechselt zwischen hell und dunkel ----

    private IRenderedComponent<MainLayout> OpenLayout()
    {
        var cut = Render<MainLayout>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='theme-toggle']")));
        return cut;
    }

    private static bool IsDark(IRenderedComponent<MainLayout> cut) => cut.FindComponent<ThemeVariables>().Instance.Dark;

    // Ohne Wahl gilt das Gerät, und die Anzeige folgt ihm, ohne neu zu laden.
    [Fact]
    public async Task With_device_the_display_follows_a_change_of_the_device()
    {
        var cut = OpenLayout();
        var variables = cut.FindComponent<ThemeVariables>().Instance;

        await cut.InvokeAsync(() => variables.OnDeviceDarkChanged(true));
        cut.WaitForAssertion(() => Assert.True(IsDark(cut)));

        await cut.InvokeAsync(() => variables.OnDeviceDarkChanged(false));
        cut.WaitForAssertion(() => Assert.False(IsDark(cut)));
    }

    // Gegenprobe: Nach einer Wahl mit dem Knopf (einmal: dunkel, zweimal: wieder hell) ändert das Gerät nichts.
    [Theory]
    [InlineData(2, false)]
    [InlineData(1, true)]
    public async Task An_explicit_choice_ignores_a_change_of_the_device(int clicks, bool dark)
    {
        var cut = OpenLayout();
        for (var i = 0; i < clicks; i++)
        {
            cut.Find("[data-testid='theme-toggle']").Click();
        }
        cut.WaitForAssertion(() => Assert.Equal(dark, IsDark(cut)));

        await cut.InvokeAsync(() => cut.FindComponent<ThemeVariables>().Instance.OnDeviceDarkChanged(!dark));

        Assert.Equal(dark, IsDark(cut));
    }

    private sealed class FormDialogDemo : ComponentBase;

    private sealed class MemoryThemeStore : IThemePreferenceStore
    {
        public Task<ThemeChoice> LoadAsync() => Task.FromResult(ThemeChoice.Device);

        public Task SaveAsync(ThemeChoice choice) => Task.CompletedTask;
    }
}
