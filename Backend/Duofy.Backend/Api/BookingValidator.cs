using Duofy.Schemas;
using Duofy.Domain;

namespace Duofy.Api;

/// <summary>Wie die Prüfung einer Buchung ausgegangen ist.</summary>
public enum BookingCheckOutcome
{
    /// <summary>Alle Regeln sind erfüllt.</summary>
    Valid,

    /// <summary>Eine Regel ist verletzt; der Fehlercode steht in <see cref="BookingCheck.Code"/> (Antwort 400).</summary>
    Invalid,

    /// <summary>Ein Konto oder eine Kategorie gibt es nicht oder sie gehört einer anderen Person (Antwort 404).</summary>
    NotFound,
}

/// <summary>
/// Das Ergebnis der Prüfung einer Buchung. Bei einer gültigen Buchung steht der Planmonat darin,
/// in dem sie zählt: der des Postens ihrer Zeilen, sonst der gewählte oder der Monat des Datums.
/// </summary>
public readonly record struct BookingCheck(BookingCheckOutcome Outcome, string? Code, int PlanYear = 0, int PlanMonth = 0)
{
    public static BookingCheck Valid { get; } = new(BookingCheckOutcome.Valid, null);

    public static BookingCheck ValidIn(int planYear, int planMonth) => new(BookingCheckOutcome.Valid, null, planYear, planMonth);

    public static BookingCheck NotFound { get; } = new(BookingCheckOutcome.NotFound, null);

    public static BookingCheck Invalid(string code) => new(BookingCheckOutcome.Invalid, code);
}

/// <summary>
/// Die Regeln für das Anlegen und Ändern einer Buchung (R1 bis R6, R11, R13). Die Prüfung
/// geht von außen nach innen und liefert den ersten Fehler:
/// 1. Form: Art, Betrag, Notiz, Zeilen, Umbuchung. Das braucht keine Daten.
/// 2. Konten: gibt es sie und gehören sie der Person (sonst 404), sind sie noch offen.
/// 3. Kategorien: gibt es sie und darf die Person sie benutzen (sonst 404), passen sie zur Richtung.
/// 4. Posten und Planmonat (Spec 66, R6, R7): Jeder Posten einer Zeile gehört der Person (sonst
///    404), passt zur Art der Zeile und liegt im selben Monat; ohne Posten liegt der Planmonat
///    höchstens einen Monat neben dem Datum.
/// 5. Datum: nicht nach heute (Berlin), nach dem Starttag jedes beteiligten Kontos.
///
/// Fremde Kennungen kommen erst nach der Form dran. So verrät keine Antwort etwas über
/// Konten und Kategorien anderer Personen.
/// </summary>
public static class BookingValidator
{
    /// <summary>Prüft eine Buchung ohne Posten; eine Zeile mit Posten gilt dann als unbekannt (404).</summary>
    public static Task<BookingCheck> CheckAsync(
        SaveBookingRequest request,
        Guid ownerId,
        IAccountLookup accounts,
        ICategoryLookup categories,
        TimeProvider time,
        CancellationToken cancellationToken) =>
        CheckAsync(request, ownerId, accounts, categories, NoPositions.Instance, time, cancellationToken);

