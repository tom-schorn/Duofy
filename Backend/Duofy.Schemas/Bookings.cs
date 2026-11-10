namespace Duofy.Schemas;

/// <summary>Die Art einer Buchung in der API: Ausgabe, Einnahme oder Umbuchung.</summary>
public static class BookingKinds
{
    public const string Expense = "expense";
    public const string Income = "income";
    public const string Transfer = "transfer";

    public static readonly IReadOnlyList<string> All = [Expense, Income, Transfer];
}

/// <summary>Eine Zeile einer neuen oder geänderten Buchung: Betrag, Topf, Kategorie und freiwillig der Posten, den sie füllt.</summary>
/// <param name="PositionId">Ein Posten im Planmonat der Buchung, dessen Topf zur Zeile passt; null heißt: keinem Posten zugeordnet.</param>
public sealed record SaveBookingLine(decimal Amount, string? Pot, Guid? CategoryId, Guid? PositionId = null, Guid? SavingsGoalId = null, Guid? DebtId = null);

/// <summary>
/// Eine neue oder geänderte Buchung (POST und PUT /api/v1/bookings).
/// Der Betrag ist positiv; die Richtung ergibt sich aus der Art und den Töpfen der Zeilen.
/// </summary>
/// <param name="Kind">"expense", "income" oder "transfer", siehe <see cref="BookingKinds"/>.</param>
/// <param name="AccountId">Das Konto; bei einer Umbuchung das Konto, von dem das Geld abgeht.</param>
/// <param name="CounterAccountId">Nur bei einer Umbuchung: das Konto, auf das das Geld geht.</param>
/// <param name="Date">Der Tag der Buchung.</param>
/// <param name="Amount">Der Gesamtbetrag in Euro, größer als null.</param>
/// <param name="Note">Eine freie Notiz, höchstens <see cref="BookingRules.NoteMaxLength"/> Zeichen.</param>
/// <param name="Lines">
/// Bei Ausgabe und Einnahme 1 bis <see cref="BookingRules.MaxLines"/> Zeilen, deren Beträge zusammen den
/// Gesamtbetrag ergeben. Bei einer Umbuchung genau eine Zeile über den ganzen Betrag ohne Kategorie; ihr Topf
/// ist der Quelltopf (Spec 72, R9).
/// </param>
/// <param name="PlanYear">Das Jahr des Planmonats; zusammen mit <paramref name="PlanMonth"/> freiwillig.</param>
/// <param name="PlanMonth">
/// Der Planmonat: der Monat, in dessen Plan die Buchung zählt. Ohne Angabe der Monat des Datums;
/// erlaubt sind dieser, der davor und der danach. Hat eine Zeile einen Posten, gilt dessen Monat.
/// </param>
/// <param name="OwnerId">
/// Nur beim Anlegen: für wen die Buchung ist (Spec 68); leer heißt: für die angemeldete Person. Beim Ändern
/// bleibt die Buchung bei ihrem Besitzer.
/// </param>
/// <param name="TargetPot">
/// Nur bei einer Umbuchung und dort Pflicht: der Zieltopf ("needs", "wants" oder "savings"). Sind Quelle und Ziel
/// verschieden, verschiebt die Umbuchung den Betrag zwischen den Töpfen (gebuchte Summe und Quoten-Ist, Spec 112).
/// </param>
public sealed record SaveBookingRequest(
    string? Kind,
    Guid AccountId,
    Guid? CounterAccountId,
    DateOnly Date,
    decimal Amount,
    string? Note,
    IReadOnlyList<SaveBookingLine>? Lines,
    int? PlanYear = null,
    int? PlanMonth = null,
    Guid? OwnerId = null,
    string? TargetPot = null);

/// <summary>Eine gespeicherte Zeile einer Buchung.</summary>
public sealed record BookingLineResponse(Guid Id, decimal Amount, string Pot, Guid? CategoryId, Guid? PositionId = null, Guid? SavingsGoalId = null, Guid? DebtId = null);

/// <summary>Eine gespeicherte Buchung.</summary>
public sealed record BookingResponse(
    Guid Id,
    string Kind,
    Guid AccountId,
    Guid? CounterAccountId,
    DateOnly Date,
    decimal Amount,
    string? Note,
    IReadOnlyList<BookingLineResponse> Lines,
    int PlanYear = 0,
    int PlanMonth = 0,
    string? TargetPot = null);

/// <summary>
/// Die Antwort auf GET /api/v1/bookings?year=&amp;month=&amp;accountId=: die Buchungen des Monats,
/// die neueste zuerst, und die beiden Summen oben auf der Seite.
/// </summary>
public sealed record BookingMonthResponse(IReadOnlyList<BookingResponse> Bookings, decimal Income, decimal Expenses);

/// <summary>Die Regeln für Buchungen. Die API prüft sie, die Oberfläche kann sie schon beim Tippen anzeigen.</summary>
public static class BookingRules
{
    /// <summary>Die größte erlaubte Länge der Notiz.</summary>
    public const int NoteMaxLength = 200;

    /// <summary>Die größte Zahl an Zeilen einer Buchung.</summary>
    public const int MaxLines = 20;

    /// <summary>Der größte erlaubte Betrag (Spalte numeric(12,2)).</summary>
    public const decimal MaxAmount = 9_999_999_999.99m;
}
