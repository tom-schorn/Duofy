using Duofy.ApiClient;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Duofy.Shared.Resources;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Duofy.Shared.Services;

/// <summary>Kategorien, Konten und was die Freigabe erlaubt für eine Person; <see cref="Error"/> gesetzt heißt: nicht zu laden.</summary>
public sealed record PersonChoices(List<CategoryResponse> Categories, IReadOnlyList<AccountChoice> Accounts, bool CanDelete, string? Error = null);

/// <summary>Was eine Aktion der Seite zu tun übrig lässt: eine Meldung zeigen und, wenn sich etwas geändert hat, den Monat neu holen.</summary>
public sealed record PositionActionResult(NoticeMessage? Message = null, bool Reload = false)
{
    public static readonly PositionActionResult Nothing = new();
}

/// <summary>
/// Die eine Logik für Posten und Buchungen eines Plans (Spec 125): Anlegen, Ändern, Löschen mit Rückfrage, Abhaken und Zurücknehmen,
/// Buchung anlegen und Drucken. Der eigene Plan und der Haushaltsplan benutzen sie gleich; sie unterscheiden sich nur in der Person
/// (<c>owner</c>, null heißt die eigene Planung) und darin, ob mehrere Personen zur Wahl stehen. Alle Rechte gelten für die Person, nicht für
/// die gewählte Sicht (Spec 68); die Seite zeigt nur, was zurückkommt.
/// </summary>
public sealed class PositionActions(
    FormDialogs forms,
    IPlanApi planApi,
    ICategoryApi categoryApi,
    IAccountChoices accountChoices,
    BookingDialogLauncher launcher,
    ActiveMember member,
    NavigationManager navigation,
    IPrintService printer,
    IStringLocalizer<SharedResources> l,
    ILogger<PositionActions> log)
{
    // --- Rechte der Person, deren Zeile es ist. Für sich selbst gilt alles; Ändern und Löschen hängen an der Stufe im Plan. ---

    private bool Allows(Guid? owner, string area, string level) => owner is not { } id || member.AllowsFor(id, area, level);

    public bool CanAdd(Guid? owner) => Allows(owner, GrantAreas.Plan, AccessLevelKeys.Create);

    public bool CanEdit(Guid? owner) => Allows(owner, GrantAreas.Plan, AccessLevelKeys.Edit);

    public bool CanDelete(Guid? owner) => Allows(owner, GrantAreas.Plan, AccessLevelKeys.Delete);

    public bool CanBook(Guid? owner) => Allows(owner, GrantAreas.Book, AccessLevelKeys.Create) && Allows(owner, GrantAreas.Accounts, AccessLevelKeys.View);

    /// <summary>Abhaken und Zurücknehmen brauchen wie in der API den Plan zum Ansehen, dazu Buchungen Ändern und Konten Ansehen beziehungsweise Buchungen Löschen.</summary>
    public bool CanToggle(Guid? owner, bool done) => Needs(owner, done).All(n => Allows(owner, n.Area, n.Level));

    private static (string Area, string Level)[] Needs(Guid? owner, bool done) => done
        ? [(GrantAreas.Plan, AccessLevelKeys.View), (GrantAreas.Book, AccessLevelKeys.Delete)]
        : [(GrantAreas.Plan, AccessLevelKeys.View), (GrantAreas.Book, AccessLevelKeys.Edit), (GrantAreas.Accounts, AccessLevelKeys.View)];

    /// <summary>Der Wert für den Parameter <c>owner</c> der API: null für die eigenen Daten.</summary>
    public Guid? OwnerArgument(Guid ownerId) => member.Household?.Members.FirstOrDefault(m => m.UserId == ownerId)?.IsMe == false ? ownerId : null;

    // Eine Freigabe fehlt: Die Meldung nennt, wessen und was (Spec 125, A3), genau die Stufen, die wirklich fehlen.
    private NoticeMessage Refusal(Guid? owner, string? ownerName, IEnumerable<(string Area, string Level)> needs)
    {
        var missing = needs.Where(n => !Allows(owner, n.Area, n.Level))
            .Select(n => $"{l[$"Grants_Area_{n.Area}"]}: {l[$"Grants_Level_{n.Level}"]}");
        return new NoticeMessage(l["HouseholdPlan_Missing", ownerName ?? "", string.Join(", ", missing)], NoticeSeverity.Warning);
    }

    // --- Dialoge laufen für die Person der Zeile; verlässt jemand währenddessen die Seite, endet das und die Dialoge gehen zu. ---

    private IDisposable Acting(Guid? owner)
    {
        var scope = member.ActAs(owner);
        void Left(object? sender, LocationChangedEventArgs e)
        {
            _ = ObserveAsync(forms.CancelAllAsync(), "Die Dialoge schließen beim Verlassen der Seite");
            scope.Dispose();
            // Die Seitenleiste hat beim Wechsel der Seite schon gezeichnet, als die Rolle noch galt; sie muss danach noch einmal.
            member.NotifyActingEnded();
        }
        navigation.LocationChanged += Left;
        return new Release(() =>
        {
            navigation.LocationChanged -= Left;
            scope.Dispose();
        });
    }

    // Eine Aufgabe, auf die niemand wartet: Scheitert sie, steht der Grund im Protokoll und geht nicht verloren.
    private async Task ObserveAsync(Task task, string what)
    {
        try
        {
            await task;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            log.LogError(exception, "{What} ist gescheitert.", what);
        }
    }

    private sealed class Release(Action end) : IDisposable
    {
        public void Dispose() => end();
    }

    private NoticeMessage Failed(string? code) => new(ErrorTexts.For(l, code), NoticeSeverity.Error);

    /// <summary>Kategorien und Konten der Person; die Konten nur, wenn sie sie sehen darf (wie im eigenen Plan).</summary>
    public async Task<PersonChoices> ChoicesAsync(Guid? owner)
    {
        var categories = await categoryApi.ListAsync(owner);
        var accounts = Allows(owner, GrantAreas.Accounts, AccessLevelKeys.View)
            ? await accountChoices.ListAsync(owner)
            : ApiResult<IReadOnlyList<AccountChoice>>.Success([]);
        if (!categories.Succeeded || !accounts.Succeeded)
        {
            return new PersonChoices([], [], false, ErrorTexts.For(l, categories.ErrorCode ?? accounts.ErrorCode));
        }
        return new PersonChoices(categories.Value!.ToList(), accounts.Value!, CanDelete(owner));
    }

    private async Task<PersonChoices> ChoicesForAsync(MemberResponse person) => await ChoicesAsync(person.IsMe ? null : person.UserId);

    // Für eine Buchung brauchen die Konten ein offenes Konto; sonst steht der Grund im ersten Schritt.
    private async Task<PersonChoices> BookingChoicesForAsync(MemberResponse person)
    {
        var choices = await ChoicesForAsync(person);
        return choices.Error is null && !choices.Accounts.Any(a => a.Active)
            ? choices with { Error = l["Bookings_NoAccounts"] }
            : choices;
    }

    // --- Posten anlegen und ändern ---

    /// <summary>
    /// Posten anlegen. Mit mehreren <paramref name="people"/> fragt der erste Schritt des Dialogs, für wen; sonst gilt <paramref name="owner"/>.
    /// <paramref name="known"/> sind schon geladene Kategorien und Konten der Person.
    /// </summary>
    public Task<PositionActionResult> AddAsync(int year, int month, Guid? owner, IReadOnlyList<MemberResponse>? people = null, PersonChoices? known = null) =>
        OpenPositionAsync(year, month, owner, null, people, known, null);

    /// <summary>Einen Posten ändern; <paramref name="position"/> ist der volle Posten, fehlt er, holt die Aktion ihn aus dem Monat der Person.</summary>
    public async Task<PositionActionResult> EditAsync(int year, int month, Guid? owner, string? ownerName, Guid positionId, PlanPositionResponse? position = null, PersonChoices? known = null)
    {
        if (!CanEdit(owner))
        {
            return new(Refusal(owner, ownerName, [(GrantAreas.Plan, AccessLevelKeys.Edit)]));
        }
        if (position is null)
        {
            var result = await planApi.GetAsync(year, month, owner);
            if (!result.Succeeded)
            {
                return new(Failed(result.ErrorCode));
            }
            position = result.Value!.Positions.FirstOrDefault(p => p.Id == positionId);
            if (position is null)
            {
                return new(Failed(ErrorCodes.PositionNotFound), Reload: true);
            }
        }
        return await OpenPositionAsync(year, month, owner, ownerName, null, known, position);
    }

    private async Task<PositionActionResult> OpenPositionAsync(
        int year, int month, Guid? owner, string? ownerName, IReadOnlyList<MemberResponse>? people, PersonChoices? known, PlanPositionResponse? existing)
    {
        if (existing is null && people is not { Count: > 1 } && !CanAdd(owner))
        {
            return new(Refusal(owner, ownerName, [(GrantAreas.Plan, AccessLevelKeys.Create)]));
        }
        var choosing = existing is null && people is { Count: > 1 };
        var title = l[existing is null ? "PlanPositionDialog_TitleNew" : "PlanPositionDialog_TitleEdit"].Value;
        // Mit mehreren Personen holt der erste Schritt die Daten der gewählten; sonst laden wir sie hier.
        var choices = choosing ? new PersonChoices([], [], false) : known ?? await ChoicesAsync(owner);
        if (choices.Error is not null)
        {
            return new(new NoticeMessage(choices.Error, NoticeSeverity.Error));
        }
        DialogOutcome<bool> outcome;
        using (Acting(choosing ? null : owner))
        {
            var parameters = new FormParameters<PlanPositionDialog>()
                .Add(x => x.Title, title)
                .Add(x => x.Year, year)
                .Add(x => x.Month, month)
                .Add(x => x.Existing, existing)
                .Add(x => x.Categories, choices.Categories)
                .Add(x => x.Accounts, choices.Accounts)
                .Add(x => x.OfferDelete, existing is not null && PlanView.CanDelete(existing))
                .Add(x => x.CanDelete, choices.CanDelete)
                .Add(x => x.People, choosing ? people! : (IReadOnlyList<MemberResponse>)[])
                .Add(x => x.ChoicesFor, choosing ? ChoicesForAsync : null);
            outcome = await forms.OpenStepAsync<PlanPositionDialog, bool>(title, parameters);
        }
        if (outcome.Saved && outcome.Value)
        {
            return new(new NoticeMessage(l["Plan_PositionSaved"], NoticeSeverity.Success), Reload: true);
        }
        // Der Dialog meldet nur den Wunsch zu löschen; die Rückfrage und das Löschen sind hier wie überall dieselben.
        return outcome.Saved && existing is not null ? await DeleteAsync(year, month, owner, ownerName, existing) : PositionActionResult.Nothing;
    }

    /// <summary>Einen Posten löschen, nach der Rückfrage (für jede Person gleich).</summary>
    public async Task<PositionActionResult> DeleteAsync(int year, int month, Guid? owner, string? ownerName, PlanPositionResponse position)
    {
        if (!CanDelete(owner))
        {
            return new(Refusal(owner, ownerName, [(GrantAreas.Plan, AccessLevelKeys.Delete)]));
        }
        if (!await forms.ConfirmAsync(l["Plan_DeleteText", position.Label], l["Plan_DeleteTitle"], l["Plan_DeleteConfirm"], l["Plan_ConfirmCancel"]))
        {
            return PositionActionResult.Nothing;
        }
        var result = await planApi.DeletePositionAsync(year, month, position.Id, owner);
        return new(result.Succeeded ? new NoticeMessage(l["Plan_PositionDeleted"], NoticeSeverity.Success) : Failed(result.ErrorCode), Reload: true);
    }

    // --- Abhaken und Zurücknehmen ---

    /// <summary>
    /// Abhaken oder den Haken zurücknehmen (Spec 82, R5; Spec 125, R3). Ein offener Posten ohne Buchungen fragt nach Datum und Betrag und
    /// bucht erst nach der Bestätigung (die Buchung gehört der Person der Zeile). Hat ein Posten schon Buchungen, entsteht keine weitere:
    /// Der Haken heißt dann "Monat erledigt"; die API hakt mit dem Betrag 0 ab.
    /// </summary>
    public async Task<PositionActionResult> ToggleAsync(int year, int month, Guid? owner, string? ownerName, PlanPositionResponse position)
    {
        var done = position.DoneAt is not null;
        if (!CanToggle(owner, done))
        {
            return new(Refusal(owner, ownerName, Needs(owner, done)));
        }
        if (done)
        {
            return Show(await planApi.UntickAsync(year, month, position.Id, owner), "Plan_Unticked");
        }
        if (position.Booked != 0m)
        {
            return Show(await planApi.TickAsync(year, month, position.Id, new TickPositionRequest(Amount: 0m), owner), "Plan_Ticked");
        }
        string title = l["PlanTickDialog_Title"];
        DialogOutcome<bool> outcome;
        using (Acting(owner))
        {
            var parameters = new FormParameters<PlanTickDialog>()
                .Add(x => x.Year, year)
                .Add(x => x.Month, month)
                .Add(x => x.Position, position);
            outcome = await forms.OpenAsync<PlanTickDialog, bool>(title, parameters);
        }
        return outcome.Saved ? new(new NoticeMessage(l["Plan_Ticked"], NoticeSeverity.Success), Reload: true) : PositionActionResult.Nothing;
    }

    private PositionActionResult Show<T>(ApiResult<T> result, string successKey) =>
        new(result.Succeeded ? new NoticeMessage(l[successKey], NoticeSeverity.Success) : Failed(result.ErrorCode), Reload: true);

    private PositionActionResult Show(ApiResult result, string successKey) =>
        new(result.Succeeded ? new NoticeMessage(l[successKey], NoticeSeverity.Success) : Failed(result.ErrorCode), Reload: true);

    /// <summary>Der Posten einer Zeile des Haushaltsplans, soweit der Dialog zum Abhaken ihn braucht: Bezeichnung, Betrag und Kennung.</summary>
    public static PlanPositionResponse ForTick(HouseholdPlanPositionResponse position) =>
        new(position.Id, null, position.Label, position.AmountPlanned, position.DueDay, position.Type, position.CategoryId, position.Pot,
            null, null, position.PassThrough, PaymentMethods.Other, position.Booked, position.DoneAt, false, null);

    // --- Buchung anlegen ---

    /// <summary>
    /// Buchung anlegen. Mit mehreren <paramref name="people"/> fragt der erste Schritt des Dialogs, für wen (nur wer Anlegen bei den
    /// Buchungen und Ansehen bei den Konten erlaubt hat); sonst gilt <paramref name="owner"/> mit den schon geladenen Konten und Kategorien.
    /// </summary>
    public async Task<PositionActionResult> NewBookingAsync(
        int year, int month, Guid? owner, string? ownerName, IReadOnlyList<MemberResponse>? people = null, PersonChoices? known = null, Guid? positionId = null)
    {
        var choosing = people is { Count: > 1 };
        if (!choosing && !CanBook(owner))
        {
            return new(Refusal(owner, ownerName, [(GrantAreas.Book, AccessLevelKeys.Create), (GrantAreas.Accounts, AccessLevelKeys.View)]));
        }
        NoticeMessage? message;
        using (Acting(choosing ? null : owner))
        {
            message = await launcher.OpenAsync(null, choosing ? null : known?.Accounts, choosing ? null : known?.Categories,
                new DateOnly(year, month, 1), positionId, people, choosing ? BookingChoicesForAsync : null);
        }
        return new(message, Reload: true);
    }

    // --- Drucken ---

    /// <summary>Druckt das Druckbild der Seite; scheitert das, steht der Grund in der Meldung und im Protokoll.</summary>
    public async Task<NoticeMessage?> PrintAsync()
    {
        try
        {
            await printer.PrintAsync();
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Das Drucken ist Sache des Systems (Browser, Teilen-Dialog, Dateien); was dort schiefgeht, lässt sich nicht aufzählen.
            log.LogError(exception, "Das Drucken des Plans ist gescheitert.");
            return new NoticeMessage(l["Plan_PrintFailed"], NoticeSeverity.Error);
        }
    }
}
