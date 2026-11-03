using Bunit;
using Duofy.Shared.Dialogs;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// FormDialogs ist der eine Ort, der entscheidet, wie ein Dialog aufgeht, und bietet OpenAsync und ConfirmAsync. Seiten kennen
/// die Optionen von Blueprint nicht. Der Fokus im echten Browser ist in der Abnahme von Spec 82 belegt (A12).
/// </summary>
public class FormDialogsTests : PageTestContext
{
    private FormDialogs Forms => Services.GetRequiredService<FormDialogs>();

    // --- Wie der Dialog aufgeht -----------------------------------------------

    [Fact]
    public async Task A_dialog_is_a_window_with_title_and_close_button()
    {
        var host = RenderDialogHost();

        var opened = Forms.OpenAsync<ProbeDialog, string>("Mein Titel", new FormParameters<ProbeDialog>().Add(d => d.Label, "Hallo"));
        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll("[data-testid='probe-dialog']")));

        Assert.Contains("Mein Titel", host.Find(DialogSelector).TextContent);
        Assert.StartsWith("Hallo", host.Find("[data-testid='probe-dialog']").TextContent);

        host.Find("[data-testid='probe-cancel']").Click();
        await opened;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Every_dialog_remembers_the_trigger_and_asks_the_browser_to_focus_its_first_field(bool asStep)
    {
        var module = JSInterop.SetupModule("./_content/Duofy.Shared/dialog-focus.js");
        module.Mode = JSRuntimeMode.Loose;
        var host = RenderDialogHost();

        var opened = asStep
            ? Forms.OpenStepAsync<ProbeDialog, string>("Titel")
            : Forms.OpenAsync<ProbeDialog, string>("Titel");
        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll("[data-testid='probe-cancel']")));

        module.VerifyInvoke("rememberTrigger");
        module.VerifyInvoke("focusFirstField");

        host.Find("[data-testid='probe-cancel']").Click();
        await opened;
        module.VerifyInvoke("restoreTrigger");
    }

    // --- Was zurückkommt --------------------------------------------------------

    [Fact]
    public async Task Cancelling_returns_nothing_changed()
    {
        var host = RenderDialogHost();
        var opened = Forms.OpenAsync<ProbeDialog, string>("Titel");
        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll("[data-testid='probe-cancel']")));

        host.Find("[data-testid='probe-cancel']").Click();
        var outcome = await opened;

        Assert.False(outcome.Saved);
        Assert.Null(outcome.Value);
        host.WaitForAssertion(() => Assert.Empty(host.FindAll("[data-testid='probe-dialog']")));
    }

    [Fact]
    public async Task Saving_returns_the_result_to_the_caller()
    {
        var host = RenderDialogHost();
        var opened = Forms.OpenAsync<ProbeDialog, string>("Titel");
        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll("[data-testid='probe-save']")));

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
        var host = RenderDialogHost();

        var asked = Forms.ConfirmAsync("Wirklich löschen?", "Buchung löschen", "Ja, löschen", "Nein, behalten");
        host.WaitForAssertion(() => Assert.Contains("Wirklich löschen?", host.Markup));

        module.VerifyInvoke("focusSafeButton");
        Assert.DoesNotContain(module.Invocations, i => i.Identifier == "focusFirstField");
        host.FindAll("button").Single(b => b.TextContent.Trim() == "Nein, behalten").Click();
        await asked;
    }

    // --- Bestätigen ---------------------------------------------------------------

    [Fact]
    public async Task Confirm_shows_the_given_texts_and_returns_true_for_yes()
    {
        var host = RenderDialogHost();

        var asked = Forms.ConfirmAsync("Wirklich löschen?", "Buchung löschen", "Ja, löschen", "Nein, behalten");
        host.WaitForAssertion(() => Assert.Contains("Wirklich löschen?", host.Markup));

        Assert.Contains("Buchung löschen", host.Markup);
        var buttons = host.FindAll("button").Select(b => b.TextContent.Trim()).ToList();
        Assert.Contains("Ja, löschen", buttons);
        Assert.Contains("Nein, behalten", buttons);

        host.FindAll("button").Single(b => b.TextContent.Trim() == "Ja, löschen").Click();

        Assert.True(await asked);
    }

    [Fact]
    public async Task Confirm_returns_false_for_no()
    {
        var host = RenderDialogHost();
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
    private sealed class ProbeDialog : FormDialogBase
    {
        [Parameter] public string Label { get; set; } = "";

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "data-testid", "probe-dialog");
            builder.AddContent(2, Label);
            builder.OpenElement(3, "button");
            builder.AddAttribute(4, "data-testid", "probe-cancel");
            builder.AddAttribute(5, "onclick", EventCallback.Factory.Create(this, () => CancelAsync()));
            builder.AddContent(6, "Abbrechen");
            builder.CloseElement();
            builder.OpenElement(7, "button");
            builder.AddAttribute(8, "data-testid", "probe-save");
            builder.AddAttribute(9, "onclick", EventCallback.Factory.Create(this, () => CloseAsync("gespeichert")));
            builder.AddContent(10, "Speichern");
            builder.CloseElement();
            builder.CloseElement();
        }
    }
}
