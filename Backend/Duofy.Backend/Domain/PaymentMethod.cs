namespace Duofy.Domain;

/// <summary>
/// Wie das Geld eines Vertrags fließt. Der Plan wird später an die Zahlungen erinnern,
/// die die Person selbst anstoßen muss (Überweisung, Abhebung); Lastschrift und
/// Dauerauftrag laufen von allein.
/// </summary>
public enum PaymentMethod
{
    /// <summary>Lastschrift: Der Empfänger zieht das Geld ein.</summary>
    DirectDebit,

    /// <summary>Dauerauftrag: Die Bank überweist regelmäßig von selbst.</summary>
    StandingOrder,

    /// <summary>Überweisung: Die Person überweist jedes Mal selbst.</summary>
    Transfer,

    /// <summary>Abhebung: Bargeld wird abgehoben, zum Beispiel für den Umschlag.</summary>
    Withdrawal,

    /// <summary>Sonstiges: alles andere, auch wenn die Art nicht bekannt ist.</summary>
    Other,
}

/// <summary>
/// Übersetzt eine <see cref="PaymentMethod"/> in ihren Schlüssel (klein geschrieben mit
/// Unterstrich, zum Beispiel "direct_debit") und zurück. Der Schlüssel steht in der
/// Datenbank und in der API.
/// </summary>
public static class PaymentMethodKeys
{
    public static string ToKey(PaymentMethod method) => method switch
    {
        PaymentMethod.DirectDebit => "direct_debit",
        PaymentMethod.StandingOrder => "standing_order",
        PaymentMethod.Transfer => "transfer",
        PaymentMethod.Withdrawal => "withdrawal",
        PaymentMethod.Other => "other",
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unbekannte Zahlungsart."),
    };

    /// <summary>Liest einen Schlüssel. Nur die genaue Schreibweise gilt, "DirectDebit" oder "1" nicht.</summary>
    public static bool TryParse(string? key, out PaymentMethod method)
    {
        foreach (var candidate in Enum.GetValues<PaymentMethod>())
        {
            if (ToKey(candidate) == key)
            {
                method = candidate;
                return true;
            }
        }
        method = default;
        return false;
    }

    /// <summary>Liest einen Schlüssel aus der Datenbank; ein unbekannter ist ein Fehler im Programm.</summary>
    public static PaymentMethod Parse(string key) =>
        TryParse(key, out var method)
            ? method
            : throw new FormatException($"Unbekannte Zahlungsart '{key}' in der Datenbank.");
}
