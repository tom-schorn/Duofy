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

    public CreateAccountRequest ToCreateRequest() =>
        new(Name, Type, OpeningBalance ?? 0m, OpeningDate, CountsAsAvailable, Iban);

    /// <summary>Alle Felder des Dialogs; eine leere IBAN geht als "" mit und entfernt die gespeicherte.</summary>
    public UpdateAccountRequest ToUpdateRequest() =>
        new(Name, Type, OpeningBalance ?? 0m, OpeningDate, CountsAsAvailable, Iban);
}
