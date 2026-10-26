using Duofy.Schemas;

namespace Duofy.Shared.Services;

/// <summary>
/// Eine Zeile im Dialog, solange sie noch bearbeitet wird. Alle Felder dürfen leer sein;
/// ob die Buchung gültig ist, sagt erst die API.
/// </summary>
public sealed class LineDraft
{
    /// <summary>Der Betrag der Zeile; leer, solange nichts eingegeben ist.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Der gewählte Topf, siehe <see cref="PotKeys"/>.</summary>
    public string Pot { get; set; } = PotKeys.Needs;

    /// <summary>Die gewählte Kategorie.</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Der Posten des Planmonats, den die Zeile füllt; null heißt keiner.</summary>
    public Guid? PositionId { get; set; }
}

/// <summary>
/// Der Entwurf einer Buchung im Dialog. Die Klasse hält die Eingaben und kennt die
/// Rechenregeln des Dialogs, damit die Seite selbst schlicht bleibt und man die Regeln
/// ohne Oberfläche testen kann:
///
/// - Es gibt immer mindestens eine Zeile. Die erste Zeile enthält, was von den anderen übrig lässt:
///   Gesamtbetrag minus die Beträge der übrigen Zeilen. So stimmt die Summe schon beim Tippen.
/// - Eine Einnahme hat nur Zeilen im Topf Einnahmen, eine Ausgabe nur in den anderen.
/// - Eine Umbuchung hat keine Zeilen im Dialog; sie kann nur als "Sparen" markiert werden.
/// - Der Planmonat ist der Monat des Datums, der davor oder der danach (Spec 66, R7). Rückt das
///   Datum zu weit weg, wird er wieder der Monat des Datums. Posten gehören zu einem Planmonat;
///   wechselt er oder die Art, fallen die gewählten Posten weg.
/// </summary>
public sealed class BookingDraft
{
    private BookingDraft()
    {
    }

    /// <summary>"expense", "income" oder "transfer", siehe <see cref="BookingKinds"/>.</summary>
    public string Kind { get; private set; } = BookingKinds.Expense;

    /// <summary>Der Gesamtbetrag; leer, solange nichts eingegeben ist.</summary>
    public decimal? Amount { get; private set; }

    private DateOnly date;

    /// <summary>Der Tag der Buchung. Passt der Planmonat nicht mehr dazu, wird er der Monat dieses Tages.</summary>
    public DateOnly Date
    {
        get => date;
        set
        {
            date = value;
            if (!PlanMonthOptions.Contains(PlanMonth))
            {
                SetPlanMonth(MonthOf(value));
            }
        }
    }

    /// <summary>Der erste Tag des Planmonats: des Monats, in dessen Plan die Buchung zählt.</summary>
    public DateOnly PlanMonth { get; private set; }

    /// <summary>Die Planmonate zur Wahl: der Monat des Datums, der davor und der danach.</summary>
    public IReadOnlyList<DateOnly> PlanMonthOptions => [MonthOf(date).AddMonths(-1), MonthOf(date), MonthOf(date).AddMonths(1)];

    /// <summary>Nur bei einer Umbuchung, die gespart wird: der Spar-Posten, den sie füllt.</summary>
    public Guid? TransferPositionId { get; set; }

    /// <summary>Das Konto; bei einer Umbuchung das Konto, von dem das Geld abgeht.</summary>
    public Guid? AccountId { get; set; }

    /// <summary>Nur bei einer Umbuchung: das Konto, auf das das Geld geht.</summary>
    public Guid? CounterAccountId { get; set; }

    public string Note { get; set; } = "";

    /// <summary>Nur bei einer Umbuchung: Das Geld wird gespart (Topf Sparen).</summary>
    public bool TransferIsSavings { get; set; }

    /// <summary>Die Zeilen; die erste ist immer da und lässt sich nicht entfernen.</summary>
    public List<LineDraft> Lines { get; } = [new LineDraft()];

    /// <summary>True, solange noch eine Zeile dazukommen darf.</summary>
    public bool CanAddLine => Lines.Count < BookingRules.MaxLines;

    /// <summary>
    /// Der Entwurf für das Plus: Ausgabe, heutiges Datum, Standardkonto (R9). Gibt es kein
    /// Standardkonto, bleibt das Konto leer und muss gewählt werden.
    /// </summary>
    public static BookingDraft ForNew(DateOnly today, Guid? defaultAccountId)
    {
        var draft = new BookingDraft { AccountId = defaultAccountId, PlanMonth = MonthOf(today) };
        draft.Date = today;
        return draft;
    }

    /// <summary>Der Entwurf zum Ändern einer gespeicherten Buchung.</summary>
    public static BookingDraft From(BookingResponse booking)
    {
        var draft = new BookingDraft
        {
            Kind = booking.Kind,
            Amount = booking.Amount,
            AccountId = booking.AccountId,
            CounterAccountId = booking.CounterAccountId,
            Note = booking.Note ?? "",
            // Ältere Antworten ohne Planmonat zählen im Monat ihres Datums.
            PlanMonth = booking.PlanMonth is >= 1 and <= 12 ? new DateOnly(booking.PlanYear, booking.PlanMonth, 1) : MonthOf(booking.Date),
        };
        // Ein Planmonat mit Posten darf weiter weg liegen als einen Monat; er bleibt, wie er gespeichert ist.
        draft.date = booking.Date;

        if (booking.Kind == BookingKinds.Transfer)
        {
            draft.TransferIsSavings = booking.Lines.Any(l => l.Pot == PotKeys.Savings);
            draft.TransferPositionId = booking.Lines.FirstOrDefault()?.PositionId;
            return draft;
        }

        draft.Lines.Clear();
        draft.Lines.AddRange(booking.Lines.Select(l => new LineDraft
        {
            Amount = l.Amount, Pot = l.Pot, CategoryId = l.CategoryId, PositionId = l.PositionId,
        }));
        if (draft.Lines.Count == 0)
        {
            draft.Lines.Add(new LineDraft { Amount = booking.Amount });
        }
        return draft;
    }

