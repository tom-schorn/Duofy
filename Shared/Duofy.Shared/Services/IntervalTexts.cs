using Duofy.Shared.Resources;
using Microsoft.Extensions.Localization;

namespace Duofy.Shared.Services;

/// <summary>
/// Der Abstand eines Vertrags in Worten, wie ihn die Vertragsliste und die Zusammenfassung des
/// Vertragsdialogs zeigen: die üblichen Abstände mit eigenem Wort, alle anderen "alle n Monate".
/// </summary>
public static class IntervalTexts
{
    public static string For(IStringLocalizer<SharedResources> L, int months) => months is 1 or 3 or 6 or 12
        ? L[$"Interval_{months}"]
        : L["Interval_Other", months];
}
