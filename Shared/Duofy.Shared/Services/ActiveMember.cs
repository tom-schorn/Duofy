using Duofy.ApiClient;
using Duofy.Schemas;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace Duofy.Shared.Services;

/// <summary>
/// Wessen Zahlen die Oberfläche gerade zeigt (Spec 68, R8): "Ich", ein Mitglied des Haushalts, das der
/// angemeldeten Person in irgendeinem Bereich mindestens Ansehen freigegeben hat, oder der ganze Haushalt
/// (Spec 69, R9), solange er mehr als ein Mitglied hat. Die Haushaltssicht gibt es nur für den Plan.
///
/// Der Dienst lebt so lange wie die Sitzung (in der App die ganze Laufzeit, im Web eine Sitzung des Browsers),
/// deshalb bleibt die Wahl beim Wechsel zwischen den Seiten. Jede Seite fragt hier nach dem Besitzer, den sie
/// der API mitgibt, und danach, ob ein Knopf gehen darf. Beim Abmelden ist die Wahl wieder "Ich".
/// </summary>
public sealed class ActiveMember : IDisposable
{
    private readonly IHouseholdApi households;
    private readonly AuthenticationStateProvider authentication;
    private readonly ILogger<ActiveMember> logger;
    private Task? loading;

    // Wer beim letzten Laden "Ich" war. Erneuert der Client die Tokens, bleibt die Wahl; steht danach jemand
    // anderes als "Ich" im Haushalt, gilt sie nicht mehr (Spec 68, A15).
    private Guid? me;

    public ActiveMember(IHouseholdApi households, AuthenticationStateProvider authentication, ILogger<ActiveMember> logger)
    {
        this.households = households;
        this.authentication = authentication;
        this.logger = logger;
        authentication.AuthenticationStateChanged += OnAuthenticationChanged;
    }

    /// <summary>Wird ausgelöst, wenn sich die Wahl oder die Liste der Mitglieder geändert hat.</summary>
    public event Action? Changed;

    /// <summary>Die Mitglieder, die der angemeldeten Person etwas zeigen, in der Reihenfolge des Haushalts.</summary>
    public IReadOnlyList<MemberResponse> Sharing { get; private set; } = [];

    /// <summary>Das gewählte Mitglied; null heißt "Ich" oder die Haushaltssicht.</summary>
    public MemberResponse? Chosen { get; private set; }

    /// <summary>Der eigene Haushalt aus dem letzten Laden; null ohne Haushalt.</summary>
    public HouseholdResponse? Household { get; private set; }

    /// <summary>True, wenn der Haushalt gewählt ist: Die Planseite zeigt dann den Haushaltsplan, alles andere hat keine gemeinsame Sicht.</summary>
    public bool IsHousehold { get; private set; }

    /// <summary>True, wenn der Haushalt mehr als ein Mitglied hat und sich als Sicht wählen lässt (Spec 69, R9).</summary>
    public bool CanShowHousehold => Household is { Members.Count: > 1 };

    /// <summary>Der Besitzer, den die Seiten der API mitgeben; null für die eigenen Daten.</summary>
    public Guid? Owner => Chosen?.UserId;

    /// <summary>Der Vorname des gewählten Mitglieds für die Gründe an ausgegrauten Knöpfen; null bei "Ich".</summary>
    public string? Name => Chosen?.FirstName;

    /// <summary>
    /// Die Stufe in einem Bereich (<see cref="GrantAreas"/>): bei "Ich" Löschen, sonst die Freigabe des Mitglieds.
    /// Der Haushalt hat keine Stufen, er ist nur zum Lesen und nur im Plan.
    /// </summary>
    public string LevelIn(string area) =>
        IsHousehold ? AccessLevelKeys.None : Chosen is null ? AccessLevelKeys.Delete : Chosen.GrantsToMe.For(area);

    /// <summary>True, wenn die Stufe im Bereich mindestens <paramref name="level"/> ist.</summary>
    public bool Allows(string area, string level) => AccessLevelKeys.Includes(LevelIn(area), level);