    public static async Task<BookingCheck> CheckAsync(
        SaveBookingRequest request,
        Guid ownerId,
        IAccountLookup accounts,
        ICategoryLookup categories,
        IPositionLookup positions,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (CheckShape(request) is { } shapeError)
        {
            return BookingCheck.Invalid(shapeError);
        }
        var isTransfer = request.Kind == BookingKinds.Transfer;

        var touched = new List<AccountInfo>();
        foreach (var accountId in isTransfer ? [request.AccountId, request.CounterAccountId!.Value] : new[] { request.AccountId })
        {
            if (await accounts.FindAsync(ownerId, accountId, cancellationToken) is not { } account)
            {
                return BookingCheck.NotFound;
            }
            touched.Add(account);
        }
        if (touched.Any(a => !a.Active))
        {
            return BookingCheck.Invalid(ErrorCodes.AccountClosed);
        }

        foreach (var line in request.Lines ?? [])
        {
            if (line.CategoryId is not { } categoryId)
            {
                continue;
            }
            if (await categories.FindAsync(ownerId, categoryId, cancellationToken) is not { } category)
            {
                return BookingCheck.NotFound;
            }
            // Einnahmen nur mit Einnahme-Kategorien, Ausgaben nur mit den anderen (R3).
            if (category.IsIncome != (request.Kind == BookingKinds.Income))
            {
                return BookingCheck.Invalid(ErrorCodes.LineCategoryInvalid);
            }
        }

        var planMonth = await CheckPlanMonthAsync(request, ownerId, positions, cancellationToken);
        if (planMonth.Outcome != BookingCheckOutcome.Valid)
        {
            return planMonth;
        }

        if (request.Date > Berlin.Today(time))
        {
            return BookingCheck.Invalid(ErrorCodes.DateInFuture);
        }
        if (touched.Any(a => request.Date <= a.OpeningDate))
        {
            return BookingCheck.Invalid(ErrorCodes.DateBeforeOpening);
        }

        return planMonth;
    }

    /// <summary>
    /// Der Planmonat (R7) und die Posten der Zeilen (R6). Einnahme-Zeilen füllen nur Einnahme-Posten,
    /// Ausgabe-Zeilen nur die übrigen, die Zeile einer Umbuchung Posten im Topf Sparen und jeden Posten
    /// mit eigenem Zielkonto (so wie das Abhaken ihn als Umbuchung bucht). Alle
    /// Posten liegen im selben Monat, und der ist der Planmonat; ohne Posten ist es der gewählte
    /// Monat oder der des Datums.
    /// </summary>
    private static async Task<BookingCheck> CheckPlanMonthAsync(
        SaveBookingRequest request, Guid ownerId, IPositionLookup positions, CancellationToken cancellationToken)
    {
        (int Year, int Month)? fromPositions = null;
        foreach (var line in request.Lines ?? [])
        {
            if (line.PositionId is not { } positionId)
            {
                continue;
            }
            if (await positions.FindAsync(ownerId, positionId, cancellationToken) is not { } position)
            {
                return BookingCheck.NotFound;
            }
            var fits = request.Kind switch
            {
                BookingKinds.Income => position.Pot == Pot.Income,
                BookingKinds.Transfer => position.Pot == Pot.Savings || position.CounterAccountId is not null,
                _ => position.Pot != Pot.Income,
            };
            if (!fits)
            {
                return BookingCheck.Invalid(ErrorCodes.LinePositionInvalid);
            }
            if (fromPositions is { } month && month != (position.PlanYear, position.PlanMonth))
            {
                return BookingCheck.Invalid(ErrorCodes.PlanMonthInvalid);
            }
            fromPositions = (position.PlanYear, position.PlanMonth);
        }

        var asked = request.PlanYear is { } year && request.PlanMonth is { } monthNumber ? (year, monthNumber) : ((int, int)?)null;
        if (fromPositions is { } positionMonth)
        {
            return asked is null || asked == positionMonth
                ? BookingCheck.ValidIn(positionMonth.Year, positionMonth.Month)
                : BookingCheck.Invalid(ErrorCodes.PlanMonthInvalid);
        }

        var (planYear, planMonth) = asked ?? (request.Date.Year, request.Date.Month);
        return Booking.IsPlanMonthAllowed(request.Date, planYear, planMonth)
            ? BookingCheck.ValidIn(planYear, planMonth)
            : BookingCheck.Invalid(ErrorCodes.PlanMonthInvalid);
    }

    /// <summary>Prüft alles, was sich ohne Blick in die Datenbank prüfen lässt.</summary>
    private static string? CheckShape(SaveBookingRequest request)
    {
        if (request.Kind is not (BookingKinds.Expense or BookingKinds.Income or BookingKinds.Transfer))
        {
            return ErrorCodes.KindInvalid;
        }
        if (!IsValidAmount(request.Amount))
        {
            return ErrorCodes.AmountInvalid;
        }
        if (request.Note is { Length: > BookingRules.NoteMaxLength })
        {
            return ErrorCodes.NoteTooLong;
        }
        // Der Planmonat kommt ganz oder gar nicht, und nur in den Jahren, die es als Plan geben kann. Die Grenze hält auch
        // den Vergleich in Booking.IsPlanMonthAllowed im Zahlenbereich.
        if ((request.PlanYear is null) != (request.PlanMonth is null)
            || request.PlanMonth is < 1 or > 12
            || request.PlanYear is < PlanRules.FirstYear or > PlanRules.LastYear)
        {
            return ErrorCodes.PlanMonthInvalid;
        }

        return request.Kind == BookingKinds.Transfer ? CheckTransfer(request) : CheckLines(request);
    }

