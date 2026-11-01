namespace Duofy.Shared.Services;

/// <summary>
/// Wie ein Dialog zu Ende ging: gespeichert mit einem Ergebnis, oder "nichts geändert" (Abbrechen, Schließen
/// mit dem X, Esc). Der Aufrufer fragt zuerst <see cref="Saved"/> und liest dann <see cref="Value"/>.
/// </summary>
public readonly record struct DialogOutcome<TResult>(bool Saved, TResult? Value)
{
    /// <summary>Nichts geändert.</summary>
    public static DialogOutcome<TResult> Unchanged => new(false, default);
}
