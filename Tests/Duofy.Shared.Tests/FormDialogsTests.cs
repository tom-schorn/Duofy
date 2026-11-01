using Bunit;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Radzen;
using Radzen.Blazor;

namespace Duofy.Shared.Tests;

/// <summary>
/// Radzen-Umstellung R5: FormDialogs ist der eine Ort, der entscheidet, wie ein Dialog aufgeht, und bietet
/// OpenAsync und ConfirmAsync. Seiten kennen die DialogOptions nicht.
/// </summary>
public class FormDialogsTests : PageTestContext
{
    private FormDialogs Forms => Services.GetRequiredService<FormDialogs>();

    private IRenderedComponent<RadzenComponents> OpenHost() => Render<RadzenComponents>();

    private void RunAsApp() => Services.AddSingleton<IAppHost, AppHost>();

    // --- Wie der Dialog aufgeht -----------------------------------------------

    [Fact]
    public async Task In_the_browser_the_dialog_is_a_window_in_the_middle_with_title_and_close_button()
    {
        var host = OpenHost();

        var opened = Forms.OpenAsync<ProbeDialog, string>("Mein Titel", new FormParameters<ProbeDialog>().Add(d => d.Label, "Hallo"));
        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll("[data-testid='probe-dialog']")));

        Assert.Contains("Mein Titel", host.Find(".rz-dialog-titlebar").TextContent);
        Assert.NotEmpty(host.FindAll(".rz-dialog-titlebar .rz-dialog-titlebar-close"));
        Assert.StartsWith("Hallo", host.Find("[data-testid='probe-dialog']").TextContent);
        Assert.Contains("width: 600px", host.Find(".rz-dialog").GetAttribute("style") ?? "");

        host.Find("[data-testid='probe-cancel']").Click();
        await opened;
    }

    [Fact]
    public async Task In_the_app_the_dialog_fills_the_screen_and_draws_no_title_of_its_own()
    {
        RunAsApp();
        var host = OpenHost();

        var opened = Forms.OpenAsync<ProbeDialog, string>("Mein Titel");
        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll("[data-testid='probe-dialog']")));

        Assert.Empty(host.FindAll(".rz-dialog-titlebar"));
        var style = host.Find(".rz-dialog").GetAttribute("style") ?? "";
        Assert.StartsWith("width: 100%", style);
        Assert.Contains("height: 100%", style);

        host.Find("[data-testid='probe-cancel']").Click();
        await opened;
    }

    [Fact]
    public void Esc_closes_the_dialog_and_radzen_does_not_pick_the_focus_itself()
    {
        var options = Forms.RadzenOptions();

        Assert.True(options.CloseDialogOnEsc);
        // Radzen fokussiert das erste fokussierbare Element, oft einen Knopf der Kopfzeile; FormDialogs setzt den Fokus selbst.
        Assert.False(options.AutoFocusFirstElement);
        Assert.False(Forms.StepOptions().AutoFocusFirstElement);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Every_dialog_asks_the_browser_to_focus_its_first_field_after_opening(bool asStep)
    {
        var module = JSInterop.SetupModule("./_content/Duofy.Shared/dialog-focus.js");
        module.Mode = JSRuntimeMode.Loose;
        var host = OpenHost();

        var opened = asStep
            ? Forms.OpenStepAsync<ProbeDialog, string>("Titel")
            : Forms.OpenAsync<ProbeDialog, string>("Titel");
        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll("[data-testid='probe-cancel']")));

        module.VerifyInvoke("countDialogs");
        module.VerifyInvoke("focusFirstField");

        host.Find("[data-testid='probe-cancel']").Click();
        await opened;
    }

    [Fact]
    public void In_the_app_a_click_next_to_the_form_does_not_close_it_by_accident()
    {
        RunAsApp();

        Assert.False(Forms.RadzenOptions().CloseDialogOnOverlayClick);
    }

    // --- Was zurückkommt --------------------------------------------------------

    [Fact]
    public async Task Cancelling_returns_nothing_changed()
    {
        var host = OpenHost();
        var opened = Forms.OpenAsync<ProbeDialog, string>("Titel");
        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll("[data-testid='probe-cancel']")));

        host.Find("[data-testid='probe-cancel']").Click();
        var outcome = await opened;

        Assert.False(outcome.Saved);
        Assert.Null(outcome.Value);
        Assert.Empty(host.FindAll("[data-testid='probe-dialog']"));
    }

    [Fact]
    public async Task Saving_returns_the_result_to_the_caller()
    {
        var host = OpenHost();
        var opened = Forms.OpenAsync<ProbeDialog, string>("Titel");
        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll("[data-testid='probe-save']")));

        host.Find("[data-testid='probe-save']").Click();
        var outcome = await opened;

        Assert.True(outcome.Saved);
        Assert.Equal("gespeichert", outcome.Value);
    }

    [Fact]
    public async Task Closing_with_the_x_counts_as_nothing_changed()
    {
        var host = OpenHost();
        var opened = Forms.OpenAsync<ProbeDialog, string>("Titel");
        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll(".rz-dialog-titlebar-close")));

        host.Find(".rz-dialog-titlebar-close").Click();
        var outcome = await opened;

        Assert.False(outcome.Saved);
    }

    // Kleine Dialoge (zum Beispiel eine Einladung mit QR-Code) sind auch in der App ein Fenster, kein Vollbild.
    [Fact]
    public async Task A_window_dialog_stays_a_window_in_the_app_and_has_a_title_and_close_button()
    {
        RunAsApp();
        var host = OpenHost();

        var opened = Forms.OpenWindowAsync<ProbeDialog, string>("Mein Fenster");
        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll("[data-testid='probe-dialog']")));

        Assert.Contains("Mein Fenster", host.Find(".rz-dialog-titlebar").TextContent);
        Assert.NotEmpty(host.FindAll(".rz-dialog-titlebar .rz-dialog-titlebar-close"));
        Assert.Contains("width: 600px", host.Find(".rz-dialog").GetAttribute("style") ?? "");

        host.Find("[data-testid='probe-save']").Click();
        var outcome = await opened;
        Assert.True(outcome.Saved);
        Assert.Equal("gespeichert", outcome.Value);
    }

    // Mangel S7: Eine Rückfrage beginnt auf dem sicheren Knopf, dem Nein; Enter löscht sonst aus Versehen.
    [Fact]
    public async Task A_confirmation_starts_on_the_safe_button_not_on_yes()
    {
        var module = JSInterop.SetupModule("./_content/Duofy.Shared/dialog-focus.js");
        module.Mode = JSRuntimeMode.Loose;
        var host = OpenHost();

        var asked = Forms.ConfirmAsync("Wirklich löschen?", "Buchung löschen", "Ja, löschen", "Nein, behalten");
        host.WaitForAssertion(() => Assert.Contains("Wirklich löschen?", host.Markup));

        module.VerifyInvoke("focusSafeButton");
        Assert.Empty(module.Invocations.Where(i => i.Identifier == "focusFirstField"));
        host.FindAll("button").Single(b => b.TextContent.Trim() == "Nein, behalten").Click();
        await asked;
    }

    // --- Bestätigen ---------------------------------------------------------------

    [Fact]
    public async Task Confirm_shows_the_given_texts_and_returns_true_for_yes()
    {
        var host = OpenHost();

        var asked = Forms.ConfirmAsync("Wirklich löschen?", "Buchung löschen", "Ja, löschen", "Nein, behalten");
        host.WaitForAssertion(() => Assert.Contains("Wirklich löschen?", host.Markup));

        Assert.Contains("Buchung löschen", host.Find(".rz-dialog-titlebar").TextContent);
        var buttons = host.FindAll(".rz-dialog-content button, .rz-dialog button.rz-button").Select(b => b.TextContent.Trim()).ToList();
        Assert.Contains("Ja, löschen", buttons);
        Assert.Contains("Nein, behalten", buttons);

        host.FindAll("button").Single(b => b.TextContent.Trim() == "Ja, löschen").Click();

        Assert.True(await asked);
    }

    [Fact]
    public async Task Confirm_returns_false_for_no()
    {
        var host = OpenHost();
        var asked = Forms.ConfirmAsync("Wirklich löschen?", "Buchung löschen", "Ja, löschen", "Nein, behalten");
        host.WaitForAssertion(() => Assert.Contains("Wirklich löschen?", host.Markup));

        host.FindAll("button").Single(b => b.TextContent.Trim() == "Nein, behalten").Click();

        Assert.False(await asked);
    }

    // --- Parameter werden beim Übersetzen geprüft ------------------------------------

    [Fact]
    public void Typed_parameters_use_the_property_names_of_the_dialog()
    {
        var parameters = new FormParameters<ProbeDialog>().Add(d => d.Label, "x").ToDictionary();

        Assert.Equal("x", parameters[nameof(ProbeDialog.Label)]);
        Assert.Single(parameters);
    }

    /// <summary>Ein kleiner Dialog für die Tests: Abbrechen schließt ohne Ergebnis, Speichern gibt "gespeichert" zurück.</summary>
    private sealed class ProbeDialog : ComponentBase
    {
        [Inject] private DialogService Dialogs { get; set; } = default!;

        [Parameter] public string Label { get; set; } = "";

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "data-testid", "probe-dialog");
            builder.AddContent(2, Label);
            builder.OpenElement(3, "button");
            builder.AddAttribute(4, "data-testid", "probe-cancel");
            builder.AddAttribute(5, "onclick", EventCallback.Factory.Create(this, () => Dialogs.Close()));
            builder.AddContent(6, "Abbrechen");
            builder.CloseElement();
            builder.OpenElement(7, "button");
            builder.AddAttribute(8, "data-testid", "probe-save");
            builder.AddAttribute(9, "onclick", EventCallback.Factory.Create(this, () => Dialogs.Close("gespeichert")));
            builder.AddContent(10, "Speichern");
            builder.CloseElement();
            builder.CloseElement();
        }
    }
}