    /// <summary>
    /// Eine Umbuchung hat zwei verschiedene Konten und genau eine Zeile ohne Kategorie über den ganzen Betrag.
    /// Der Topf der Zeile ist der Quelltopf, dazu kommt der Zieltopf (Spec 72, R9). Beide sind Pflicht, jeder aus
    /// Grundbedarf, Wünsche oder Sparen, und es darf derselbe sein. Verschiedene Töpfe verschieben den Betrag zwischen den Töpfen (Spec 112); hier wird nur geprüft.
    /// </summary>
    private static string? CheckTransfer(SaveBookingRequest request)
    {
        if (request.CounterAccountId is not { } counter)
        {
            return ErrorCodes.CounterAccountRequired;
        }
        if (counter == request.AccountId)
        {
            return ErrorCodes.TransferSameAccount;
        }

        var lines = request.Lines ?? [];
        if (lines.Count > 1)
        {
            return ErrorCodes.LinesTooMany;
        }
        if (lines.Count == 0 || lines[0].Pot is null || request.TargetPot is null)
        {
            return ErrorCodes.TransferPotsRequired;
        }

        var line = lines[0];
        if (!PotKey.TryParse(line.Pot, out var source) || source == Pot.Income
            || !PotKey.TryParse(request.TargetPot, out var target) || target == Pot.Income)
        {
            return ErrorCodes.TransferPotInvalid;
        }
        if (line.CategoryId is not null)
        {
            return ErrorCodes.TransferCategoryInvalid;
        }
        return line.Amount == request.Amount ? null : ErrorCodes.LinesSumMismatch;
    }

    /// <summary>
    /// Eine Ausgabe oder Einnahme hat 1 bis 20 Zeilen mit Betrag, Topf und Kategorie. Alle Zeilen
    /// einer Einnahme liegen im Topf Einnahmen, alle einer Ausgabe in einem der anderen drei;
    /// die Beträge ergeben zusammen genau den Gesamtbetrag.
    /// </summary>
    private static string? CheckLines(SaveBookingRequest request)
    {
        if (request.CounterAccountId is not null)
        {
            return ErrorCodes.CounterAccountUnexpected;
        }

        var lines = request.Lines ?? [];
        if (lines.Count == 0)
        {
            return ErrorCodes.LinesRequired;
        }
        if (lines.Count > BookingRules.MaxLines)
        {
            return ErrorCodes.LinesTooMany;
        }

        var isIncome = request.Kind == BookingKinds.Income;
        foreach (var line in lines)
        {
            if (!IsValidAmount(line.Amount))
            {
                return ErrorCodes.LineAmountInvalid;
            }
            if (line.Pot is null)
            {
                return ErrorCodes.PotRequired;
            }
            // Einnahme: nur der Topf Einnahmen. Ausgabe: Grundbedarf, Wünsche oder Sparen.
            if (!PotKey.TryParse(line.Pot, out var pot) || (pot == Pot.Income) != isIncome)
            {
                return ErrorCodes.LinePotInvalid;
            }
            if (line.CategoryId is null)
            {
                return ErrorCodes.CategoryRequired;
            }
        }

        return lines.Sum(l => l.Amount) == request.Amount ? null : ErrorCodes.LinesSumMismatch;
    }

    /// <summary>Größer als null, höchstens der größte Betrag der Spalte und höchstens zwei Nachkommastellen.</summary>
    private static bool IsValidAmount(decimal amount) =>
        amount > 0 && amount <= BookingRules.MaxAmount && decimal.Round(amount, 2) == amount;
}
