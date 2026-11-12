namespace Duofy.Domain;

/// <summary>
/// Ein Vertrag: eine Zahlung oder Einnahme, die sich regelmäßig wiederholt,
/// zum Beispiel die Miete, das Gehalt oder ein Streaming-Abo.
///
/// Aus den Verträgen entsteht der Monatsplan. Für jeden Monat wird gefragt,
/// welche Verträge in diesem Monat fällig sind, und für jeden fälligen Vertrag
/// kommt eine Zeile in den Plan.
///
/// Ein Vertrag steht für genau eine Abbuchung. Wer bei einem Anbieter Strom
/// und Gas getrennt bezahlt, legt dafür zwei Verträge an.
/// </summary>
public class Commitment
{
    /// <summary>
    /// Die eindeutige Kennung dieses Vertrags. Sie ist der Primärschlüssel der Tabelle.
    /// Beim Speichern eines neuen Vertrags vergibt die Datenbankschicht sie von selbst.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Die Kennung des Benutzers, dem dieser Vertrag gehört. Duofy ist ein gehosteter
    /// Dienst, die Verträge aller Kunden liegen in derselben Tabelle. An diesem Feld
    /// erkennt man, wessen Vertrag es ist.
    /// </summary>
    public required Guid OwnerId { get; set; }

    /// <summary>Die Bezeichnung, die im Plan angezeigt wird, zum Beispiel "Miete".</summary>
    public required string Name { get; set; }

    /// <summary>
    /// Der Betrag einer einzelnen Zahlung in Euro.
    ///
    /// Geldbeträge sind im ganzen Projekt vom Typ decimal und nie double oder float.
    /// Der Grund: double rechnet mit Rundungsfehlern, dort ergibt 0,1 + 0,2 nicht
    /// genau 0,3. Bei decimal stimmt die Rechnung auf den Cent.
    /// </summary>
    public required decimal Amount { get; set; }

    /// <summary>Die Art des Vertrags, zum Beispiel Ausgabe, Sparziel, Schuld oder Einnahme.</summary>
    public required CommitmentType Type { get; set; }

    /// <summary>
    /// Der Abstand zwischen zwei Zahlungen in Monaten.
    ///
    /// Beispiele: 1 heißt jeden Monat, 3 heißt alle drei Monate (vierteljährlich),
    /// 12 heißt einmal im Jahr. Gezählt wird ab dem Monat der ersten Zahlung,
    /// siehe <see cref="FirstDueDate"/>.
    /// </summary>
    public required int IntervalMonths { get; set; }

    /// <summary>
    /// Das Datum der ersten Zahlung.
    ///
    /// Aus diesem einen Datum ergeben sich drei Dinge:
    /// - Vor diesem Monat gibt es den Vertrag noch nicht.
    /// - Der Monat ist der Startpunkt, ab dem der Abstand gezählt wird. Erste
    ///   Zahlung im Februar und Abstand 3 ergibt Februar, Mai, August, November.
    /// - Der Tag ist der Zahltag in jedem späteren Monat. Erste Zahlung am
    ///   15. Februar heißt: immer am 15.
    /// </summary>
    public required DateOnly FirstDueDate { get; set; }

    /// <summary>
    /// Das Datum der letzten Zahlung. Ist das Feld leer (null), läuft der Vertrag
    /// ohne Ende weiter.
    ///
    /// Gemeint ist der Tag, an dem zum letzten Mal Geld fließt, und nicht der Tag,
    /// an dem der Vertrag rechtlich endet. Das ist Absicht: Ein Fitnessstudio bucht
    /// im Voraus ab, ein Gehalt kommt im Nachhinein. Am Tag des Vertragsendes
    /// fließt deshalb mal noch Geld und mal nicht, und das Programm kann den
    /// Unterschied nicht erkennen. Die letzte Zahlung ist dagegen immer eindeutig.
    ///
    /// Eine Zahlung genau an diesem Tag zählt noch mit.
    /// </summary>
    public DateOnly? LastDueDate { get; set; }

