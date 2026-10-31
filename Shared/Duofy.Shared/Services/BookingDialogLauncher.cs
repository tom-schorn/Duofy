using Duofy.ApiClient;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Duofy.Shared.Resources;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace Duofy.Shared.Services;

/// <summary>
/// Öffnet den Buchungsdialog. Die Buchungsseite und die Startseite (Plus-Knopf in der App)
/// brauchen dasselbe; hier steht es einmal. Hat die Seite Konten und Kategorien schon geladen,
/// reicht sie sie mit, sonst holt der Dienst sie selbst.
/// </summary>
public sealed class BookingDialogLauncher(
    IDialogService dialogs,
    FormDialogs forms,
    IAccountChoices accountChoices,
    ICategoryApi categoryApi,
    ActiveMember member,
    TimeProvider time,
    IStringLocalizer<SharedResources> l)
{
    /// <returns>
    /// null, wenn die Person abgebrochen hat; eine Erfolgsmeldung nach dem Speichern;
    /// eine Fehlermeldung, wenn Konten oder Kategorien nicht zu laden waren.
    /// </returns>
    public async Task<NoticeMessage?> OpenAsync(
        BookingResponse? existing = null,
        IReadOnlyList<AccountChoice>? accounts = null,
        List<CategoryResponse>? categories = null,
        DateOnly? planMonth = null)
    {
        if (accounts is null)
        {
            // Die Konten der Person, deren Zahlen gerade offen sind (Spec 68).
            var accountResult = await accountChoices.ListAsync(member.Owner);
            if (!accountResult.Succeeded)
            {
                return new NoticeMessage(ErrorTexts.For(l, accountResult.ErrorCode), Severity.Error);
            }
            accounts = accountResult.Value!;
        }

        // Ohne offenes Konto gibt es nichts Neues zu buchen.
        if (existing is null && !accounts.Any(a => a.Active))
        {
            return new NoticeMessage(l["Bookings_NoAccounts"], Severity.Info);
        }

        if (categories is null)
        {
            var categoryResult = await categoryApi.ListAsync(member.Owner);
            if (!categoryResult.Succeeded)
            {
                return new NoticeMessage(ErrorTexts.For(l, categoryResult.ErrorCode), Severity.Error);
            }
            categories = categoryResult.Value!.ToList();
        }

        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);

        // Die Parameter des Dialogs; die Schreibweise mit x => x.Name prüft schon beim Übersetzen, ob es sie gibt.
        var parameters = new DialogParameters<BookingDialog>
        {
            { x => x.Existing, existing },
            { x => x.Accounts, accounts },
            { x => x.Categories, categories },
            { x => x.Today, today },
            { x => x.PlanMonth, planMonth },
        };

        var title = l[existing is null ? "BookingDialog_TitleNew" : "BookingDialog_TitleEdit"];
        var dialog = await dialogs.ShowAsync<BookingDialog>(title, parameters, forms.Options());
        var result = await dialog.Result;

        return result is { Canceled: false } ? new NoticeMessage(l["Bookings_Saved"], Severity.Success) : null;
    }
}
