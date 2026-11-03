using System.Globalization;

namespace Duofy.Shared.Charts;

/// <summary>Rechnen und Schreiben der Diagramme: Längen der Balken und Zahlen im deutschen Format.</summary>
public static class ChartMath
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Eine Prozentzahl als CSS-Zahl (immer mit Punkt, höchstens zwei Stellen), auf 0 bis 100 begrenzt.</summary>
    public static string CssPercent(decimal percent) =>
        Math.Round(Math.Clamp(percent, 0m, 100m), 2).ToString("0.##", CultureInfo.InvariantCulture) + "%";

    /// <summary>Anteil des Werts am größten Wert in Prozent; negative Werte und ein Maximum von 0 ergeben 0.</summary>
    public static decimal ShareOfMax(decimal value, decimal max) => max <= 0m || value <= 0m ? 0m : value / max * 100m;

    /// <summary>Wert als Zahl ohne überflüssige Nachkommastellen, z. B. "100" oder "12,5".</summary>
    public static string Number(decimal value) => value.ToString("#,##0.##", German);

    /// <summary>Soll und Ist mit zwei Nachkommastellen, wie die Beträge der anderen Bausteine.</summary>
    public static string Amount(decimal value) => value.ToString("N2", German);

    /// <summary>Ein Prozentwert mit höchstens einer Nachkommastelle, z. B. "38,5 %" oder "50 %".</summary>
    public static string PercentValue(decimal percent) => percent.ToString("0.#", German) + " %";

    /// <summary>Ganze Prozent mit Leerzeichen vor dem Zeichen, z. B. "125 %".</summary>
    public static string Percent(decimal percent) => Math.Round(percent, 0, MidpointRounding.AwayFromZero).ToString("N0", German) + " %";
}