    /// <summary>
    /// Die Kategorie, wie bei einer Buchung: eine eingebaute oder eine eigene der Person,
    /// siehe <see cref="Category"/>. Die API setzt sie immer.
    /// </summary>
    public Guid CategoryId { get; set; }

    /// <summary>
    /// Der Topf, in dem der Vertrag im Plan steht. Sparziel und Schuld liegen immer im Topf
    /// Sparen, eine Einnahme immer im Topf Einnahmen (siehe <see cref="FixedPotFor"/>);
    /// nur bei einem gewöhnlichen Vertrag wählt die Person zwischen Grundbedarf und Wünschen.
    /// </summary>
    public Pot Pot { get; set; }

    /// <summary>
    /// Das Konto, von dem gezahlt wird (bei einer Einnahme: auf das das Geld kommt). Leer (null)
    /// heißt: das Standardkonto, und zwar das, das am Tag der Zahlung Standard ist.
    /// </summary>
    public Guid? AccountId { get; set; }

    /// <summary>
    /// Ein eigenes Zielkonto. Ist es gesetzt, ist jede Zahlung eine Umbuchung zwischen zwei
    /// eigenen Konten, zum Beispiel die Sparrate vom Girokonto aufs Sparkonto. Das Geld
    /// verlässt den Haushalt dann nicht.
    /// </summary>
    public Guid? CounterAccountId { get; set; }

    /// <summary>
    /// Durchlaufender Posten: Das Geld bewegt den Kontostand, zählt aber in keinem Budget und
    /// keiner Quote. Gedacht für Geld, das nur weitergereicht wird.
    /// </summary>
    public bool PassThrough { get; set; }

    /// <summary>Wie das Geld fließt, zum Beispiel per Lastschrift oder Dauerauftrag.</summary>
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Other;

    /// <summary>Nur bei einem Sparziel: der Betrag, der am Ende zusammenkommen soll. Freiwillig.</summary>
    public decimal? TargetAmount { get; set; }

    /// <summary>Nur bei einem Sparziel: der Tag, bis zu dem das Ziel erreicht sein soll. Freiwillig.</summary>
    public DateOnly? TargetDate { get; set; }

    /// <summary>
    /// Das Sparziel, dem die Rate zufließt (Issue 32); null, wenn der Vertrag keinem Ziel zugeordnet ist. Ist das Ziel
    /// erreicht, verlangt der Plan die Rate nicht mehr (Issue 30).
    /// </summary>
    public Guid? SavingsGoalId { get; set; }

    /// <summary>
    /// Die Schuld oder der Kredit, den die Rate zurückzahlt (Issue 31); null, wenn keiner. Ist alles gezahlt, verlangt der
    /// Plan die Rate nicht mehr.
    /// </summary>
    public Guid? DebtId { get; set; }

    /// <summary>
    /// Der Betrag der letzten Zahlung, wenn er vom Betrag der übrigen abweicht (Issue 137), zum Beispiel die kleinere letzte
    /// Rate eines Ratenplans. Er gilt nur im Monat der <see cref="LastDueDate"/>; leer heißt: überall <see cref="Amount"/>.
    /// </summary>
    public decimal? LastAmount { get; set; }

    /// <summary>
    /// True, wenn der <see cref="LastAmount"/> von Hand gesetzt wurde (Issue 137); false, wenn Duofy ihn aus dem offenen Betrag
    /// gerechnet hat. Nur ein von Hand gesetzter Betrag wird bei einer Planänderung wieder vorbelegt.
    /// </summary>
    public bool LastAmountByHand { get; set; }