    /// <summary>Setzt die Art. Alle Zeilen bekommen den Standardtopf der Art, die Kategorien entfallen (sie passen nur zu einer Art).</summary>
    public void SetKind(string kind)
    {
        if (kind == Kind)
        {
            return;
        }

        Kind = kind;
        foreach (var line in Lines)
        {
            line.Pot = kind == BookingKinds.Income ? PotKeys.Income : PotKeys.Needs;
            line.CategoryId = null;
            line.PositionId = null;
        }
        TransferPositionId = null;
        if (kind != BookingKinds.Transfer)
        {
            CounterAccountId = null;
            TransferIsSavings = false;
        }
    }

    /// <summary>Setzt den Gesamtbetrag. Die erste Zeile passt sich an.</summary>
    public void SetAmount(decimal? amount)
    {
        Amount = amount;
        Rebalance();
    }

    /// <summary>Hängt eine leere Zeile an. Sie ändert zunächst nichts an den Beträgen.</summary>
    public void AddLine()
    {
        if (!CanAddLine)
        {
            return;
        }
        Lines.Add(new LineDraft { Pot = Kind == BookingKinds.Income ? PotKeys.Income : PotKeys.Needs });
    }

    /// <summary>Entfernt eine Zeile außer der ersten. Ihr Betrag fällt an die erste Zeile zurück.</summary>
    public void RemoveLine(int index)
    {
        if (index <= 0 || index >= Lines.Count)
        {
            return;
        }
        Lines.RemoveAt(index);
        Rebalance();
    }

    /// <summary>Setzt den Betrag einer weiteren Zeile; die erste Zeile nimmt den Rest.</summary>
    public void SetLineAmount(int index, decimal? amount)
    {
        if (index <= 0 || index >= Lines.Count)
        {
            return;
        }
        Lines[index].Amount = amount;
        Rebalance();
    }

    /// <summary>
    /// Wählt die Kategorie einer Zeile und wählt deren vorgeschlagenen Topf vor.
    /// Die Person darf den Topf danach ändern.
    /// </summary>
    public void ChooseCategory(int index, CategoryResponse? category)
    {
        var line = Lines[index];
        line.CategoryId = category?.Id;
        if (category is not null && Kind == BookingKinds.Expense && category.SuggestedPot != PotKeys.Income)
        {
            line.Pot = category.SuggestedPot;
        }
    }

    /// <summary>Wählt den Planmonat. Ein anderer Monat hat andere Posten; die gewählten fallen weg.</summary>
    public void SetPlanMonth(DateOnly month)
    {
        var first = MonthOf(month);
        if (first == PlanMonth)
        {
            return;
        }
        PlanMonth = first;
        foreach (var line in Lines)
        {
            line.PositionId = null;
        }
        TransferPositionId = null;
    }

    /// <summary>
    /// Ordnet eine Zeile einem Posten zu, oder mit null keinem. Hat die Zeile noch keine Kategorie,
    /// übernimmt sie Kategorie und Topf des Postens.
    /// </summary>
    public void ChoosePosition(int index, PlanPositionResponse? position)
    {
        var line = Lines[index];
        line.PositionId = position?.Id;
        if (position is not null && line.CategoryId is null)
        {
            line.CategoryId = position.CategoryId;
            line.Pot = position.Pot;
        }
    }

    /// <summary>
    /// Macht aus dem Entwurf die Anfrage an die API. Ein fehlender Betrag wird zu 0; die API
    /// weist ihn mit <c>amount_invalid</c> ab, so gibt es die Regeln nur an einer Stelle.
    /// </summary>
    public SaveBookingRequest ToRequest()
    {
        var total = Amount ?? 0m;
        IReadOnlyList<SaveBookingLine> lines = Kind == BookingKinds.Transfer
            ? (TransferIsSavings ? [new SaveBookingLine(total, PotKeys.Savings, null, TransferPositionId)] : [])
            : Lines.Select(l => new SaveBookingLine(l.Amount ?? 0m, l.Pot, l.CategoryId, l.PositionId)).ToList();

        return new SaveBookingRequest(
            Kind,
            AccountId ?? Guid.Empty,
            Kind == BookingKinds.Transfer ? CounterAccountId : null,
            Date,
            total,
            string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
            lines,
            PlanMonth.Year,
            PlanMonth.Month);
    }

    private static DateOnly MonthOf(DateOnly day) => new(day.Year, day.Month, 1);

    /// <summary>Die erste Zeile bekommt den Gesamtbetrag minus die Beträge der übrigen Zeilen.</summary>
    private void Rebalance()
    {
        if (Amount is not { } total)
        {
            Lines[0].Amount = null;
            return;
        }
        Lines[0].Amount = total - Lines.Skip(1).Sum(l => l.Amount ?? 0m);
    }
}
