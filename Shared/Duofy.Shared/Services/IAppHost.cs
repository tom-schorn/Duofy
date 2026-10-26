namespace Duofy.Shared.Services;

/// <summary>
/// Sagt, ob die Oberfläche in der Android-App läuft oder im Browser. Die Seiten fragen hier,
/// statt den User-Agent zu prüfen: Die App meldet <see cref="AppHost"/> in MauiProgram an,
/// im Browser bleibt es bei <see cref="BrowserHost"/> aus AddDuofyUi.
/// </summary>
public interface IAppHost
{
    bool IsApp { get; }
}

/// <summary>Der Standard: Wir laufen im Browser.</summary>
public sealed class BrowserHost : IAppHost
{
    public bool IsApp => false;
}

/// <summary>Wir laufen in der Android-App. Nur MauiProgram meldet diese Klasse an.</summary>
public sealed class AppHost : IAppHost
{
    public bool IsApp => true;
}