    /// <summary>
    /// True, wenn Duofy den Vertrag als Ratenplan einer Schuld angelegt hat (Issue 137), auch wenn er inzwischen durch einen
    /// neueren Plan abgelöst und beendet ist; er wird nur an der Schuld geändert und zeigt "gehört zu". Ein bestehender, nur
    /// verknüpfter Vertrag ist es nicht.
    /// </summary>
    public bool IsDebtPlan { get; set; }

    /// <summary>Wann der Vertrag angelegt wurde.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Wann der Vertrag zuletzt geändert wurde.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Der Betrag der Zahlung in diesem Monat: im Monat der letzten Zahlung der <see cref="LastAmount"/>, sofern gesetzt,
    /// sonst der <see cref="Amount"/>.
    /// </summary>
    public decimal AmountIn(int year, int month) =>
        LastAmount is { } last && LastDueDate is { } end && end.Year == year && end.Month == month ? last : Amount;

    /// <summary>
    /// Der Topf, den die Art fest vorgibt: Sparen für Sparziel und Schuld, Einnahmen für eine
    /// Einnahme. Bei einem gewöhnlichen Vertrag gibt die Art keinen vor (null).
    /// </summary>
    public static Pot? FixedPotFor(CommitmentType type) => type switch
    {
        CommitmentType.SavingsGoal or CommitmentType.Debt => Pot.Savings,
        CommitmentType.Income => Pot.Income,
        _ => null,
    };

    /// <summary>
    /// Beantwortet die Frage: Kommt in diesem Monat eine Zahlung aus diesem Vertrag?
    ///
    /// Die Antwort ist nur dann ja, wenn alle drei Bedingungen erfüllt sind:
    /// 1. Der Monat liegt nicht vor der ersten Zahlung.
    /// 2. Die Zahlung dieses Monats liegt nicht nach der letzten Zahlung.
    /// 3. Der Monat trifft den Abstand. Bei Abstand 3 ab Februar ist das der Mai,
    ///    aber nicht der März oder April.
    /// </summary>
    public bool IsDueIn(int year, int month)
    {
        // Jahr und Monat werden zu einer einzigen fortlaufenden Zahl zusammengefasst:
        // Jahr mal 12 plus Monat. Dezember 2026 ergibt 24324, Januar 2027 ergibt 24325.
        // Mit dieser Zahl lassen sich zwei Monate einfach vergleichen und voneinander
        // abziehen, auch wenn ein Jahreswechsel dazwischen liegt.
        int asked = year * 12 + month;
        int start = FirstDueDate.Year * 12 + FirstDueDate.Month;

        // Bedingung 1: Vor der ersten Zahlung gibt es den Vertrag noch nicht.
        if (asked < start)
        {
            return false;
        }

        // Bedingung 2 gilt nur für Verträge, die ein Ende haben.
        if (LastDueDate is not null)
        {
            int end = LastDueDate.Value.Year * 12 + LastDueDate.Value.Month;

            // Der gefragte Monat liegt komplett nach der letzten Zahlung.
            if (asked > end)
            {
                return false;
            }

            // Der gefragte Monat ist der Monat der letzten Zahlung. Hier entscheidet
            // der Tag: Ist der Zahltag der 15., die letzte Zahlung aber schon am 10.,
            // dann kommt am 15. kein Geld mehr.
            if (asked == end && EffectiveDueDay(year, month) > LastDueDate.Value.Day)
            {
                return false;
            }
        }

        // Bedingung 3: Die Zahl der Monate seit der ersten Zahlung muss sich ohne
        // Rest durch den Abstand teilen lassen. Von Februar bis Mai sind es 3 Monate,
        // 3 geteilt durch Abstand 3 hat Rest 0, also fällig. Von Februar bis April
        // sind es 2 Monate, Rest 2, also nicht fällig.
        int distance = asked - start;
        return distance % IntervalMonths == 0;
    }

