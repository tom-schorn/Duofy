using Duofy.Shared.Blocks;
using Duofy.Shared.Resources;
using Microsoft.Extensions.Localization;

namespace Duofy.Shared.Services;

/// <summary>
/// Der Zusatz unter dem Frei-Betrag auf der Startseite (Spec 33, R1): der Übertrag, sobald einer im Betrag steckt
/// (Spec 66, R9), und im Haushalt, wessen Übertrag fehlen kann. Derselbe Text wie in der Monatsübersicht.
/// </summary>
public static class StartFreeNote
{
    /// <returns>Der Zusatz oder null, wenn es nichts zu sagen gibt.</returns>
    public static string? For(MonthOverviewModel model, IReadOnlyList<string> bookingsMissing, IStringLocalizer<SharedResources> localizer)
    {
        var incoming = model.CarryOvers.Where(c => c.Incoming).ToList();
        var parts = new List<string>();
        if (incoming.Count > 0)
        {
            parts.Add(localizer["Month_FreeCarryOver", MoneyText.Format(incoming.Sum(c => c.Amount))].Value);
        }
        if (bookingsMissing.Count > 0)
        {
            parts.Add(localizer["Month_FreeCarryOverMissing", string.Join(", ", bookingsMissing)].Value);
        }
        return parts.Count > 0 ? string.Join(" ", parts) : null;
    }
}
