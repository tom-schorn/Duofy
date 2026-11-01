using AngleSharp.Dom;
using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Dialogs;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Duofy.Shared.Tests;

/// <summary>
/// Issue #21 und #22 (Spec 22): Buchung, Konto, Haushalt und Plan-Posten nutzen das Dialog-Gerüst (Spec 11).
/// Die Buchung hat zwei Schritte, die anderen drei einen; Escape bricht ab wie der Knopf (mit der Rückfrage bei
/// Eingaben); ein fehlendes Konto steht als Fehler am Feld. Die Seitentests der Dialoge liegen in den
/// Nachbardateien. Der echte Browser und das Handy sind damit nicht gesehen.
/// </summary>
public class DialogsRestyleTests : PageTestContext
{
    private readonly IRenderedComponent<MudDialogProvider> dialogs;
    private IDialogReference? reference;

    public DialogsRestyleTests()
    {
        Render<MudPopoverProvider>();
        dialogs = Render<MudDialogProvider>();
    }

    private async Task OpenAsync<T>(string title, DialogParameters<T> parameters) where T : Microsoft.AspNetCore.Components.IComponent
    {
        var service = Services.GetRequiredService<IDialogService>();
        reference = await dialogs.InvokeAsync(() => service.ShowAsync<T>(title, parameters, StepDialogOptions.Create()));
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll(".step-dialog")));
    }

    private Task OpenBookingAsync(IReadOnlyList<AccountChoice>? accounts = null) =>
        OpenAsync("Neue Buchung", new DialogParameters<BookingDialog>
        {
            { x => x.Accounts, accounts ?? [new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true)] },
            { x => x.Categories, new List<CategoryResponse>() },
            { x => x.Today, new DateOnly(2026, 10, 21) },
        });

    private Task OpenAccountAsync() =>
        OpenAsync("Neues Konto", new DialogParameters<AccountDialog> { { x => x.Today, new DateOnly(2026, 10, 21) } });

    private Task OpenHouseholdAsync() =>
        OpenAsync("Haushalt bearbeiten", new DialogParameters<HouseholdDialog>
        {
            { x => x.Household, new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20, [FakeHouseholdApi.Anna(isMe: true)]) },
        });

    private Task OpenPositionAsync() =>
        OpenAsync("Neuer Posten", new DialogParameters<PlanPositionDialog>
        {
            { x => x.Year, 2026 },
            { x => x.Month, 10 },
            { x => x.Categories, new List<CategoryResponse>() },
        });

    private IElement Escape(string selector)
    {
        var element = dialogs.Find(selector);
        element.KeyDown(new KeyboardEventArgs { Key = "Escape" });
        return element;
    }

    // A1 (Spec 22): Jeder der vier Dialoge steht auf dem Dialog-Gerüst.
    [Fact]
    public async Task The_booking_dialog_has_two_steps_on_the_scaffold()
    {
        await OpenBookingAsync();

        Assert.Equal(2, dialogs.FindAll("[data-testid^='step-dialog-step-']").Count);
        Assert.Equal("Schritt 1 von 2: Was und wann", dialogs.Find("[data-testid='step-dialog-progress']").TextContent.Trim());
        Assert.Equal("Weiter", dialogs.Find("[data-testid='booking-next']").TextContent.Trim());
    }

    [Fact]
    public async Task The_account_dialog_is_one_step_on_the_scaffold_without_progress()
    {
        await OpenAccountAsync();

        Assert.Single(dialogs.FindAll("[data-testid^='step-dialog-step-']"));
        Assert.Empty(dialogs.FindAll("[data-testid='step-dialog-progress']"));
        Assert.Equal("Speichern", dialogs.Find("[data-testid='account-save']").TextContent.Trim());
        Assert.NotEmpty(dialogs.FindAll("[data-testid='account-name']"));
    }

    [Fact]
    public async Task The_household_dialog_is_one_step_on_the_scaffold()
    {
        await OpenHouseholdAsync();

        Assert.Single(dialogs.FindAll("[data-testid^='step-dialog-step-']"));
        Assert.Equal("Speichern", dialogs.Find("[data-testid='household-dialog-save']").TextContent.Trim());
        Assert.Equal(3, dialogs.FindAll("[data-testid^='household-dialog-quota-']").Count);
    }

    [Fact]
    public async Task The_position_dialog_is_one_step_on_the_scaffold()
    {
        await OpenPositionAsync();

        Assert.Single(dialogs.FindAll("[data-testid^='step-dialog-step-']"));
        Assert.Equal("Speichern", dialogs.Find("[data-testid='position-save']").TextContent.Trim());
        Assert.NotEmpty(dialogs.FindAll("[data-testid='position-label']"));
    }

    // Alle Buchungsarten bleiben möglich, auch die Umbuchung mit Von Topf und Zu Topf (Schritt 2).
    [Fact]
    public async Task All_booking_kinds_stay_and_a_transfer_keeps_its_pots_in_step_two()
    {
        await OpenBookingAsync();

        Assert.Equal(["expense", "income", "transfer"], dialogs.FindAll("[data-testid^='booking-kind-']").Select(b => b.GetAttribute("data-testid")!["booking-kind-".Length..]).Order());

        dialogs.Find("[data-testid='booking-kind-transfer']").Click();

        var step2 = dialogs.Find("[data-testid='step-dialog-step-2']");
        Assert.NotNull(step2.QuerySelector("[data-testid='booking-transfer-source-pot']"));
        Assert.NotNull(step2.QuerySelector("[data-testid='booking-transfer-target-pot']"));
        Assert.NotNull(dialogs.Find("[data-testid='step-dialog-step-1']").QuerySelector("[data-testid='booking-counter-account']"));
    }

    // A4 (Spec 22): Ein fehlendes Konto steht als Fehler am Feld, und Weiter bleibt im ersten Schritt.
    [Fact]
    public async Task A_missing_account_is_an_error_at_the_field_and_next_stays_on_step_one()
    {
        await OpenBookingAsync([new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: false, Active: true)]);

        dialogs.Find("[data-testid='booking-next']").Click();

        Assert.Equal("Bitte wähle ein Konto.", dialogs.Find("#booking-account-input-error").TextContent.Trim());
        Assert.Equal("alert", dialogs.Find("#booking-account-input-error").GetAttribute("role"));
        Assert.Equal("Schritt 1 von 2: Was und wann", dialogs.Find("[data-testid='step-dialog-progress']").TextContent.Trim());
    }

    // A4 (Spec 22): Escape schließt, solange nichts eingegeben ist ...
    [Fact]
    public async Task Escape_closes_a_dialog_without_input()
    {
        await OpenAccountAsync();

        Escape(".step-dialog-steps");

        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindAll(".step-dialog")));
        Assert.True((await reference!.Result)!.Canceled);
    }

    // ... und fragt wie der Knopf Abbrechen nach, sobald etwas eingegeben ist.
    [Fact]
    public async Task Escape_asks_before_discarding_input()
    {
        await OpenHouseholdAsync();
        dialogs.Find("[data-testid='household-dialog-name']").Input("Neuer Name");

        Escape(".step-dialog-steps");

        dialogs.WaitForAssertion(() => Assert.Contains("Eingaben verwerfen?", dialogs.Markup));
        Assert.NotEmpty(dialogs.FindAll(".step-dialog"));
    }

    // A2 (Spec 22): Der Inhalt rollt im Dialog, Kopf und Knöpfe bleiben stehen; auf dem Handy füllt das Gerüst den Bildschirm.
    [Fact]
    public void No_dialog_is_taller_than_the_screen_because_only_the_content_scrolls()
    {
        var css = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "app.css")).Replace("\r\n", "\n");

        Assert.Contains(".step-dialog .mud-dialog-content {\n    overflow-y: auto;", css);
        Assert.Contains("overflow-y: hidden;", css);
        var start = css.IndexOf("@media (max-width: 599px) {\n    .mud-dialog-container .mud-dialog.step-dialog", StringComparison.Ordinal);
        Assert.True(start >= 0, "Regel für das Handy fehlt in app.css");
        Assert.Contains("height: 100%;", css[start..]);
    }
}
