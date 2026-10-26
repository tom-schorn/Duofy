using System.Reflection.Metadata.Ecma335;

namespace Duofy.Domain;

/// <summary>
/// Der Monatsplan: alles, was eine Person in einem bestimmten Monat an Zahlungen
/// und Einnahmen erwartet. Er besteht aus Posten, siehe <see cref="PlanPosition"/>.
///
/// Ein Plan wird nicht von Hand zusammengestellt, sondern aus den Verträgen
/// erzeugt, siehe <see cref="CreateFrom"/>.
/// </summary>
public class Plan
{
    /// <summary>Die eindeutige Kennung; die API vergibt sie beim Anlegen des Monats.</summary>
    public Guid Id { get; set; }

    /// <summary>Die Person, der der Plan gehört. Jede Person hat je Monat höchstens einen Plan.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>
    /// Die Zielquote für Grundbedarf in Prozent. Ein neuer Monat übernimmt die Quoten aus den
    /// Einstellungen der Person; spätere Änderungen dort ändern alte Monate nicht.
    /// </summary>
    public int QuotaNeeds { get; set; } = Quotas.Default.Needs;

    /// <summary>Die Zielquote für Wünsche in Prozent.</summary>
    public int QuotaWants { get; set; } = Quotas.Default.Wants;

    /// <summary>Die Zielquote für Sparen in Prozent.</summary>
    public int QuotaSavings { get; set; } = Quotas.Default.Savings;

    /// <summary>Die drei Zielquoten zusammen; liest und schreibt die drei Felder darüber.</summary>
    public Quotas Quotas
    {
        get => new(QuotaNeeds, QuotaWants, QuotaSavings);
        set => (QuotaNeeds, QuotaWants, QuotaSavings) = (value.Needs, value.Wants, value.Savings);
    }

    /// <summary>Wann der Monat angelegt wurde.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Der Übertrag in diesen Monat besteht aus zwei Buchungen, die sich im Kontostand aufheben:
    /// dieser Einnahme und der Ausgabe in <see cref="CarryOverExpenseBookingId"/>. Bei einem positiven
    /// Rest ist die Einnahme "Übertrag aus ..." am Ersten des Monats und die Ausgabe "Übertrag nach ..."
    /// am Letzten des Vormonats, bei einem negativen umgekehrt. Null, solange es keinen Übertrag gibt.
    /// </summary>
    public Guid? CarryOverIncomeBookingId { get; set; }

    /// <summary>Die Ausgabe des Übertrags; siehe <see cref="CarryOverIncomeBookingId"/>.</summary>
    public Guid? CarryOverExpenseBookingId { get; set; }

    /// <summary>True, wenn es für diesen Monat schon einen Übertrag gibt (höchstens einer je Monat).</summary>
    public bool HasCarryOver => CarryOverIncomeBookingId is not null || CarryOverExpenseBookingId is not null;

    /// <summary>Das Jahr, für das der Plan gilt, zum Beispiel 2026.</summary>
    public required int Year { get; set; }

    /// <summary>Der Monat, für den der Plan gilt, als Zahl von 1 (Januar) bis 12 (Dezember).</summary>
    public required int Month { get; set; }

    /// <summary>
    /// Die Posten des Monats, sortiert nach dem Tag, an dem sie fällig sind.
    /// So steht oben, was zuerst bezahlt werden muss.
    /// </summary>
    public List<PlanPosition> Positions { get; set; } = new();

    /// <summary>
    /// Erzeugt den Plan für einen Monat aus einer Liste von Verträgen.
    ///
    /// Für jeden Vertrag, der in diesem Monat fällig ist, entsteht ein Posten.
    /// Verträge, die in diesem Monat nicht fällig sind, werden übersprungen.
    /// Die Posten sind Kopien: Spätere Änderungen am Vertrag ändern den Plan nicht.
    /// Sie übernehmen alle Felder, die der Plan kennt, siehe <see cref="PlanPosition.CopyOf"/>.
    ///
    /// Ein Monat außerhalb von 1 bis 12 ist ein Fehler des Aufrufers und führt zu
    /// einer <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    public static Plan CreateFrom(IEnumerable<Commitment> commitments, int year, int month)
    {
        if (month < 1 || month > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month));
        }
           
        Plan newPlan = new Plan
        { Year = year, Month = month };
            foreach (var commitment in commitments)
            {
                if (commitment.IsDueIn(year, month))
                {

                    newPlan.Positions.Add(PlanPosition.CopyOf(commitment, year, month));
                }
            }
            newPlan.SortPositions();
        return newPlan;
    }

    /// <summary>
    /// Sortiert die Posten nach dem Fälligkeitstag und bei gleichem Tag nach der Bezeichnung.
    /// So steht jeder Posten immer an derselben Stelle, auch nach einem Abgleich.
    /// </summary>
    public void SortPositions() =>
        Positions.Sort((a, b) =>
        {
            var byDay = a.DueDay.CompareTo(b.DueDay);
            return byDay != 0 ? byDay : string.Compare(a.Label, b.Label, StringComparison.CurrentCultureIgnoreCase);
        });
}
