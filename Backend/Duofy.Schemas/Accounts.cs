namespace Duofy.Schemas;

/// <summary>Die Art eines Kontos in der API: Girokonto, Sparkonto oder Bargeld.</summary>
public static class AccountTypes
{
    public const string Checking = "checking";
    public const string Savings = "savings";
    public const string Cash = "cash";

    /// <summary>Alle Arten in der Reihenfolge, in der die Oberfläche sie zeigt.</summary>
    public static readonly IReadOnlyList<string> All = [Checking, Savings, Cash];

    /// <summary>
    /// Der Vorschlag für "zählt als verfügbar": Girokonto und Bargeld ja, Sparkonto nein.
    /// Dieselbe Regel steht in der Domäne; hier braucht sie die Oberfläche beim Tippen.
    /// </summary>
    public static bool CountsAsAvailableByDefault(string? type) => type != Savings;
}

/// <summary>
/// Ein neues Konto (POST /api/v1/accounts).
/// </summary>
/// <param name="Name">Der Name, 1 bis <see cref="MoneyAccountRules.NameMaxLength"/> Zeichen.</param>
/// <param name="Type">"checking", "savings" oder "cash", siehe <see cref="AccountTypes"/>.</param>
/// <param name="OpeningBalance">Der Stand am Ende des Starttages in Euro, höchstens zwei Nachkommastellen, darf negativ sein.</param>
/// <param name="OpeningDate">Der Starttag, nicht in der Zukunft.</param>
/// <param name="CountsAsAvailable">Zählt das Geld als verfügbar? Fehlt der Wert, gilt der Vorschlag der Art.</param>
/// <param name="Iban">Die IBAN, gern mit Leerzeichen; leer heißt keine.</param>
/// <param name="IsDefault">True macht das Konto zum Standardkonto; fehlt der Wert, heißt das nein.</param>
/// <param name="OwnerId">Für wen das Konto ist (Spec 68); leer heißt: für die angemeldete Person.</param>
public sealed record CreateAccountRequest(
    string? Name,
    string? Type,
    decimal OpeningBalance,
    DateOnly OpeningDate,
    bool? CountsAsAvailable = null,
    string? Iban = null,
    bool? IsDefault = null,
    Guid? OwnerId = null);

/// <summary>
/// Eine Änderung an einem Konto (PATCH /api/v1/accounts/{id}). Jedes Feld ist freiwillig;
/// was fehlt (null), bleibt, wie es ist. Eine leere IBAN ("") entfernt die IBAN.
/// <paramref name="Active"/> false löst das Konto auf, true öffnet es wieder.
/// </summary>
public sealed record UpdateAccountRequest(
    string? Name = null,
    string? Type = null,
    decimal? OpeningBalance = null,
    DateOnly? OpeningDate = null,
    bool? CountsAsAvailable = null,
    string? Iban = null,
    bool? IsDefault = null,
    bool? Active = null);

/// <summary>Ein Konto in der Antwort der API, mit dem heutigen Stand.</summary>
/// <param name="Balance">Der Stand am Ende des heutigen Tages (Berlin), ausgerechnet, nie gespeichert.</param>
public sealed record AccountResponse(
    Guid Id,
    string Name,
    string Type,
    decimal OpeningBalance,
    DateOnly OpeningDate,
    bool CountsAsAvailable,
    string? Iban,
    bool IsDefault,
    bool Active,
    decimal Balance);

/// <summary>
/// Die Regeln für Konten, auf denen Geld liegt. Die API prüft sie, die Oberfläche kann sie schon
/// beim Tippen anzeigen. (<see cref="AccountRules"/> gilt dagegen für das Benutzerkonto der Anmeldung.)
/// </summary>
public static class MoneyAccountRules
{
    /// <summary>Die größte erlaubte Länge des Kontonamens.</summary>
    public const int NameMaxLength = 100;

    /// <summary>Der größte erlaubte Betrag des Startbetrags, nach oben und unten (Spalte numeric(12,2)).</summary>
    public const decimal MaxOpeningBalance = 9_999_999_999.99m;
}
