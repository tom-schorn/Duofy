namespace Duofy.Schemas;

/// <summary>Warum ein Ratenplan nicht berechnet werden kann.</summary>
public enum InstallmentPlanError
{
    /// <summary>Kein Fehler.</summary>
    None,

    /// <summary>Die Rate ist nicht größer als 0 oder hat mehr als zwei Nachkommastellen.</summary>
    RateInvalid,

    /// <summary>Der Rhythmus ist kein Abstand von 1 bis 120 Monaten.</summary>
    IntervalInvalid,

    /// <summary>Es ist nichts mehr offen, also gibt es nichts zu verteilen.</summary>
    NothingOpen,

    /// <summary>Die letzte Rate von Hand ist nicht größer als 0 oder hat mehr als zwei Nachkommastellen.</summary>
    LastAmountInvalid,

    /// <summary>Die letzte Rate von Hand passt nicht zum offenen Betrag: Sie ist nicht kleiner als er, oder die vollen Raten davor zahlen ihn schon ab.</summary>
    LastAmountDoesNotFit,

    /// <summary>Der Plan bräuchte mehr als <see cref="InstallmentPlan.MaxInstallments"/> Raten.</summary>
    TooManyInstallments,
}

/// <summary>Das Ergebnis der Ratenrechnung: Anzahl, letzte Rate und Tag der letzten Rate.</summary>
public readonly record struct InstallmentPlanResult(InstallmentPlanError Error, int Count, decimal LastAmount, DateOnly LastDueDate);

/// <summary>
/// Die Rechnung hinter dem Ratenplan einer Schuld (Issue 137); API und Oberfläche rechnen mit demselben Code. Alles wird in Cent gerechnet, damit die letzte Rate auf den
/// Cent stimmt: Anzahl n = aufgerundet (offen / Rate), letzte Rate = offen minus (n - 1) mal Rate. Ist die letzte Rate von Hand
/// gesetzt, gilt sie, und die Anzahl folgt daraus; die Summe übersteigt nie den offenen Betrag (die letzte wird gekürzt). Es gibt höchstens <see cref="MaxInstallments"/> Raten.
/// </summary>
public static class InstallmentPlan
{
    /// <summary>Die größte Zahl an Raten, die ein Plan haben darf.</summary>
    public const int MaxInstallments = 600;

    /// <summary>Berechnet den Plan für den offenen Betrag ab dem ersten Fälligkeitstag.</summary>
    public static InstallmentPlanResult Compute(decimal open, decimal rate, int intervalMonths, DateOnly firstDueDate, decimal? lastAmount)
    {
        static bool IsMoney(decimal value) => value > 0 && decimal.Round(value, 2) == value;
        static long Cents(decimal value) => (long)decimal.Round(value * 100m, 0);
        static InstallmentPlanResult Fail(InstallmentPlanError error) => new(error, 0, 0m, default);

        if (!IsMoney(rate))
        {
            return Fail(InstallmentPlanError.RateInvalid);
        }
        if (intervalMonths is < 1 or > CommitmentRules.MaxIntervalMonths)
        {
            return Fail(InstallmentPlanError.IntervalInvalid);
        }
        if (open <= 0)
        {
            return Fail(InstallmentPlanError.NothingOpen);
        }
        if (lastAmount is { } byHand && !IsMoney(byHand))
        {
            return Fail(InstallmentPlanError.LastAmountInvalid);
        }

        long openCents = Cents(open);
        long rateCents = Cents(rate);
        long count;
        long lastCents;
        if (lastAmount is { } set)
        {
            // Die Summe aller Raten übersteigt nie den offenen Betrag: Reichen die vollen Raten davor mit der letzten von Hand
            // über ihn hinaus, wird die letzte auf den Rest gekürzt; bleibt nichts für sie übrig, passt sie nicht.
            long byHand = Cents(set);
            long rest = openCents - byHand;
            if (rest <= 0)
            {
                return Fail(InstallmentPlanError.LastAmountDoesNotFit);
            }
            long full = (rest + rateCents - 1) / rateCents;
            lastCents = Math.Min(byHand, openCents - full * rateCents);
            if (lastCents <= 0)
            {
                return Fail(InstallmentPlanError.LastAmountDoesNotFit);
            }
            count = full + 1;
        }
        else
        {
            count = (openCents + rateCents - 1) / rateCents;
            lastCents = openCents - (count - 1) * rateCents;
        }
        if (count > MaxInstallments)
        {
            return Fail(InstallmentPlanError.TooManyInstallments);
        }

        var lastDue = firstDueDate.AddMonths(checked((int)((count - 1) * intervalMonths)));
        return new InstallmentPlanResult(InstallmentPlanError.None, (int)count, lastCents / 100m, lastDue);
    }
}
