namespace Duofy.Schemas;

/// <summary>Ein Posten im Haushaltsplan, mit der Person, die ihn trägt (Spec 69, R3).</summary>
/// <param name="Type">Die Art wie beim Vertrag, siehe <see cref="CommitmentTypes"/>.</param>
/// <param name="Pot">Siehe <see cref="PotKeys"/>.</param>
/// <param name="Booked">Die Summe der Buchungszeilen, die dem Posten zugeordnet sind.</param>
/// <param name="DoneAt">Wann der Posten abgehakt wurde; null, solange er offen ist.</param>
/// <param name="OwnerId">Wem der Plan gehört, in dem der Posten steht.</param>
/// <param name="OwnerFirstName">Der Vorname dieser Person.</param>
public sealed record HouseholdPlanPositionResponse(
    Guid Id,
    string Label,
    decimal AmountPlanned,
    int DueDay,
    string Type,
    Guid CategoryId,
    string Pot,
    bool PassThrough,
    decimal Booked,
    DateTimeOffset? DoneAt,
    Guid OwnerId,
    string OwnerFirstName);

/// <summary>Geplant und gebucht in einem Topf, ohne durchlaufende Posten.</summary>
/// <param name="Pot">"needs", "wants" oder "savings".</param>
public sealed record HouseholdPotResponse(string Pot, decimal Planned, decimal Booked);

/// <summary>Die Buchungen des Planmonats ohne Posten, je Topf; Umbuchungen und der Übertrag zählen nicht.</summary>
public sealed record HouseholdUnplannedResponse(decimal Income, decimal Needs, decimal Wants, decimal Savings);

/// <summary>Ein Konto eines Mitglieds mit dem gerechneten Stand am letzten Tag des Monats (oder heute, wenn der Monat noch läuft).</summary>
/// <param name="OwnerFirstName">Der Vorname der Person, der das Konto gehört.</param>
public sealed record HouseholdAccountResponse(Guid AccountId, Guid OwnerId, string OwnerFirstName, string Name, bool IsDefault, decimal Balance);

/// <summary>
/// Der Haushaltsplan eines Monats (GET /api/v1/households/{id}/plans/{year}/{month}); nur zum Lesen und nirgends
/// gespeichert, bei jedem Lesen aus den Monaten der gezählten Mitglieder zusammengesetzt (Spec 69).
/// </summary>
/// <param name="Positions">Alle Posten aller gezählten Mitglieder nach Topf, Fälligkeitstag und Name.</param>
/// <param name="Income">Die geplanten Einnahmen ohne durchlaufende Posten.</param>
/// <param name="Pots">Geplant und gebucht je Topf für Grundbedarf, Wünsche und Sparen.</param>
/// <param name="Unpaid">Was noch hinausgeht; abgehakte, durchlaufende Posten und Einnahmen zählen nicht, nie negativ.</param>
/// <param name="Quotas">Die Zielquoten des Haushalts und das Ist aller gezählten Mitglieder.</param>
/// <param name="Accounts">Die offenen Konten der Mitglieder, die sie dem Betrachter mindestens zum Ansehen freigegeben haben; die eigenen immer.</param>
/// <param name="AccountsMissing">Die Vornamen derer, deren Konten fehlen, weil sie sie nicht freigegeben haben.</param>
/// <param name="Members">Je gezähltem Mitglied Einnahmen und Quoten gegen den Richtwert des Haushalts; alle sehen alle (Spec 27).</param>
/// <param name="Fixed">Die geplanten Ausgaben aller gezählten Mitglieder ohne durchlaufende Posten (Spec 24).</param>
/// <param name="Free">Einnahmen minus Festes über alle gezählten Mitglieder (Spec 24) plus die Überträge derer, deren Buchungen in <paramref name="Bookings"/> zu sehen sind (Spec 66, R9); hängt also vom Betrachter ab.</param>
/// <param name="Bookings">Die Buchungen mit diesem Planmonat der gezählten Mitglieder, die ihre Buchungen dem Betrachter mindestens zum Ansehen freigegeben haben (die eigenen immer), die neueste zuerst (Spec 24).</param>
/// <param name="BookingsMissing">Die Vornamen derer, deren Buchungen fehlen, weil sie sie nicht freigegeben haben.</param>
/// <param name="CarryOvers">Die Überträge als eigene Zeilen, nur von denen, deren Buchungen in <paramref name="Bookings"/> zu sehen sind (Spec 66, R9).</param>
public sealed record HouseholdPlanResponse(
    Guid HouseholdId,
    string HouseholdName,
    int Year,
    int Month,
    IReadOnlyList<HouseholdPlanPositionResponse> Positions,
    decimal Income,
    IReadOnlyList<HouseholdPotResponse> Pots,
    decimal Unpaid,
    IReadOnlyList<PlanQuotaResponse> Quotas,
    HouseholdUnplannedResponse Unplanned,
    IReadOnlyList<HouseholdAccountResponse> Accounts,
    IReadOnlyList<string> AccountsMissing,
    IReadOnlyList<HouseholdMemberResponse> Members,
    decimal Fixed = 0m,
    decimal Free = 0m,
    IReadOnlyList<HouseholdBookingResponse>? Bookings = null,
    IReadOnlyList<string>? BookingsMissing = null,
    IReadOnlyList<PlanCarryOverLineResponse>? CarryOvers = null);

