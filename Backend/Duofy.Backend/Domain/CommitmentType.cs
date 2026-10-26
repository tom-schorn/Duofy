namespace Duofy.Domain;

/// <summary>
/// Die Art eines Vertrags. Sie sagt, wofür das Geld fließt, und entscheidet
/// später darüber, wie der Vertrag im Monatsplan gezählt wird.
/// </summary>
public enum CommitmentType
{
    /// <summary>Eine gewöhnliche wiederkehrende Ausgabe, zum Beispiel Miete, Strom oder ein Abo.</summary>
    Contract,

    /// <summary>Geld, das regelmäßig zur Seite gelegt wird, zum Beispiel für einen Urlaub.</summary>
    SavingsGoal,

    /// <summary>Die Rate, mit der eine Schuld abbezahlt wird, zum Beispiel ein Kredit.</summary>
    Debt,

    /// <summary>Geld, das hereinkommt, zum Beispiel Gehalt, Kindergeld oder Zinsen.</summary>
    Income,
}
