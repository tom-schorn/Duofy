using Duofy.Shared.Services;
using AngleSharp.Dom;
using Bunit;
using Duofy.Shared.Blocks;
using Duofy.Shared.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 11 (Dialog-Gerüst mit Schritten und Fortschritt): Kopf, feste Knöpfe, Eingaben beim Zurückgehen,
/// Prüfung je Schritt und Rückfrage beim Abbrechen. Geprüft am Beispieldialog der Testseite mit drei Schritten
/// (Name Pflicht, Betrag Pflicht, Prüfen). Die Größe im Browser prüft diese Datei nur über das Stylesheet.
/// </summary>
public class StepDialogTests : UiTestContext
{
    private readonly IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> dialogs;
    private Task<DialogOutcome<string>>? opened;

    public StepDialogTests()
    {
        dialogs = RenderDialogHost();
    }

    private Task OpenAsync()
    {
        opened = Services.GetRequiredService<FormDialogs>().OpenStepAsync<StepDialogDemoDialog, string>("Vertrag anlegen",
            new FormParameters<StepDialogDemoDialog>().Add(d => d.Title, "Vertrag anlegen"));
        dialogs.WaitForAssertion(() => Assert.NotNull(dialogs.Find("[data-testid='step-dialog-progress']")));
        return Task.CompletedTask;
    }

    private string Progress => dialogs.Find("[data-testid='step-dialog-progress']").TextContent.Trim();

    private IElement Button(string text) => dialogs.FindAll("button").Single(b => b.TextContent.Trim() == text);

    private IElement Input(string testId)
    {
        var element = dialogs.Find($"[data-testid='{testId}']");
        return element.TagName == "INPUT" ? element : element.QuerySelector("input")!;
    }

    // Tippen wie im Browser: input bei jedem Zeichen, change beim Verlassen des Felds (dann übernimmt das Feld den Wert).
    private void Type(string testId, string value)
    {
        Input(testId).Input(value);
        Input(testId).Change(value);
    }

    private IElement Step(int number) => dialogs.FindAll("[data-testid^='step-dialog-step-']")[number - 1];

