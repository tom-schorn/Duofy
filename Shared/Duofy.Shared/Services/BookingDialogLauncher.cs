using Duofy.ApiClient;
using Duofy.Schemas;
using Duofy.Shared.Dialogs;
using Duofy.Shared.Pages;
using Duofy.Shared.Resources;
using Microsoft.Extensions.Localization;

namespace Duofy.Shared.Services;

/// <summary>
/// Öffnet den Buchungsdialog: Der Monat zum Anlegen, die Bewegungen am Konto zum Ändern (Spec 51). Hat die Seite Konten
/// und Kategorien schon geladen, reicht sie sie mit, sonst holt der Dienst sie selbst.
/// </summary>
public sealed class BookingDialogLauncher(
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
        DateOnly? planMonth = null,
        Guid? positionId = null,
        IReadOnlyList<MemberResponse>? people = null,
        Func<MemberResponse, Task<PersonChoices>>? choicesFor = null)
    {
        // Mit mehreren Personen zur Wahl holt der erste Schritt des Dialogs Konten und Kategorien der gewählten (Spec 125, R7).
        var choosing = people is { Count: > 1 } && choicesFor is not null;
        if (choosing)
        {
            accounts = [];
            categories = [];
        }
        if (accounts is null)
        {
            // Die Konten der Person, deren Zahlen gerade offen sind (Spec 68).
            var accountResult = await accountChoices.ListAsync(member.Owner);
            if (!accountResult.Succeeded)
            {
                return new NoticeMessage(ErrorTexts.For(l, accountResult.ErrorCode), NoticeSeverity.Error);
            }
            accounts = accountResult.Value!;
        }

        // Ohne offenes Konto gibt es nichts Neues zu buchen.
        if (existing is null && !choosing && !accounts.Any(a => a.Active))
        {
            return new NoticeMessage(l["Bookings_NoAccounts"], NoticeSeverity.Info);
        }

        if (categories is null)
        {
            var categoryResult = await categoryApi.ListAsync(member.Owner);
            if (!categoryResult.Succeeded)
            {
                return new NoticeMessage(ErrorTexts.For(l, categoryResult.ErrorCode), NoticeSeverity.Error);
            }
            categories = categoryResult.Value!.ToList();
        }

        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);

        // Die Parameter des Dialogs; die Schreibweise mit x => x.Name prüft schon beim Übersetzen, ob es sie gibt.
        var title = l[existing is null ? "BookingDialog_TitleNew" : "BookingDialog_TitleEdit"].Value;
        var parameters = new FormParameters<BookingDialog>()
            .Add(x => x.Title, title)
            .Add(x => x.Existing, existing)
            .Add(x => x.Accounts, accounts)
            .Add(x => x.Categories, categories)
            .Add(x => x.Today, today)
            .Add(x => x.PlanMonth, planMonth)
            .Add(x => x.PositionId, positionId)
            .Add(x => x.People, choosing ? people! : [])
            .Add(x => x.ChoicesFor, choosing ? choicesFor : null);

        // Im Browser ein Fenster, in der App der ganze Bildschirm; der Dialog zeichnet Titel und Abbrechen selbst.
        var outcome = await forms.OpenStepAsync<BookingDialog, bool>(title, parameters);

        return outcome.Saved ? new NoticeMessage(l["Bookings_Saved"], NoticeSeverity.Success) : null;
    }
}
