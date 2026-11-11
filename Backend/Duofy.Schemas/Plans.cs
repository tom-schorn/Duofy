namespace Duofy.Schemas;

/// <summary>Welche Art von Unterschied zwischen Monat und Verträgen die API meldet.</summary>
public static class PlanChangeKinds
{
    /// <summary>Der Vertrag hat andere Werte als sein Posten.</summary>
    public const string Changed = "changed";

    /// <summary>Der Vertrag ist in diesem Monat fällig, hat aber noch keinen Posten.</summary>
    public const string New = "new";

    /// <summary>Der Vertrag des Postens ist in diesem Monat nicht mehr fällig.</summary>
    public const string NoLongerDue = "no_longer_due";
}

/// <summary>
/// Die Felder eines Postens, die aus dem Vertrag stammen. Die Werte vorher und nachher stehen als
/// Text in der Antwort: Beträge mit Punkt ("890.00"), Kennungen, Schlüssel von Topf, Art und
/// Zahlungsart, Wahrheitswerte als "true" oder "false"; null heißt leer.
/// </summary>
public static class PlanFields
{
    public const string Label = "label";
    public const string Amount = "amount";
    public const string DueDay = "due_day";
    public const string Type = "type";
    public const string Category = "category";
    public const string Pot = "pot";
    public const string Account = "account";
    public const string CounterAccount = "counter_account";
    public const string PassThrough = "pass_through";
    public const string PaymentMethod = "payment_method";
}

/// <summary>Ein Posten in der Antwort der API.</summary>
/// <param name="CommitmentId">Der Vertrag, aus dem der Posten stammt; null bei einem einmaligen Posten.</param>
/// <param name="Type">Die Art wie beim Vertrag, siehe <see cref="CommitmentTypes"/>.</param>
/// <param name="AccountId">Das zahlende Konto; null heißt Standardkonto.</param>
/// <param name="Booked">Die Summe der Buchungszeilen, die dem Posten zugeordnet sind; größer als 0 heißt: er hat Buchungen.</param>
/// <param name="DoneAt">Wann der Posten abgehakt wurde; null, solange er offen ist.</param>
/// <param name="ChangedByHand">True, wenn der Posten im Monat von Hand geändert wurde.</param>
/// <param name="TickBookingId">Die Buchung, die beim Abhaken entstanden ist.</param>
public sealed record PlanPositionResponse(
    Guid Id,
    Guid? CommitmentId,
    string Label,
    decimal AmountPlanned,
    int DueDay,
    string Type,
    Guid CategoryId,
    string Pot,
    Guid? AccountId,
    Guid? CounterAccountId,
    bool PassThrough,
    string PaymentMethod,
    decimal Booked,
    DateTimeOffset? DoneAt,
    bool ChangedByHand,
    Guid? TickBookingId);

/// <summary>Ein Feld eines Unterschieds mit dem Wert im Posten (vorher) und im Vertrag (nachher).</summary>
/// <param name="Field">Siehe <see cref="PlanFields"/>.</param>
public sealed record PlanFieldChangeResponse(string Field, string? Before, string? After);

/// <summary>Ein Unterschied zwischen dem Monat und den Verträgen von heute.</summary>
/// <param name="Kind">Siehe <see cref="PlanChangeKinds"/>.</param>
/// <param name="PositionId">Der Posten; null bei einem neuen Vertrag.</param>
/// <param name="Label">Die Bezeichnung zum Anzeigen: die des Postens oder, bei einem neuen Vertrag, dessen Name.</param>
/// <param name="Protected">True bei einem abgehakten oder von Hand geänderten Posten; "alle übernehmen" lässt ihn aus.</param>
public sealed record PlanChangeResponse(
    string Kind,
    Guid? PositionId,
    Guid CommitmentId,
    string Label,
    IReadOnlyList<PlanFieldChangeResponse> Fields,
    bool Protected);

/// <summary>Ziel und Ist der Quote eines Topfs.</summary>
/// <param name="Pot">"needs", "wants" oder "savings".</param>
/// <param name="Target">Die Zielquote des Monats in ganzen Prozent.</param>
/// <param name="Actual">Das Ist in Prozent mit einer Nachkommastelle.</param>
public sealed record PlanQuotaResponse(string Pot, int Target, decimal Actual);

/// <summary>Der gerechnete Stand eines Kontos am letzten Tag des Vormonats, für den Übertrag.</summary>
public sealed record PlanAccountBalanceResponse(Guid AccountId, decimal Balance);

/// <summary>Was der Dialog "Übertrag" braucht.</summary>
/// <param name="Suggested">Einnahmen minus Ausgaben des Vormonats nach Planmonat.</param>
/// <param name="Exists">True, wenn es für diesen Monat schon einen Übertrag gibt.</param>
/// <param name="Amount">Der Betrag des vorhandenen Übertrags; null ohne Übertrag.</param>
/// <param name="Balances">Die offenen Konten mit ihrem gerechneten Stand am letzten Tag des Vormonats.</param>
public sealed record PlanCarryOverResponse(
    decimal Suggested,
    bool Exists,
    decimal? Amount,
    IReadOnlyList<PlanAccountBalanceResponse> Balances);