    /// <summary>Lädt die Mitglieder einmal; spätere Aufrufe warten nur auf das erste Laden.</summary>
    public Task LoadAsync() => loading ??= RefreshAsync();

    /// <summary>
    /// Holt die Mitglieder neu, zum Beispiel nachdem sich Freigaben geändert haben. Teilt das gewählte Mitglied
    /// danach nichts mehr, gilt wieder "Ich".
    /// </summary>
    public async Task RefreshAsync()
    {
        var result = await households.GetMineAsync();
        if (!result.Succeeded && result.ErrorCode != ClientErrorCodes.NotFound)
        {
            // Ohne Haushalt (NotFound) gibt es niemanden zum Umschalten; jeder andere Fehler wird nur
            // festgehalten, die eigenen Zahlen bleiben dann sichtbar.
            logger.LogWarning("Mitglieder für den Umschalter nicht geladen: {ErrorCode}.", result.ErrorCode);
        }
        Sharing = result.Succeeded
            ? result.Value!.Members.Where(m => !m.IsMe && m.GrantsToMe.SharesAnything).ToList()
            : [];
        Household = result.Succeeded ? result.Value : null;
        var nowMe = Household?.Members.FirstOrDefault(m => m.IsMe)?.UserId;
        if (me is not null && nowMe is not null && nowMe != me)
        {
            Chosen = null;
            IsHousehold = false;
        }
        me = nowMe ?? me;
        Chosen = Chosen is { } chosen ? Sharing.FirstOrDefault(m => m.UserId == chosen.UserId) : null;
        // Schrumpft der Haushalt auf eine Person, gibt es die Haushaltssicht nicht mehr.
        IsHousehold &= CanShowHousehold;
        Changed?.Invoke();
    }

    /// <summary>Wählt ein Mitglied aus <see cref="Sharing"/>; null oder ein unbekanntes heißt "Ich".</summary>
    public void Choose(Guid? userId)
    {
        var next = userId is { } id ? Sharing.FirstOrDefault(m => m.UserId == id) : null;
        if (next?.UserId == Chosen?.UserId && !IsHousehold)
        {
            return;
        }
        Chosen = next;
        IsHousehold = false;
        Changed?.Invoke();
    }

    /// <summary>Wählt den ganzen Haushalt (Spec 69, R9); ohne Haushalt mit mindestens zwei Mitgliedern bleibt alles, wie es ist.</summary>
    public void ChooseHousehold()
    {
        if (!CanShowHousehold || IsHousehold)
        {
            return;
        }
        Chosen = null;
        IsHousehold = true;
        Changed?.Invoke();
    }

    private async void OnAuthenticationChanged(Task<AuthenticationState> state)
    {
        bool signedIn;
        try
        {
            signedIn = (await state).User.Identity?.IsAuthenticated == true;
        }
        catch (Exception exception)
        {
            // Ein Ereignis-Handler ohne Aufrufer: Ein Fehler hier würde die ganze Sitzung beenden. Ohne lesbaren
            // Zustand ist nicht sicher, wer angemeldet ist; dann lieber zurück auf "Ich".
            logger.LogWarning(exception, "Anmeldezustand für den Umschalter nicht lesbar; die Wahl wird zurückgesetzt.");
            signedIn = false;
        }

        if (signedIn)
        {
            // Eine Erneuerung der Tokens meldet sich hier genauso wie eine neue Anmeldung. Die Wahl bleibt; die
            // nächste Seite lädt die Mitglieder neu, und ist dann jemand anderes "Ich", fällt sie zurück.
            loading = null;
            return;
        }

        // Wer sich abmeldet, sieht nach der nächsten Anmeldung zuerst wieder die eigenen Zahlen.
        Chosen = null;
        IsHousehold = false;
        Household = null;
        Sharing = [];
        loading = null;
        me = null;
        Changed?.Invoke();
    }

    public void Dispose() => authentication.AuthenticationStateChanged -= OnAuthenticationChanged;
}
