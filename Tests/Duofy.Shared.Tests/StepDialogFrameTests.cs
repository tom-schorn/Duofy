using AngleSharp.Dom;
using Bunit;
using Duofy.Shared.Blocks;
using Duofy.Shared.Dialogs;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Der Schritt-Dialog (Spec 11) geht über FormDialogs auf: Kopf mit Fortschritt,
/// feste Knöpfe, Eingaben beim Zurückgehen, Prüfung je Schritt und Rückfrage beim Abbrechen. Geprüft am
/// Beispieldialog mit drei Schritten.
/// </summary>
public class StepDialogFrameTests : UiTestContext
{
    private readonly IRenderedComponent<IComponent> host;
    private Task<DialogOutcome<string>>? opened;

    public StepDialogFrameTests()
    {
        host = RenderDialogHost();
    }

    private FormDialogs Forms => Services.GetRequiredService<FormDialogs>();

    private void Open()
    {
        opened = Forms.OpenStepAsync<StepDialogDemoDialog, string>("Vertrag anlegen",
            new FormParameters<StepDialogDemoDialog>().Add(d => d.Title, "Vertrag anlegen"));
        host.WaitForAssertion(() => Assert.NotNull(host.Find("[data-testid='step-dialog-progress']")));
    }

    private string Progress => host.Find("[data-testid='step-dialog-progress']").TextContent.Trim();

    private IElement Button(string text) => host.FindAll("button").Single(b => b.TextContent.Trim() == text);

    private IElement Input(string testId)
    {
        var element = host.Find($"[data-testid='{testId}']");
        return element.TagName == "INPUT" ? element : element.QuerySelector("input")!;
    }

    private void Type(string testId, string value)
    {
        Input(testId).Input(value);
        Input(testId).Change(value);
    }

    private IElement Step(int number) => host.FindAll("[data-testid^='step-dialog-step-']")[number - 1];

    private void GoToStepTwo()
    {
        Type("demo-name", "Miete");
        Button("Weiter").Click();
        host.WaitForAssertion(() => Assert.StartsWith("Schritt 2 von 3", Progress));
    }

    // Mangel S4: Das Dialogfenster hat einen Namen, die Überschrift des Dialogs, auch ohne Titelzeile von Blueprint.
    [Fact]
    public void The_dialog_window_is_named_by_the_heading_of_the_step_dialog()
    {
        Open();

        var dialog = host.Find(DialogSelector);
        var labelledBy = dialog.GetAttribute("aria-labelledby");
        var name = labelledBy is null ? dialog.GetAttribute("aria-label") : host.Find("#" + labelledBy).TextContent.Trim();
        Assert.Equal("Vertrag anlegen", name);
        Assert.Equal("dialog", dialog.GetAttribute("role"));
    }

    [Fact]
    public void The_dialog_draws_its_own_head_with_title_progress_and_cancel()
    {
        Open();

        Assert.Equal("Vertrag anlegen", host.Find(".step-dialog-title").TextContent.Trim());
        Assert.Equal("Schritt 1 von 3: Name", Progress);
        Assert.Equal("polite", host.Find("[data-testid='step-dialog-progress']").GetAttribute("aria-live"));
        Assert.Equal(3, host.FindAll(".step-dialog-bar-segment").Count);
        Assert.Single(host.FindAll(".step-dialog-bar-segment--reached"));
        Assert.NotNull(host.Find("[data-testid='step-dialog-cancel']"));
    }

    [Fact]
    public void Second_of_three_steps_shows_back_left_and_next_right()
    {
        Open();
        GoToStepTwo();

        Assert.Equal("Schritt 2 von 3: Betrag", Progress);
        var actions = host.Find(".step-dialog-actions").Children;
        Assert.Equal(2, actions.Length);
        Assert.Equal("Zurück", actions[0].TextContent.Trim());
        Assert.Equal("Weiter", actions[1].TextContent.Trim());
        Assert.Equal(2, host.FindAll(".step-dialog-bar-segment--reached").Count);
    }

    [Fact]
    public void First_step_keeps_the_place_of_back_free_so_next_stays_right()
    {
        Open();

        var actions = host.Find(".step-dialog-actions").Children;
        Assert.Contains("step-dialog-actions-spacer", actions[0].ClassList);
        Assert.Equal("Weiter", actions[1].TextContent.Trim());
    }

