using System.Globalization;
using Duofy.Shared.Services;
using MudBlazor.Utilities;

namespace Duofy.Shared.Theme;

/// <summary>
/// Die gewählte Hell-Dunkel-Einstellung schon beim ersten Zeichnen (Issue 5). Im Browser kennt der Server die Wahl nicht
/// (sie liegt im localStorage), und MudBlazor zeichnet beim Vorrendern erst einmal hell; die Wahl wird erst nach dem
/// Verbinden angewendet. Darum steht im Kopf der Seite ein kleines eigenes Skript (ohne Fremdskript, ohne Datei, die erst
/// geladen werden müsste), das noch vor dem ersten Zeichnen die Klasse <see cref="DarkClass"/> an <c>html</c> setzt, und ein
/// Stil, der unter dieser Klasse die wichtigsten Farben des dunklen Themes setzt. Sobald das Layout selbst im richtigen Modus
/// zeichnet, nimmt es die Klasse wieder weg (<see cref="RemoveFunction"/>), damit sie eine spätere Wahl nicht überstimmt.
/// </summary>
public static class ThemeBoot
{
    /// <summary>Die Klasse an <c>html</c>, solange nur das Skript im Kopf die Farbe bestimmt.</summary>
    public const string DarkClass = "duofy-theme-boot-dark";

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

    /// <summary>Die Funktion, mit der das Layout die Klasse nach dem ersten richtigen Zeichnen wieder entfernt (ohne eval).</summary>
    public const string RemoveFunction = "document.documentElement.classList.remove";

    /// <summary>
    /// Der Stil für die Zeit vor dem ersten richtigen Zeichnen: Unter der Klasse gelten die dunklen Farben des Themes
    /// (Hintergrund, Flächen, Text, Linien) und der Browser zeichnet seine eigenen Elemente dunkel. Die Werte kommen aus
    /// der Palette des Themes, damit sie nicht an zwei Stellen stehen.
    /// </summary>
    public static string Style
    {
        get
        {
            var p = DuofyTheme.Instance.PaletteDark;
            var vars = new (string Name, MudColor Value)[]
            {
                ("background", p.Background), ("surface", p.Surface),
                ("appbar-background", p.AppbarBackground), ("appbar-text", p.AppbarText),
                ("drawer-background", p.DrawerBackground), ("drawer-text", p.DrawerText),
                ("text-primary", p.TextPrimary), ("text-secondary", p.TextSecondary),
                ("lines-default", p.LinesDefault), ("lines-inputs", p.LinesInputs), ("divider", p.Divider),
                ("action-default", p.ActionDefault), ("primary", p.Primary), ("primary-contrast-text", p.PrimaryContrastText),
            };
            var declarations = string.Join(";", vars.Select(v => $"--mud-palette-{v.Name}:{Css(v.Value)}"));
            return $"html.{DarkClass}{{color-scheme:dark;background-color:{Css(p.Background)};{declarations}}}" +
                   $"html.{DarkClass} body{{background-color:{Css(p.Background)};color:{Css(p.TextPrimary)}}}";
        }
    }

    /// <summary>Skript und Stil als fertiges Stück Kopf, das die Seite vor den Stylesheets einfügt.</summary>
    public static string HeadMarkup => $"<script>{Script}</script><style>{Style}</style>";

    private static string Css(MudColor c) => c.A == 255
        ? $"#{c.R:x2}{c.G:x2}{c.B:x2}"
        : string.Create(CultureInfo.InvariantCulture, $"rgba({c.R},{c.G},{c.B},{c.A / 255.0:0.##})");
}