/// <summary>Die Quote eines Mitglieds in einem Topf gegen den Richtwert des Haushalts (Spec 27).</summary>
/// <param name="Pot">"needs", "wants" oder "savings".</param>
/// <param name="Target">Die Zielquote des Haushalts in ganzen Prozent, für alle gleich.</param>
/// <param name="Actual">Das Ist des Mitglieds in Prozent mit einer Nachkommastelle, gerechnet wie das des Haushalts.</param>
/// <param name="Deviation">Ist minus Ziel in Prozentpunkten mit einer Nachkommastelle; positiv heißt über dem Richtwert.</param>
public sealed record HouseholdMemberQuotaResponse(string Pot, int Target, decimal Actual, decimal Deviation);

/// <summary>Ein gezähltes Mitglied mit seinen Einnahmen und Quoten im Haushaltsplan (Spec 27).</summary>
/// <param name="Income">Die gebuchten Einnahmen des Mitglieds, durch die das Ist geteilt wird.</param>
/// <param name="HasIncome">False, wenn das Mitglied im Monat keine Einnahmen hat; dann ist jedes Ist 0.</param>
/// <param name="Quotas">Grundbedarf, Wünsche und Sparen in dieser Reihenfolge.</param>
public sealed record HouseholdMemberResponse(Guid UserId, string FirstName, decimal Income, bool HasIncome, IReadOnlyList<HouseholdMemberQuotaResponse> Quotas);

/// <summary>
/// Eine Buchung mit diesem Planmonat im Haushaltsplan (Spec 24), mit der Person, der sie gehört; ohne Notiz und Konto,
/// denn die Konten zeigt der Haushaltsplan nur mit Freigabe.
/// </summary>
/// <param name="Kind">Die Art, siehe <see cref="BookingKinds"/> (Spec 51, R9).</param>
/// <param name="Purpose">Wofür die Buchung war: der Posten, sonst die Kategorie der ersten Zeile; null bei einer Umbuchung ohne Posten (Spec 51, R10).</param>
/// <param name="Lines">Die Zeilen mit ihrem Posten, damit ein Posten seine Buchungen zeigen kann (Spec 51, R8).</param>
public sealed record HouseholdBookingResponse(
    Guid Id,
    DateOnly Date,
    decimal Amount,
    int PlanYear,
    int PlanMonth,
    Guid OwnerId,
    string OwnerFirstName,
    string Kind = BookingKinds.Expense,
    string? Purpose = null,
    IReadOnlyList<HouseholdBookingLineResponse>? Lines = null);

/// <summary>Eine Zeile einer Buchung im Haushaltsplan (Spec 51, R8).</summary>
/// <param name="PositionId">Der Posten, den die Zeile füllt; null heißt keiner.</param>
/// <param name="Amount">Der Betrag der Zeile.</param>
/// <param name="Category">Der Name der Kategorie; null ohne Kategorie.</param>
public sealed record HouseholdBookingLineResponse(Guid? PositionId, decimal Amount, string? Category);

/// <summary>Ein Monat in der Liste der Haushaltsmonate (GET /api/v1/households/{id}/plans).</summary>
/// <param name="Free">Der Frei-Betrag aller gezählten Mitglieder, gerechnet wie bei <see cref="PlanMonthResponse"/>; Überträge nur von denen, deren Buchungen der Betrachter sieht.</param>
/// <param name="CarryOver">Die Summe der in <paramref name="Free"/> enthaltenen Überträge; null, wenn keiner enthalten ist.</param>
public sealed record HouseholdPlanMonth(int Year, int Month, decimal Free, decimal? CarryOver = null);