    [Fact]
    public async Task Last_step_finishes_with_save_and_returns_the_result()
    {
        Open();
        GoToStepTwo();
        Type("demo-amount", "62,40");
        Button("Weiter").Click();
        host.WaitForAssertion(() => Assert.Equal("Schritt 3 von 3: Prüfen", Progress));
        Assert.Contains("Miete", Step(3).TextContent);

        Button("Speichern").Click();

        var outcome = await opened!;
        Assert.True(outcome.Saved);
        Assert.Equal("Miete", outcome.Value);
    }

    [Fact]
    public void Values_stay_when_going_back_and_forward()
    {
        Open();
        GoToStepTwo();
        Type("demo-amount", "62,40");

        Button("Zurück").Click();
        host.WaitForAssertion(() => Assert.StartsWith("Schritt 1 von 3", Progress));
        Assert.Equal("Miete", Input("demo-name").GetAttribute("value"));
        Assert.False(Step(1).HasAttribute("hidden"));
        Assert.True(Step(2).HasAttribute("hidden"));

        Button("Weiter").Click();
        host.WaitForAssertion(() => Assert.StartsWith("Schritt 2 von 3", Progress));
        Assert.Equal("62,40", Input("demo-amount").GetAttribute("value"));
    }

    [Fact]
    public void Empty_required_field_keeps_the_step_and_shows_an_alert_at_the_field()
    {
        Open();

        Button("Weiter").Click();

        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll("[role='alert']")));
        Assert.StartsWith("Schritt 1 von 3", Progress);
        var alert = host.Find("[data-testid='demo-name-field'] [role='alert']");
        Assert.Equal("Bitte ausfüllen.", alert.TextContent.Trim());
        Assert.Equal("demo-name-input-error", alert.Id);
    }

    [Fact]
    public async Task Cancel_without_input_closes_at_once_as_nothing_changed()
    {
        Open();

        Button("Abbrechen").Click();

        var outcome = await opened!;
        Assert.False(outcome.Saved);
        Assert.DoesNotContain("Eingaben verwerfen?", host.Markup);
    }

    [Fact]
    public void Cancel_with_input_asks_first_and_keeping_on_leaves_the_dialog_open()
    {
        Open();
        Type("demo-name", "Miete");

        Button("Abbrechen").Click();
        host.WaitForAssertion(() => Assert.Contains("Eingaben verwerfen?", host.Markup));
        Assert.False(opened!.IsCompleted);

        Button("Weiter bearbeiten").Click();

        host.WaitForAssertion(() => Assert.DoesNotContain("Eingaben verwerfen?", host.Markup));
        Assert.False(opened.IsCompleted);
        Assert.Equal("Miete", Input("demo-name").GetAttribute("value"));
    }

    [Fact]
    public async Task Discarding_in_the_question_closes_the_dialog_as_nothing_changed()
    {
        Open();
        Type("demo-name", "Miete");
        Button("Abbrechen").Click();
        host.WaitForAssertion(() => Assert.Contains("Eingaben verwerfen?", host.Markup));

        Button("Verwerfen").Click();

        Assert.False((await opened!).Saved);
    }
}

public class DuofyButtonKindTests : UiTestContext
{
    [Theory]
    [InlineData(ButtonKind.Primary, "duofy-button--primary")]
    [InlineData(ButtonKind.Secondary, "duofy-button--secondary")]
    [InlineData(ButtonKind.Danger, "duofy-button--danger")]
    public void The_button_keeps_its_kind_classes_testid_and_label(ButtonKind kind, string kindClass)
    {
        var cut = Render<DuofyButton>(p => p
            .Add(c => c.Kind, kind)
            .Add(c => c.TestId, "mein-knopf")
            .Add(c => c.AriaLabel, "Mein Knopf")
            .AddChildContent("Speichern"));

        var button = cut.Find("button");
        Assert.Contains("duofy-button", button.ClassList);
        Assert.Contains(kindClass, button.ClassList);
        Assert.Equal("mein-knopf", button.GetAttribute("data-testid"));
        Assert.Equal("Mein Knopf", button.GetAttribute("aria-label"));
        Assert.Contains("Speichern", button.TextContent);
    }

    [Fact]
    public void A_disabled_button_is_disabled_and_an_enabled_one_clicks()
    {
        var clicks = 0;
        var enabled = Render<DuofyButton>(p => p
            .Add(c => c.OnClick, EventCallback.Factory.Create<MouseEventArgs>(this, () => clicks++))
            .AddChildContent("Ja"));
        var disabled = Render<DuofyButton>(p => p
            .Add(c => c.Disabled, true)
            .AddChildContent("Nein"));

        enabled.Find("button").Click();

        Assert.Equal(1, clicks);
        Assert.True(disabled.Find("button").HasAttribute("disabled"));
    }
}
