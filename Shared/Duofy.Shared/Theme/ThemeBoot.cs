using Duofy.Shared.Services;

namespace Duofy.Shared.Theme;

/// <summary>
/// Die gewählte Hell-Dunkel-Einstellung schon beim ersten Zeichnen (Issue 5). Im Browser kennt der Server die Wahl nicht
/// (sie liegt im localStorage), und das Vorrendern zeichnet erst einmal ohne sie. Darum steht im Kopf der Seite ein
/// kleines eigenes Skript (ohne Fremdskript, ohne Datei, die erst geladen werden müsste), das noch vor dem ersten
/// Zeichnen die Klasse <see cref="DarkClass"/> an <c>html</c> setzt. Danach führt <c>ThemeVariables</c> sie.
/// </summary>
public static class ThemeBoot
{
    /// <summary>Die Klasse an <c>html</c>, die theme.css und Blueprint für den dunklen Modus lesen.</summary>
    public const string DarkClass = "dark";

    /// <summary>Der Name des Eintrags im localStorage, derselbe wie im <see cref="LocalStorageThemePreferenceStore"/>.</summary>
    public const string StorageKey = LocalStorageThemePreferenceStore.StorageKey;

    /// <summary>
    /// Das Skript: dunkel bei gemerktem "true", hell bei "false", sonst nach der Einstellung des Geräts. Ein verweigerter
    /// Speicher (privates Fenster) ändert nichts; dann bleibt es bei der Voreinstellung der Seite.
    /// </summary>
    public static string Script =>
        "(function(){try{var v=localStorage.getItem('" + StorageKey + "');" +
        "var d=v==='true'||(v!=='false'&&window.matchMedia&&matchMedia('(prefers-color-scheme: dark)').matches);" +
        "if(d)document.documentElement.classList.add('" + DarkClass + "');}catch(e){}})();";

    /// <summary>Das Skript als fertiges Stück Kopf, das die Seite vor den Stylesheets einfügt.</summary>
    public static string HeadMarkup => $"<script>{Script}</script>";
}
