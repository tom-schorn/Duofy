using Duofy.ApiClient;
using Duofy.Schemas;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace Duofy.Shared.Services;

/// <summary>
/// Wessen Zahlen die Oberfläche gerade zeigt (Spec 68, R8): "Ich", ein Mitglied des Haushalts, das der
/// angemeldeten Person in irgendeinem Bereich mindestens Ansehen freigegeben hat. Der Haushalt steht nicht in der
/// Personenauswahl (Spec 126, R1); die Haushaltssicht des Plans erreicht man über die Navigation und gibt es nur für den Plan.
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

    // Wer gerade handelt (Spec 125): Im Haushaltsplan arbeitet die Person für ein Mitglied, ohne dass die Sicht wechselt.
    // Null heißt: kein Bereich offen; sonst die Person, für die ein Dialog läuft (bei "Ich" ein leeres Mitglied).
    private (MemberResponse? Member, bool Open) acting;

    /// <summary>Der Besitzer, den die Seiten der API mitgeben; null für die eigenen Daten.</summary>
    public Guid? Owner => (acting.Open ? acting.Member : Chosen)?.UserId;

    /// <summary>Der Vorname des gewählten Mitglieds für die Gründe an ausgegrauten Knöpfen; null bei "Ich".</summary>
    public string? Name => (acting.Open ? acting.Member : Chosen)?.FirstName;

    /// <summary>
    /// Die Stufe in einem Bereich (<see cref="GrantAreas"/>): bei "Ich" Löschen, sonst die Freigabe des Mitglieds.
    /// Die Haushaltssicht hat selbst keine Stufen; im Haushaltsplan gilt für jede Zeile die Stufe ihrer Person (<see cref="AllowsFor"/>).
    /// </summary>
    public string LevelIn(string area)
    {
        if (acting.Open)
        {
            return acting.Member is null ? AccessLevelKeys.Delete : acting.Member.GrantsToMe.For(area);
        }
        return IsHousehold ? AccessLevelKeys.None : Chosen is null ? AccessLevelKeys.Delete : Chosen.GrantsToMe.For(area);
    }

    /// <summary>True, wenn die Stufe im Bereich mindestens <paramref name="level"/> ist.</summary>
    public bool Allows(string area, string level) => AccessLevelKeys.Includes(LevelIn(area), level);

    /// <summary>
    /// Die Personen, für die die angemeldete Person in einem Bereich mindestens diese Stufe hat (Spec 125, R2): sie selbst zuerst,
    /// dann die Mitglieder des Haushalts, deren Freigabe sie erreicht. Ohne Haushalt ist niemand angeboten.
    /// </summary>
    public IReadOnlyList<MemberResponse> PeopleWith(string area, string level) => Household is null
        ? []
        : Household.Members.Where(m => m.IsMe).Concat(Household.Members.Where(m => !m.IsMe && AccessLevelKeys.Includes(m.GrantsToMe.For(area), level))).ToList();

    /// <summary>
    /// Ob die angemeldete Person im Bereich für diese Person mindestens diese Stufe hat, unabhängig von der gewählten Sicht
    /// (Spec 125, R3): Im Haushaltsplan steht jede Zeile für ihre eigene Person. Für sich selbst gilt alles, für Fremde nichts.
    /// </summary>
    public bool AllowsFor(Guid ownerId, string area, string level) => Household?.Members.FirstOrDefault(m => m.UserId == ownerId) switch
    {
        null => false,
        { IsMe: true } => true,
        var other => AccessLevelKeys.Includes(other.GrantsToMe.For(area), level),
    };

    /// <summary>
    /// Lässt Dialoge und Aufrufe bis zum Ende des Bereichs für diese Person laufen (Spec 125): <see cref="Owner"/>, <see cref="Name"/> und
    /// <see cref="Allows"/> gelten dann für sie, ohne die gewählte Sicht zu ändern und ohne <see cref="Changed"/>. Eine unbekannte Person
    /// oder die eigene heißt "Ich".
    /// </summary>
    public IDisposable ActAs(Guid? userId)
    {
        var previous = acting;
        var member = userId is { } id ? Household?.Members.FirstOrDefault(m => m.UserId == id && !m.IsMe) : null;
        acting = (member, true);
        return new ActingScope(() => acting = previous);
    }

    private sealed class ActingScope(Action end) : IDisposable
    {
        private Action? end = end;

        public void Dispose()
        {
            end?.Invoke();
            end = null;
        }
    }

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
