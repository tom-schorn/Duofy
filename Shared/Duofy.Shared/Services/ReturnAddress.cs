namespace Duofy.Shared.Services;

/// <summary>
/// Der Rücksprung nach Anmelden oder Registrieren (Parameter returnUrl). Nur Adressen auf dieser Seite
/// sind erlaubt; sonst könnte jemand einen Link bauen, der danach auf eine fremde Seite weiterleitet.
/// </summary>
public static class ReturnAddress
{
    public static bool IsLocalPath(string? url) =>
        !string.IsNullOrEmpty(url)
        && url.StartsWith('/')
        && !url.StartsWith("//")
        && !url.Contains('\\')
        // Browser entfernen Tabs und Zeilenumbrüche aus Adressen; aus "/\t/evil.example" würde sonst "//evil.example".
        && !url.Any(c => char.IsControl(c) || char.IsWhiteSpace(c));

    /// <summary>Hängt den Rücksprung an eine Adresse, wenn er erlaubt ist, zum Beispiel "/registrieren?returnUrl=%2Fkonto".</summary>
    public static string With(string path, string? returnUrl) =>
        IsLocalPath(returnUrl) ? $"{path}?returnUrl={Uri.EscapeDataString(returnUrl!)}" : path;
}
