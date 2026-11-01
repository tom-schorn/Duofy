using Duofy.Schemas;

namespace Duofy.Shared.Services;

/// <summary>
/// Der Entwurf eines Kontos im Dialog. Die Klasse hält die Eingaben und die eine Regel des
/// Dialogs, damit die Seite schlicht bleibt und man die Regel ohne Oberfläche testen kann:
///
/// Wählt man eine Art, stellt sich der Schalter "zählt als verfügbar" auf ihren Vorschlag
/// (Girokonto und Bargeld an, Sparkonto aus). Hat die Person den Schalter selbst gestellt,
/// lässt eine spätere Wahl der Art ihn in Ruhe. Beim Ändern eines gespeicherten Kontos gilt
/// der gespeicherte Schalter als selbst gestellt.
///
/// Ob die Eingaben gültig sind, sagt erst die API.
/// </summary>
public sealed class AccountDraft
{
    private bool switchTouched;

    // Die Wahl der Person oder das gespeicherte eigene Symbol; null heißt: das Standardsymbol der Art.
    private string? icon;
    private bool iconTouched;

    private AccountDraft()
    {
    }

    public string Name { get; set; } = "";

    /// <summary>"checking", "savings" oder "cash", siehe <see cref="AccountTypes"/>.</summary>
    public string Type { get; private set; } = AccountTypes.Checking;

    /// <summary>Der Startbetrag; leer zählt als 0.</summary>
    public decimal? OpeningBalance { get; set; }

    public DateOnly OpeningDate { get; set; }

    public bool CountsAsAvailable { get; private set; } = true;

    /// <summary>Die IBAN, so wie sie getippt wurde; die API entfernt Leerzeichen und prüft sie.</summary>
    public string Iban { get; set; } = "";

    /// <summary>Das Symbol, das der Dialog zeigt: die Wahl, sonst das Standardsymbol der Art (es folgt der Art).</summary>
    public string Icon => icon ?? AccountIcons.DefaultFor(Type);

    /// <summary>Wählt ein Symbol. Wer das Standardsymbol der Art wählt, wählt damit "kein eigenes".</summary>
    public void SetIcon(string value)
    {
        icon = value == AccountIcons.DefaultFor(Type) ? null : value;
        iconTouched = true;
    }

    /// <summary>Der Entwurf für ein neues Konto: Girokonto, Starttag heute.</summary>
    public static AccountDraft ForNew(DateOnly today) => new() { OpeningDate = today };

    /// <summary>Der Entwurf zum Ändern eines gespeicherten Kontos.</summary>
    public static AccountDraft From(AccountResponse account) => new()
    {
        Name = account.Name,
        Type = account.Type,
        OpeningBalance = account.OpeningBalance,
        OpeningDate = account.OpeningDate,
        CountsAsAvailable = account.CountsAsAvailable,
        Iban = account.Iban ?? "",
        icon = account.Icon == AccountIcons.DefaultFor(account.Type) ? null : account.Icon,
        switchTouched = true,
    };

    public void SetType(string type)
    {
        Type = type;
        if (!switchTouched)
        {
            CountsAsAvailable = AccountTypes.CountsAsAvailableByDefault(type);
        }
    }

    public void SetCountsAsAvailable(bool value)
    {
        CountsAsAvailable = value;
        switchTouched = true;
    }

    /// <summary>Das Symbol geht nur mit, wenn die Person ein eigenes gewählt hat; sonst gilt das der Art.</summary>
    public CreateAccountRequest ToCreateRequest() =>
        new(Name, Type, OpeningBalance ?? 0m, OpeningDate, CountsAsAvailable, Iban, Icon: iconTouched ? icon : null);

    /// <summary>
    /// Alle Felder des Dialogs; eine leere IBAN geht als "" mit und entfernt die gespeicherte. Das Symbol geht nur mit, wenn
    /// die Person es angefasst hat (ein Speichern aus anderem Grund pinnt es nicht): ein eigenes als Name, die Rückkehr
    /// zum Standard der Art als "".
    /// </summary>
    public UpdateAccountRequest ToUpdateRequest() =>
        new(Name, Type, OpeningBalance ?? 0m, OpeningDate, CountsAsAvailable, Iban, Icon: iconTouched ? icon ?? "" : null);
}
