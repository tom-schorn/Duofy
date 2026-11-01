using Duofy.Schemas;

namespace Duofy.Shared.Services;

/// <summary>
/// Die einzige Stelle, die den Namen eines Kontosymbols aus der API (<see cref="AccountIcons"/>) einem Zeichen der
/// Symbolsammlung zuordnet (Issue 14). Es sind die Material Symbols, die Radzen mitbringt; wechselt
/// die Oberfläche die Sammlung, ändert sich nur diese Tabelle, nicht die Daten und nicht die Seiten.
/// </summary>
public static class AccountIconSet
{
    private static readonly IReadOnlyDictionary<string, string> Symbols = new Dictionary<string, string>
    {
        [AccountIcons.Bank] = "account_balance",
        [AccountIcons.Piggy] = "savings",
        [AccountIcons.Cash] = "payments",
        [AccountIcons.Card] = "credit_card",
        [AccountIcons.Wallet] = "account_balance_wallet",
        [AccountIcons.Coins] = "toll",
    };

    /// <summary>Der Name des Zeichens in der Sammlung; ein unbekannter Name zeigt das Zeichen der Bank.</summary>
    public static string SymbolName(string? icon) =>
        icon is not null && Symbols.TryGetValue(icon, out var symbol) ? symbol : Symbols[AccountIcons.Bank];
}