/// <summary>
/// Ein Monat (GET /api/v1/plans/{year}/{month}): die Posten nach Fälligkeitstag und Name, die
/// Summen, die Quoten, die Unterschiede zu den Verträgen und der Übertrag.
/// </summary>
/// <param name="PlannedIncome">Die geplanten Einnahmen (Posten im Topf Einnahmen).</param>
/// <param name="PlannedExpenses">Die geplanten Ausgaben (alle anderen Posten).</param>
/// <param name="BookedIncome">Die Einnahmen der Buchungen mit diesem Planmonat, ohne das Paar eines Übertrags.</param>
/// <param name="BookedExpenses">Die Ausgaben der Buchungen mit diesem Planmonat, ohne das Paar eines Übertrags; Umbuchungen zählen nicht.</param>
public sealed record PlanResponse(
    Guid Id,
    int Year,
    int Month,
    IReadOnlyList<PlanPositionResponse> Positions,
    decimal PlannedIncome,
    decimal PlannedExpenses,
    decimal BookedIncome,
    decimal BookedExpenses,
    IReadOnlyList<PlanQuotaResponse> Quotas,
    IReadOnlyList<PlanChangeResponse> Changes,
    PlanCarryOverResponse CarryOver,
    PlanOverviewResponse? Overview = null);

/// <summary>Geplant und gebucht in einem Topf, ohne durchlaufende Posten.</summary>
/// <param name="Pot">"needs", "wants" oder "savings".</param>
public sealed record PlanPotResponse(string Pot, decimal Planned, decimal Booked);

/// <summary>
/// Was die Übersicht eines Monats zeigt (Spec 24), von der API gerechnet: die Blöcke, die Töpfe für das Lückendiagramm
/// und die Buchungen mit diesem Planmonat, die neueste zuerst (auch die, deren Datum in einem anderen Monat liegt).
/// Ohne Sicht auf die Buchungen sind Gebuchtes und die Liste leer.
/// </summary>
/// <param name="Income">Die geplanten Einnahmen ohne durchlaufende Posten.</param>
/// <param name="Fixed">Die geplanten Ausgaben ohne durchlaufende Posten.</param>
/// <param name="Free">Einnahmen minus Festes plus der Übertrag aus dem Vormonat, wie in der Übersicht der Monate; auch negativ möglich. Ohne Sicht auf die Buchungen ohne Übertrag.</param>
/// <param name="Bookings">Die Buchungen mit diesem Planmonat ohne die beiden Buchungen eines Übertrags (Spec 66, R9).</param>
/// <param name="CarryOvers">Die Überträge dieses Monats als eigene Zeilen; leer ohne Sicht auf die Buchungen.</param>
public sealed record PlanOverviewResponse(
    decimal Income,
    decimal Fixed,
    decimal Free,
    IReadOnlyList<PlanPotResponse> Pots,
    IReadOnlyList<BookingResponse> Bookings,
    IReadOnlyList<PlanCarryOverLineResponse>? CarryOvers = null);

/// <summary>Die Richtung einer Zeile des Übertrags.</summary>
public static class CarryOverDirections
{
    /// <summary>"Übertrag aus ...": der Rest des Vormonats kommt an.</summary>
    public const string In = "in";

    /// <summary>"Übertrag nach ...": der Rest geht in den nächsten Monat.</summary>
    public const string Out = "out";
}

/// <summary>Ein Übertrag als eigene Zeile des Monats (Spec 66, R9): in keinem Topf und keiner Quote, ohne Kennung einer Buchung.</summary>
/// <param name="Direction">Siehe <see cref="CarryOverDirections"/>.</param>
/// <param name="Year">Das Jahr des Monats, den die Zeile nennt: woher der Rest kommt oder wohin er geht.</param>
/// <param name="Month">Dieser Monat, 1 bis 12.</param>
/// <param name="Amount">Der übertragene Rest; negativ bei einem negativen Rest.</param>
/// <param name="OwnerFirstName">Nur im Haushaltsplan: wessen Übertrag es ist.</param>
public sealed record PlanCarryOverLineResponse(string Direction, int Year, int Month, decimal Amount, string? OwnerFirstName = null);

/// <summary>
/// Ein Monat in der Übersicht der Pläne (GET /api/v1/plans, Spec 23).
/// </summary>
/// <param name="Free">Der Frei-Betrag: geplante Einnahmen minus geplante Ausgaben, ohne durchlaufende Posten, plus der Übertrag aus dem Vormonat; auch negativ möglich.</param>
/// <param name="CarryOver">Der in <paramref name="Free"/> enthaltene Übertrag; null ohne Übertrag oder ohne Sicht auf die Buchungen.</param>
public sealed record PlanMonthResponse(int Year, int Month, decimal Free, decimal? CarryOver = null);