    /// <summary>
    /// Liefert den Tag, an dem die Zahlung in diesem Monat fällig ist.
    ///
    /// Normalerweise ist das der Tag der ersten Zahlung. Gibt es diesen Tag im
    /// Monat nicht, wird der letzte Tag des Monats genommen: Ein Vertrag mit
    /// Zahltag 31 ist im Februar am 28. fällig, im Schaltjahr am 29.
    ///
    /// Die Zahlung fällt also nie aus und rutscht auch nicht in den nächsten Monat.
    /// </summary>
    public int EffectiveDueDay(int year, int month)
    {
        if (FirstDueDate.Day <= DateTime.DaysInMonth(year, month))
        {
            return FirstDueDate.Day;
        }
        return DateTime.DaysInMonth(year, month);
    }

    /// <summary>
    /// Beantwortet die Frage: Läuft der Vertrag an diesem Tag noch?
    ///
    /// Ein Vertrag ohne letzte Zahlung läuft immer. Ein Vertrag mit letzter Zahlung
    /// läuft bis einschließlich dieses Tages und danach nicht mehr.
    ///
    /// Die Vertragsliste nutzt das, um beendete Verträge auszublenden.
    /// </summary>
    public bool RunsOn(DateOnly today)
    {
        if (LastDueDate.HasValue)
        {
            DateOnly end = LastDueDate.Value;
            if (end >= today)
            {
                return true;
            }
        }
        else
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Liefert den Tag der letzten Zahlung an diesem Tag oder davor, oder null, wenn es bis
    /// dahin noch keine gab. Der Knopf "Beenden" setzt damit die letzte Zahlung.
    ///
    /// Beispiel: monatlich ab 1. Januar, gefragt am 23. Oktober, ergibt den 1. Oktober.
    /// Eine Zahlung genau an diesem Tag zählt mit.
    ///
    /// Gesucht wird Monat für Monat rückwärts bis zur ersten Zahlung; ob ein Monat zählt,
    /// entscheiden <see cref="IsDueIn"/> und <see cref="EffectiveDueDay"/>.
    /// </summary>
    public DateOnly? LastPaymentOnOrBefore(DateOnly day)
    {
        var firstMonth = new DateOnly(FirstDueDate.Year, FirstDueDate.Month, 1);
        for (var month = new DateOnly(day.Year, day.Month, 1); month >= firstMonth; month = month.AddMonths(-1))
        {
            if (DueDateIn(month) is { } due && due <= day)
            {
                return due;
            }
        }
        return null;
    }

    /// <summary>
    /// Liefert den Tag der nächsten Zahlung an diesem Tag oder danach, oder null, wenn keine
    /// mehr kommt. Die Vertragsliste sortiert danach.
    ///
    /// Gesucht wird Monat für Monat vorwärts: bis zum Monat der letzten Zahlung, bei einem
    /// Vertrag ohne Ende höchstens einen ganzen Abstand weit, denn darin liegt immer eine Zahlung.
    /// </summary>
    public DateOnly? NextPaymentOnOrAfter(DateOnly day)
    {
        var start = new DateOnly(day.Year, day.Month, 1);
        var firstMonth = new DateOnly(FirstDueDate.Year, FirstDueDate.Month, 1);
        if (start < firstMonth)
        {
            start = firstMonth;
        }

        var limit = LastDueDate is { } last
            ? new DateOnly(last.Year, last.Month, 1)
            : start.AddMonths(IntervalMonths);
        for (var month = start; month <= limit; month = month.AddMonths(1))
        {
            if (DueDateIn(month) is { } due && due >= day)
            {
                return due;
            }
        }
        return null;
    }

    /// <summary>Der Zahltag im Monat, der mit <paramref name="month"/> beginnt, oder null, wenn der Vertrag dort nicht fällig ist.</summary>
    private DateOnly? DueDateIn(DateOnly month) =>
        IsDueIn(month.Year, month.Month)
            ? new DateOnly(month.Year, month.Month, EffectiveDueDay(month.Year, month.Month))
            : null;
}
