using System.Globalization;
using Duofy.Shared.Blocks;
using Duofy.Shared.Resources;
using Microsoft.Extensions.Localization;

namespace Duofy.Shared.Services;

/// <summary>Macht aus den Überträgen eines Monats Zeilen der Linientabelle, überall gleich benannt (Spec 66, R9).</summary>
public static class MonthCarryOverText
{
    private static readonly CultureInfo German = new("de-DE");

    public static IReadOnlyList<LineTableRow> Rows(IEnumerable<MonthCarryOverRow> rows, IStringLocalizer<SharedResources> localizer) =>
        rows.Select(row => new LineTableRow(
                localizer[row.Incoming ? "Month_CarryOverIn" : "Month_CarryOverOut", row.Month.ToString("MMMM", German)].Value,
                row.Amount,
                row.Owner,
                TestId: row.Incoming ? "month-carry-over-in" : "month-carry-over-out"))
            .ToList();
}