/// <summary>
/// Ein Monat in der Übersicht mit Zahlen (GET /api/v1/plans/overview, Issue #94): alles, was eine Monatskarte der
/// Plan-Übersicht und die Jahreslinie der Startseite zeigen, mit denselben Zahlen wie der Monat selbst.
/// </summary>
/// <param name="Free">Verplanbar wie im Monat (<see cref="PlanOverviewResponse.Free"/>), auch negativ möglich.</param>
/// <param name="CarriedIn">True, wenn im Verplanbar-Betrag ein Übertrag aus dem Vormonat steckt.</param>
/// <param name="Unpaid">Was von den nicht abgehakten Posten noch offen ist, ohne Einnahmen und durchlaufende Posten.</param>
/// <param name="Pots">Geplant und gebucht je Topf (Grundbedarf, Wünsche, Sparen), wie im Monat.</param>
public sealed record PlanOverviewMonthResponse(
    int Year, int Month, decimal Free, bool CarriedIn, decimal Unpaid, IReadOnlyList<PlanPotResponse> Pots);

/// <summary>Ein einmaliger Posten (POST /api/v1/plans/{year}/{month}/positions).</summary>
/// <param name="Label">Die Bezeichnung, 1 bis <see cref="PlanRules.LabelMaxLength"/> Zeichen.</param>
/// <param name="Amount">Der geplante Betrag in Euro, größer als null.</param>
/// <param name="DueDay">Der Tag im Monat, 1 bis zum letzten Tag des Monats.</param>
/// <param name="CategoryId">Eine eingebaute oder eigene Kategorie, passend zum Topf.</param>
/// <param name="Pot">Der Topf, siehe <see cref="PotKeys"/>; Einnahmen nur mit Einnahme-Kategorien.</param>
/// <param name="AccountId">Das zahlende Konto; leer heißt: das Standardkonto.</param>
public sealed record CreatePositionRequest(
    string? Label,
    decimal Amount,
    int DueDay,
    Guid? CategoryId,
    string? Pot,
    Guid? AccountId = null);

/// <summary>
/// Eine Änderung an einem Posten (PATCH .../positions/{id}). Was fehlt, bleibt; das Konto wird mit
/// <paramref name="ClearAccountId"/> geleert. Ein Posten aus einem Vertrag gilt danach als von Hand geändert.
/// </summary>
public sealed record UpdatePositionRequest(
    string? Label = null,
    decimal? Amount = null,
    int? DueDay = null,
    Guid? CategoryId = null,
    string? Pot = null,
    Guid? AccountId = null,
    bool? ClearAccountId = null);

/// <summary>
/// Das Abhaken eines Postens (POST .../positions/{id}/tick). Alles ist freiwillig: Ohne Buchungen
/// am Posten entsteht eine Buchung über den geplanten Betrag; mit Buchungen nur, wenn ein Betrag
/// über 0 geschickt wird (der Ausgleich). 0 hakt ohne Buchung ab.
/// </summary>
/// <param name="Date">Der Tag der Buchung; ohne Angabe heute oder der Fälligkeitstag, wenn er schon vorbei ist.</param>
/// <param name="AccountId">Das Konto der Buchung; ohne Angabe das des Postens, sonst das Standardkonto.</param>
public sealed record TickPositionRequest(decimal? Amount = null, DateOnly? Date = null, Guid? AccountId = null);

/// <summary>Ein Unterschied, den die Person übernehmen will: über den Posten, bei einem neuen Vertrag über den Vertrag.</summary>
public sealed record PlanChangeItem(Guid? PositionId, Guid? CommitmentId);

/// <summary>
/// Unterschiede übernehmen (POST .../changes/apply). Ohne Liste alle, die nicht geschützt sind;
/// mit Liste genau diese, auch geschützte.
/// </summary>
public sealed record ApplyPlanChangesRequest(IReadOnlyList<PlanChangeItem>? Items = null);

/// <summary>
/// Der echte Stand eines Kontos am letzten Tag des Vormonats. Ohne Betrag (Feld fehlt) lehnt die API die Anfrage ab,
/// statt 0 anzunehmen und das ganze Konto auszugleichen.
/// </summary>
public sealed record RealBalance(Guid AccountId, decimal? Balance);

/// <summary>
/// Der Übertrag in einen Monat (POST .../carry-over): der Betrag, der wirklich übrig ist, und
/// freiwillig die echten Kontostände; jeder Unterschied zum gerechneten Stand wird eine Ausgleichsbuchung.
/// </summary>
public sealed record CreateCarryOverRequest(decimal Amount, IReadOnlyList<RealBalance>? Balances = null);

/// <summary>Die Zielquoten einer Person (GET und PUT /api/v1/users/me/quotas), ganze Prozent mit Summe 100.</summary>
public sealed record QuotaSettings(int Needs, int Wants, int Savings);

/// <summary>Die Regeln für Pläne. Die API prüft sie, die Oberfläche kann sie schon beim Tippen anzeigen.</summary>
public static class PlanRules
{
    /// <summary>Die größte erlaubte Länge der Bezeichnung eines einmaligen Postens.</summary>
    public const int LabelMaxLength = 100;

    /// <summary>Der erste und der letzte Monat, den die API anlegt.</summary>
    public const int FirstYear = 2000;
    public const int LastYear = 2100;
}
