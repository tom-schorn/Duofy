namespace Duofy.Shared.Services;

/// <summary>
/// Gibt einen Link weiter, zum Beispiel eine Einladung in den Haushalt. Die App öffnet den Teilen-Dialog
/// von Android (MAUI Share), der Browser kopiert den Link in die Zwischenablage. Jeder Host meldet seine
/// eigene Umsetzung an.
/// </summary>
public interface IShareService
{
    /// <param name="title">Die Überschrift im Teilen-Dialog, zum Beispiel "Einladung in Haushalt von Anna".</param>
    /// <param name="link">Der Link, der weitergegeben wird.</param>
    Task ShareAsync(string title, string link);
}
