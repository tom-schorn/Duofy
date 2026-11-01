using Duofy.Schemas;

namespace Duofy.Shared.Services;

/// <summary>
/// Die einzige Stelle, die eine Zahlungsart der API (<see cref="PaymentMethods"/>) einem Zeichen der Symbolsammlung
/// zuordnet (Issue 15), so wie <see cref="AccountIconSet"/> es für Konten tut. Wechselt die Oberfläche die Sammlung,
/// ändert sich nur diese Tabelle.
/// </summary>
public static class PaymentMethodIconSet
{
    private static readonly IReadOnlyDictionary<string, string> Symbols = new Dictionary<string, string>
    {
        [PaymentMethods.DirectDebit] = "credit_card",
        [PaymentMethods.StandingOrder] = "repeat",
        [PaymentMethods.Transfer] = "swap_horiz",
        [PaymentMethods.Withdrawal] = "payments",
        [PaymentMethods.Other] = "toll",
    };

    /// <summary>Der Name des Zeichens in der Sammlung; eine unbekannte Zahlungsart zeigt das Zeichen für "Sonstiges".</summary>
    public static string SymbolName(string? method) =>
        method is not null && Symbols.TryGetValue(method, out var symbol) ? symbol : Symbols[PaymentMethods.Other];
}
