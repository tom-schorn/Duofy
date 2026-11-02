using System.Globalization;
using Duofy.Schemas;
using Duofy.Shared.Blocks;
using Duofy.Shared.Pages;
using Duofy.Shared.Resources;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace Duofy.Shared.Services;

/// <summary>Macht aus einer Buchung des Monats eine Zeile der Linientabelle, überall gleich benannt (Spec 51, R9, R10).</summary>
public static class MonthBookingText
{
    /// <summary>Die vierte Art einer Zeile: Ein Übertrag ist keine Buchung, steht aber in denselben Listen (Spec 66, R9).</summary>
    public const string CarryOver = "carryover";

    private static readonly CultureInfo German = new("de-DE");

    public static LineTableRow Row(MonthBookingRow row, IStringLocalizer<SharedResources> localizer, string testId, RenderFragment? trailing = null) => new(
        row.Purpose ?? localizer[$"BookingKind_{row.Kind}"].Value,
        row.Amount,
        string.Join(" · ", new[] { row.Date.ToString("d", German), row.Detail }.OfType<string>()),
        Trailing: trailing,
        TestId: testId,
        Tag: Badge(row.Kind),
        AmountPrefix: Sign(row.Kind));

    /// <summary>Einnahmen mit Plus, Ausgaben mit Minus; eine Umbuchung hat im Monat keine Richtung.</summary>
    public static string? Sign(string kind) => kind switch
    {
        BookingKinds.Income => "+",
        BookingKinds.Expense => "−",
        _ => null,
    };

    public static RenderFragment Badge(string kind) => builder =>
    {
        builder.OpenComponent<BookingKindBadge>(0);
        builder.AddAttribute(1, nameof(BookingKindBadge.Kind), kind);
        builder.CloseComponent();
    };
}
