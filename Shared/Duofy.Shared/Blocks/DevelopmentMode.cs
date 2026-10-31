namespace Duofy.Shared.Blocks;

/// <summary>
/// Sagt, ob die Anwendung im Entwicklungsbetrieb läuft. Der Host meldet sie an (Web: Program.cs);
/// ohne Meldung, etwa in der App, gilt Produktion. Nur die Testseite der Bausteine fragt danach.
/// </summary>
public interface IDevelopmentMode
{
    bool IsDevelopment { get; }
}

/// <summary>Der Wert, den der Host anmeldet.</summary>
public sealed record DevelopmentMode(bool IsDevelopment) : IDevelopmentMode;
