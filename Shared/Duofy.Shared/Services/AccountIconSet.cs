using Duofy.Schemas;

namespace Duofy.Shared.Services;

/// <summary>
/// Die einzige Stelle, die den Namen eines Kontosymbols aus der API (<see cref="AccountIcons"/>) einem Zeichen der
/// Symbolsammlung zuordnet (Issue 14). Heute sind es Tabler-Symbole; wechselt die Oberfläche die Sammlung (etwa zu
/// Material Symbols), ändert sich nur diese Tabelle, nicht die Daten und nicht die Seiten.
/// </summary>
public static class AccountIconSet
{
    private static readonly IReadOnlyDictionary<string, string> Symbols = new Dictionary<string, string>
    {
        [AccountIcons.Bank] = "building-bank",
        [AccountIcons.Piggy] = "pig-money",
        [AccountIcons.Cash] = "cash",
        [AccountIcons.Card] = "credit-card",
        [AccountIcons.Wallet] = "wallet",
        [AccountIcons.Coins] = "coins",
    };

    /// <summary>Der Name des Zeichens in der Sammlung; ein unbekannter Name zeigt das Zeichen der Bank.</summary>
    public static string SymbolName(string? icon) =>
        icon is not null && Symbols.TryGetValue(icon, out var symbol) ? symbol : Symbols[AccountIcons.Bank];
}
