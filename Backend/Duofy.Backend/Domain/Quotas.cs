namespace Duofy.Domain;

/// <summary>
/// Die Zielquoten einer Person oder eines Monats in ganzen Prozent: welcher Anteil der Einnahmen
/// in Grundbedarf, Wünsche und Sparen gehen soll. Die drei Werte ergeben zusammen 100.
/// </summary>
public readonly record struct Quotas(int Needs, int Wants, int Savings)
{
    /// <summary>Die 50/30/20-Regel; so startet jede Person.</summary>
    public static Quotas Default { get; } = new(50, 30, 20);

    /// <summary>True, wenn jeder Wert zwischen 0 und 100 liegt und alle zusammen 100 ergeben.</summary>
    public bool IsValid =>
        Needs is >= 0 and <= 100 && Wants is >= 0 and <= 100 && Savings is >= 0 and <= 100
        && Needs + Wants + Savings == 100;

    /// <summary>Die Zielquote eines Topfs; der Topf Einnahmen hat keine.</summary>
    public int? For(Pot pot) => pot switch
    {
        Pot.Needs => Needs,
        Pot.Wants => Wants,
        Pot.Savings => Savings,
        _ => null,
    };
}