    private static string Css() => File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "duofy.css"));

    // Eine Regel ab dem Selektor bis zur schließenden Klammer; die Regeln des Dialogs sind nicht verschachtelt.
    private static string Rule(string css, string selector)
    {
        var start = css.IndexOf(selector + " {", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Regel '{selector}' fehlt in app.css");
        return css[start..css.IndexOf('}', start)];
    }

    // Der Inhalt des @media-Blocks mit dieser Bedingung, in dem der Selektor steht; app.css hat mehrere gleiche Bedingungen.
    private static string MediaBlock(string css, string query, string selector)
    {
        var head = "@media " + query + " {";
        for (var start = css.IndexOf(head, StringComparison.Ordinal); start >= 0; start = css.IndexOf(head, start + 1, StringComparison.Ordinal))
        {
            var block = MediaBlockAt(css, start, query);
            if (block.Contains(selector + " {", StringComparison.Ordinal)) return block;
        }

        Assert.Fail($"@media {query} mit '{selector}' fehlt in app.css");
        return "";
    }

    private static string MediaBlockAt(string css, int start, string query)
    {
        var open = css.IndexOf('{', start);
        var depth = 0;
        for (var i = open; i < css.Length; i++)
        {
            if (css[i] == '{') depth++;
            if (css[i] == '}' && --depth == 0) return css[(open + 1)..i];
        }
        throw new InvalidOperationException($"@media {query} ist nicht geschlossen");
    }

    private async Task GoToStepTwoAsync()
    {
        Type("demo-name", "Miete");
        Button("Weiter").Click();
        dialogs.WaitForAssertion(() => Assert.StartsWith("Schritt 2 von 3", Progress));
    }

    // A1: Im zweiten von drei Schritten steht "Schritt 2 von 3" im Kopf, Zurück links und Weiter rechts unten.
    [Fact]
    public async Task Second_of_three_steps_shows_its_position_and_back_left_next_right()
    {
        await OpenAsync();
        await GoToStepTwoAsync();

        Assert.Equal("Schritt 2 von 3: Betrag", Progress);
        Assert.NotNull(dialogs.Find("[data-testid='step-dialog-progress']"));

        var actions = dialogs.Find(".step-dialog-actions").Children;
        Assert.Equal(2, actions.Length);
        Assert.Equal("Zurück", actions[0].TextContent.Trim());
        Assert.Equal("Weiter", actions[1].TextContent.Trim());
        Assert.Contains("duofy-button", actions[1].ClassList);

        // Der Fortschritt ist auch als Balken zu sehen: zwei von drei Abschnitten sind erreicht.
        Assert.Equal(2, dialogs.FindAll(".step-dialog-bar-segment--reached").Count);
        Assert.Equal(3, dialogs.FindAll(".step-dialog-bar-segment").Count);

        var css = Css();
        Assert.Contains("justify-content: space-between", Rule(css, ".step-dialog-actions"));
    }

    // A1: Im ersten Schritt fehlt Zurück, sein Platz bleibt frei; Weiter steht trotzdem rechts.
    [Fact]
    public async Task First_step_keeps_the_place_of_back_free_so_next_stays_right()
    {
        await OpenAsync();

        Assert.Equal("Schritt 1 von 3: Name", Progress);
        var actions = dialogs.Find(".step-dialog-actions").Children;
        Assert.Equal(2, actions.Length);
        Assert.Contains("step-dialog-actions-spacer", actions[0].ClassList);
        Assert.Equal("Weiter", actions[1].TextContent.Trim());
    }

    // A1: Im letzten Schritt heißt Weiter "Speichern" und schließt den Dialog mit dem Ergebnis.
    [Fact]
    public async Task Last_step_finishes_with_save()
    {
        await OpenAsync();
        await GoToStepTwoAsync();
        Type("demo-amount", "62,40");
        Button("Weiter").Click();
        dialogs.WaitForAssertion(() => Assert.Equal("Schritt 3 von 3: Prüfen", Progress));
        Assert.Contains("Miete", Step(3).TextContent);

        Button("Speichern").Click();

        var result = await opened!;
        Assert.True(result.Saved);
        Assert.Equal("Miete", result.Value);
    }

    // A1: Der Kopf sagt Vorlese-Programmen jeden Schrittwechsel an.
    [Fact]
    public async Task Progress_text_is_announced()
    {
        await OpenAsync();

        Assert.Equal("polite", dialogs.Find("[data-testid='step-dialog-progress']").GetAttribute("aria-live"));
    }

    // A3: Ein Wert aus Schritt 1 steht nach Weiter und Zurück noch da, und der aus Schritt 2 auch.
    [Fact]
    public async Task Values_stay_when_going_back_and_forward()
    {
        await OpenAsync();
        await GoToStepTwoAsync();
        Type("demo-amount", "62,40");

        Button("Zurück").Click();
        dialogs.WaitForAssertion(() => Assert.StartsWith("Schritt 1 von 3", Progress));
        Assert.Equal("Miete", Input("demo-name").GetAttribute("value"));
        Assert.False(Step(1).HasAttribute("hidden"));
        Assert.True(Step(2).HasAttribute("hidden"));

        Button("Weiter").Click();
        dialogs.WaitForAssertion(() => Assert.StartsWith("Schritt 2 von 3", Progress));
        Assert.Equal("62,40", Input("demo-amount").GetAttribute("value"));
    }

    // A4: Ein leeres Pflichtfeld hält den Dialog in Schritt 1, der Fehler steht am Feld mit role alert.
    [Fact]
    public async Task Empty_required_field_keeps_the_step_and_shows_an_alert_at_the_field()
    {
        await OpenAsync();

        Button("Weiter").Click();

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[role='alert']")));
        Assert.StartsWith("Schritt 1 von 3", Progress);
        Assert.True(Step(2).HasAttribute("hidden"));

        var field = dialogs.Find("[data-testid='demo-name-field']");
        var alert = field.QuerySelector("[role='alert']");
        Assert.NotNull(alert);
        Assert.Equal("Bitte ausfüllen.", alert!.TextContent.Trim());
        Assert.Equal("demo-name-input-error", alert.Id);
    }

    // A4: Vor dem ersten Weiter zeigt niemand einen Fehler; ist das Feld gefüllt, verschwindet er wieder.
    [Fact]
    public async Task Error_appears_only_after_next_and_goes_once_the_field_is_filled()
    {
        await OpenAsync();
        Assert.Empty(dialogs.FindAll("[role='alert']"));

        Button("Weiter").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[role='alert']")));

        Type("demo-name", "Miete");

        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindAll("[role='alert']")));
    }

    // A4: Geprüft wird nur der aktuelle Schritt: Das leere Pflichtfeld in Schritt 2 hält Schritt 1 nicht auf.
    [Fact]
    public async Task Only_the_current_step_is_checked()
    {
        await OpenAsync();

        await GoToStepTwoAsync();

        Assert.Empty(dialogs.FindAll("[role='alert']"));
    }

    // A5: Mit Eingaben fragt Abbrechen nach; "Weiter bearbeiten" lässt den Dialog offen.
    [Fact]
    public async Task Cancel_with_input_asks_first_and_keeping_on_leaves_the_dialog_open()
    {
        await OpenAsync();
        Type("demo-name", "Miete");

        Button("Abbrechen").Click();

        dialogs.WaitForAssertion(() => Assert.Contains("Eingaben verwerfen?", dialogs.Markup));
        Assert.False(opened!.IsCompleted);

        Button("Weiter bearbeiten").Click();

        dialogs.WaitForAssertion(() => Assert.DoesNotContain("Eingaben verwerfen?", dialogs.Markup));
        Assert.False(opened.IsCompleted);
        Assert.Equal("Miete", Input("demo-name").GetAttribute("value"));
    }

    // A5: Wer in der Rückfrage verwirft, schließt den Dialog ohne Ergebnis.
    [Fact]
    public async Task Discarding_in_the_question_closes_the_dialog()
    {
        await OpenAsync();
        Type("demo-name", "Miete");
        Button("Abbrechen").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Eingaben verwerfen?", dialogs.Markup));

        Button("Verwerfen").Click();

        var result = await opened!;
        Assert.False(result.Saved);
    }

    // A5: Ohne Eingaben schließt Abbrechen sofort, ohne Rückfrage.
    [Fact]
    public async Task Cancel_without_input_closes_at_once()
    {
        await OpenAsync();

        Button("Abbrechen").Click();

        var result = await opened!;
        Assert.False(result.Saved);
        Assert.DoesNotContain("Eingaben verwerfen?", dialogs.Markup);
    }
}

/// <summary>Spec 11: Die Testseite des Schritt-Dialogs gibt es nur im Entwicklungsbetrieb.</summary>
public class StepDialogPageTests : UiTestContext
{
    [Fact]
    public void Test_page_opens_the_example_dialog_in_development()
    {
        Services.AddSingleton<IDevelopmentMode>(new DevelopmentMode(true));
        var dialogs = RenderDialogHost();
        var cut = Render<StepDialogDemo>();

        cut.Find("[data-testid='step-dialog-demo-open'] button").Click();

        dialogs.WaitForAssertion(() => Assert.Equal("Schritt 1 von 3: Name", dialogs.Find("[data-testid='step-dialog-progress']").TextContent.Trim()));
    }

    [Fact]
    public void Test_page_shows_nothing_unless_development_is_announced()
    {
        var cut = Render<StepDialogDemo>();

        Assert.Empty(cut.FindAll("[data-testid='step-dialog-demo-open']"));
    }

    [Fact]
    public void Test_page_shows_nothing_in_production()
    {
        Services.AddSingleton<IDevelopmentMode>(new DevelopmentMode(false));
        var cut = Render<StepDialogDemo>();

        Assert.Empty(cut.FindAll("[data-testid='step-dialog-demo-open']"));
    }
}
