using Duofy.Shared.Resources;
using Duofy.Shared.Services;
using Microsoft.Extensions.Localization;

namespace Duofy.Shared.Layout;

/// <summary>
/// Der Satz, wenn eine Person einen Bereich nicht teilt (Spec 68, 126): In der Sicht auf eine Person nennt er Person und
/// Bereich ("Kai teilt Konten nicht mit dir."), sonst bleibt es beim allgemeinen Satz.
/// </summary>
internal static class NotSharedText
{
    /// <param name="area">Der Schlüssel des Bereichs, wie ihn <c>Grants_Area_*</c> kennt (plan, book, accounts, commitments, savings).</param>
    public static string For(IStringLocalizer<SharedResources> l, ActiveMember member, string? area)
    {
        if (area is null || member.Name is not { } who)
        {
            return l["Grant_NotShared"].Value;
        }

        return l["Grant_NotSharedNamed", who, l[$"Grants_Area_{area}"].Value].Value;
    }
}
