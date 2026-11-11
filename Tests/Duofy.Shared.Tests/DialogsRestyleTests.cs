using AngleSharp.Dom;
using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Dialogs;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Issue #21 und #22 (Spec 22): Buchung, Konto, Haushalt und Plan-Posten nutzen das Dialog-Gerüst (Spec 11).
/// Die Buchung hat zwei Schritte, die anderen drei einen; Escape bricht ab wie der Knopf (mit der Rückfrage bei
/// Eingaben); ein fehlendes Konto steht als Fehler am Feld. Die Seitentests der Dialoge liegen in den
/// Nachbardateien. Der echte Browser und das Handy sind damit nicht gesehen.
/// </summary>
public class DialogsRestyleTests : PageTestContext
{
    private readonly IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> dialogs;

    public DialogsRestyleTests()
    {
        dialogs = RenderDialogHost();
    }

    // Der Buchungsdialog geht über FormDialogs als Dialog in Schritten auf.
    private Task<DialogOutcome<bool>>? bookingOutcome;

    private void OpenBooking(IReadOnlyList<AccountChoice>? accounts = null)
    {
        bookingOutcome = Services.GetRequiredService<FormDialogs>().OpenStepAsync<BookingDialog, bool>("Neue Buchung",
            new FormParameters<BookingDialog>()
                .Add(x => x.Title, "Neue Buchung")
                .Add(x => x.Accounts, accounts ?? [new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true)])
                .Add(x => x.Categories, new List<CategoryResponse>())
                .Add(x => x.Today, new DateOnly(2026, 10, 21)));
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll(".step-dialog")));
    }

    // Der Kontodialog geht über FormDialogs als Dialog in Schritten auf.
    private Task<DialogOutcome<bool>>? accountOutcome;

    private void OpenAccount()
    {
        accountOutcome = Services.GetRequiredService<FormDialogs>().OpenStepAsync<AccountDialog, bool>("Neues Konto",
            new FormParameters<AccountDialog>().Add(x => x.Title, "Neues Konto").Add(x => x.Today, new DateOnly(2026, 10, 21)));
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll(".step-dialog")));
    }

    private Task OpenPositionAsync()
    {
        _ = Services.GetRequiredService<FormDialogs>().OpenStepAsync<PlanPositionDialog, bool>("Neuer Posten",
            new FormParameters<PlanPositionDialog>()
                .Add(x => x.Title, "Neuer Posten")
                .Add(x => x.Year, 2026)
                .Add(x => x.Month, 10)
                .Add(x => x.Categories, new List<CategoryResponse>()));
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll(".step-dialog")));
        return Task.CompletedTask;
    }

    private IElement Escape(string selector)
    {
        var element = dialogs.Find(selector);
        element.KeyDown(new KeyboardEventArgs { Key = "Escape" });
        return element;
    }

    // A1 (Spec 22): Jeder der vier Dialoge steht auf dem Dialog-Gerüst.
    [Fact]
    public async Task The_booking_dialog_has_four_steps_on_the_scaffold_and_a_fifth_for_the_split()
    {
        OpenBooking();

        // Spec 124: Was und wann, Konto, Kategorie und Posten, (Aufteilen, nur bei Bedarf), Prüfen und speichern.
        Assert.Equal(5, dialogs.FindAll("[data-testid^='step-dialog-step-']").Count);
        Assert.Equal("Schritt 1 von 4: Was und wann", dialogs.Find("[data-testid='step-dialog-progress']").TextContent.Trim());
        Assert.Equal("Weiter", dialogs.Find("[data-testid='booking-next']").TextContent.Trim());
    }

    [Fact]
    public async Task The_account_dialog_is_one_step_on_the_scaffold_without_progress()
    {
        OpenAccount();

        Assert.Single(dialogs.FindAll("[data-testid^='step-dialog-step-']"));
        Assert.Empty(dialogs.FindAll("[data-testid='step-dialog-progress']"));
        Assert.Equal("Speichern", dialogs.Find("[data-testid='account-save']").TextContent.Trim());
        Assert.NotEmpty(dialogs.FindAll("[data-testid='account-name']"));
    }

    [Fact]
    public async Task The_position_dialog_is_one_step_on_the_scaffold()
    {
        await OpenPositionAsync();

        Assert.Single(dialogs.FindAll("[data-testid^='step-dialog-step-']"));
        Assert.Equal("Speichern", dialogs.Find("[data-testid='position-save']").TextContent.Trim());
        Assert.NotEmpty(dialogs.FindAll("[data-testid='position-label']"));
    }

    // Alle Buchungsarten bleiben möglich, auch die Umbuchung mit Von Topf und Zu Topf (Schritt 3).
    [Fact]
    public async Task All_booking_kinds_stay_and_a_transfer_keeps_its_pots_in_step_three()
    {
        OpenBooking();

        Assert.Equal(["expense", "income", "transfer"], dialogs.FindAll("[data-testid^='booking-kind-']").Select(b => b.GetAttribute("data-testid")!["booking-kind-".Length..]).Order());

        dialogs.Find("[data-testid='booking-kind-transfer']").Click();

        var step3 = dialogs.Find("[data-testid='step-dialog-step-3']");
        Assert.NotNull(step3.QuerySelector("[data-testid='booking-transfer-source-pot']"));
        Assert.NotNull(step3.QuerySelector("[data-testid='booking-transfer-target-pot']"));
        Assert.NotNull(dialogs.Find("[data-testid='step-dialog-step-2']").QuerySelector("[data-testid='booking-counter-account']"));
    }

    // A4 (Spec 22): Ein fehlendes Konto steht als Fehler am Feld, und Weiter bleibt im ersten Schritt.
    [Fact]
    public async Task A_missing_account_is_an_error_at_the_field_and_next_stays_on_the_account_step()
    {
        OpenBooking([new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: false, Active: true)]);

        dialogs.Find("[data-testid='booking-next']").Click();
        dialogs.Find("[data-testid='booking-next']").Click();

        Assert.Equal("Bitte wähle ein Konto.", dialogs.Find("#booking-account-input-error").TextContent.Trim());
        Assert.Equal("alert", dialogs.Find("#booking-account-input-error").GetAttribute("role"));
        Assert.Equal("Schritt 2 von 4: Konto", dialogs.Find("[data-testid='step-dialog-progress']").TextContent.Trim());
    }

    // A4 (Spec 22): Escape schließt, solange nichts eingegeben ist ...
    [Fact]
    public async Task Escape_closes_a_dialog_without_input()
    {
        OpenAccount();

        dialogs.Find(".step-dialog-steps").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindAll(".step-dialog")));
        Assert.False((await accountOutcome!).Saved);
    }

    // ... und fragt wie der Knopf Abbrechen nach, sobald etwas eingegeben ist.
    [Fact]
    public async Task Escape_asks_before_discarding_input()
    {
        OpenAccount();
        dialogs.Find("[data-testid='account-name']").Input("Neuer Name");

        dialogs.Find(".step-dialog-steps").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        dialogs.WaitForAssertion(() => Assert.Contains("Eingaben verwerfen?", dialogs.Markup));
        Assert.NotEmpty(dialogs.FindAll(".step-dialog"));
    }

    // A2 (Spec 22): Der Inhalt rollt im Dialog, Kopf und Knöpfe bleiben stehen; auf dem Handy füllt das Gerüst den Bildschirm.
    [Fact]
    public void No_dialog_is_taller_than_the_screen_because_only_the_content_scrolls()
    {
        var css = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "duofy.css"));
        string Rule(string selector) { var from = css.IndexOf(selector + " {", StringComparison.Ordinal); return css[from..css.IndexOf('}', from)]; }

        Assert.Contains("overflow-y: auto;", Rule(".step-dialog-scroll"));
        Assert.Contains("min-height: 0;", Rule(".step-dialog-scroll"));
        Assert.Contains("max-height: calc(100dvh", Rule(".step-dialog"));
    }
}
