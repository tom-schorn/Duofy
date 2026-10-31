using System.Globalization;

namespace Duofy.Shared.Blocks;

/// <summary>Beträge der Bausteine immer gleich geschrieben: deutsches Zahlenformat mit Euro-Zeichen.</summary>
internal static class MoneyText
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    public static string Format(decimal amount) => amount.ToString("N2", German) + " €";
}
