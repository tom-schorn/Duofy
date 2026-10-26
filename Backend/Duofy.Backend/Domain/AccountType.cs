namespace Duofy.Domain;

/// <summary>
/// Die Art eines Kontos. Das Original kannte sechs Arten; nur diese drei bedeuten etwas.
/// Die Art schlägt vor, ob das Geld als verfügbar zählt, siehe <see cref="Account.CountsAsAvailableFor"/>.
/// </summary>
public enum AccountType
{
    /// <summary>Girokonto: Geld für den Alltag.</summary>
    Checking,

    /// <summary>Sparkonto: Geld, das zurückgelegt ist.</summary>
    Savings,

    /// <summary>Bargeld im Portemonnaie oder in der Kasse.</summary>
    Cash,
}
