namespace Duofy.Domain;

/// <summary>"Jetzt verfügbar" (Spec 139): die Kontostände ohne Sparkonten und die offenen Ausgaben bis Monatsende.</summary>
public sealed record AvailableNowFigure(decimal Balances, decimal OpenExpenses)
{
    /// <summary>Was jetzt ausgegeben werden kann; kann negativ sein.</summary>
    public decimal Value => Balances - OpenExpenses;
}
