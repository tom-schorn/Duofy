namespace Duofy.Shared.Theme;

/// <summary>
/// Das freigegebene Aussehen als --rz-*-Werte an einer Stelle (Radzen R3): Palette "Altes Duofy", 12 px Rundung, Menü ohne
/// Trenner und Balken, Inter und das bestätigte Dunkel-Design. Ausgeliefert werden die Werte als fertiger Stil
/// (<see cref="Css"/>), nicht wie im Spike zur Laufzeit per JavaScript, damit die Standardfarben von Radzen nicht aufblitzen.
/// </summary>
public static class RadzenVariables
{
    /// <summary>Die Theme-Datei von Radzen für den hellen Modus.</summary>
    public const string LightTheme = "material";

    /// <summary>Die Theme-Datei von Radzen für den dunklen Modus.</summary>
    public const string DarkTheme = "material-dark";

    private const string Radius = "12px";

    /// <summary>
    /// Typografie-Skala: Größe, Gewicht und Zeilenhöhe je Rolle (Titel, Abschnitt, Betrag, Text, Zusatz).
    /// Spätere Bausteine lesen ihre Schrift hier ab.
    /// </summary>
    public static IReadOnlyDictionary<string, TypeStyle> TypeScale { get; } = new Dictionary<string, TypeStyle>
    {
        ["Title"] = new("clamp(2rem, 3vw, 3.25rem)", "700", "1.1"),
        ["Section"] = new("1.5rem", "650", "1.2"),
        ["Amount"] = new("clamp(1.6rem, 2.4vw, 2.8rem)", "700", "1.1"),
        ["Text"] = new("1rem", "400", "1.6"),
        ["Extra"] = new("0.875rem", "500", "1.45"),
    };

    // Zuerst die Listen: Statische Felder werden der Reihe nach gesetzt.
    private static readonly string[] RoundedParts =
    [
        "border", "card-border", "button-border", "input-border", "panel-menu-item-border", "menu-item-border", "menu-border",
        "dialog-border", "alert-border", "accordion-border", "fieldset-border", "grid-border", "datalist-border",
        "listbox-border", "form-field-filled-border",
    ];

    /// <summary>Was hell und dunkel gleich ist: Rundung, Menü und die Schrift.</summary>
    public static IReadOnlyDictionary<string, string> Common { get; } = BuildCommon();

    /// <summary>Palette "Altes Duofy"; die helleren und dunkleren Stufen sind daraus abgeleitet und im Test auf Kontrast geprüft.</summary>
    public static IReadOnlyDictionary<string, string> Light { get; } = BuildLight();

    /// <summary>Das bestätigte Dunkel-Design: Grund #0b0e14, Karte #141922, Hauptfarbe #86ace0; auf der Hauptfarbe steht dunkler Text.</summary>
    public static IReadOnlyDictionary<string, string> Dark { get; } = BuildDark();

    /// <summary>Der fertige Stil für einen Modus. Der Selektor html:root wiegt mehr als das :root der Theme-Datei, die Reihenfolge der Dateien ist also egal.</summary>
    public static string Css(bool dark)
    {
        var mode = dark ? Dark : Light;
        var declarations = Common.Concat(mode).Select(v => $"{v.Key}:{v.Value};");
        return $"html:root{{{string.Concat(declarations)}color-scheme:{(dark ? "dark" : "light")}}}";
    }

    private static Dictionary<string, string> BuildCommon()
    {
        var values = RoundedParts.ToDictionary(part => $"--rz-{part}-radius", _ => Radius);
        values["--rz-sidebar-border-inline-end"] = "none";
        values["--rz-sidebar-width"] = "var(--side-nav-width)";
        values["--rz-layout-body-padding"] = "0";
        values["--rz-panel-menu-item-border"] = "none";
        values["--rz-panel-menu-item-active-indicator"] = "transparent";
        values["--rz-panel-menu-item-background-color"] = "transparent";
        values["--rz-profile-menu-top-item-background-color"] = "transparent";
        values["--rz-profile-menu-toggle-button-color"] = "currentColor";
        values["--rz-header-border"] = "none";

        // Dünner Fortschrittsbalken, wo die Seite keinen eigenen setzt (Sparziele, Schulden); die Diagramme setzen ihre Höhe selbst.
        values["--rz-progressbar-height"] = "0.5rem";
        values["--rz-progressbar-border-radius"] = "var(--rz-border-radius)";
        values["--rz-input-border"] = "var(--rz-border-width) solid var(--rz-input-line)";
        // Checkbox und Radioknopf zeichnen ihren Rand mit dieser Größe; im Theme ist sie ein helles Grau, das auf Papier kaum zu sehen ist.
        values["--rz-input-border-block-end"] = "var(--rz-border-width) solid var(--rz-input-line)";

        // Die Kopfleiste (1300) und die untere Leiste (1200) dürfen weder Dialog noch Abdunklung verdecken.
        values["--rz-dialog-mask-zindex"] = "1400";
        values["--rz-dialog-zindex"] = "1401";
        values["--rz-text-font-family"] = "'Inter Variable', 'Inter', sans-serif";
        return values;
    }

    private static Dictionary<string, string> BuildLight()
    {
        const string ink = "#14213d";
        const string white = "#ffffff";
        const string primary = "#1e3a5f";

        var values = new Dictionary<string, string>
        {
            ["--rz-body-background-color"] = "#fbf9f4",
            ["--rz-layout-body-background-color"] = "#fbf9f4",
            ["--rz-base-background-color"] = "#fbf9f4",
            ["--rz-sidebar-background-color"] = "#fbf9f4",
            ["--rz-sidebar-color"] = "#424242",
            ["--rz-card-background-color"] = white,
            ["--rz-card-shadow"] = "0 1px 3px rgba(20, 33, 61, 0.06), 0 4px 12px rgba(20, 33, 61, 0.05)",
            ["--rz-header-background-color"] = primary,
            ["--rz-header-color"] = white,
            ["--rz-text-color"] = "#424242",
            ["--rz-text-title-color"] = "#212121",
            ["--rz-text-secondary-color"] = "#535b66",
            ["--rz-text-tertiary-color"] = "#5f6b7a",
            ["--rz-link-color"] = primary,
            ["--rz-input-line"] = "#788aa0",
            ["--rz-card-border"] = "var(--rz-border-width) solid #c3cad4",
            ["--rz-grid-cell-border"] = "var(--rz-border-width) solid #788aa0",
        };

        foreach (var name in new[] { "primary", "secondary" })
        {
            AddColour(values, name, (primary, white), ("#2a4d7a", white), ("#172d4a", white), ("#12243b", white), ("rgba(30, 58, 95, 0.12)", primary));
        }

        AddColour(values, "info", ("#2b6cb0", white), ("#2f70b5", white), ("#235a93", white), ("#1c4a79", white), ("rgba(43, 108, 176, 0.14)", "#1f5288"));
        AddColour(values, "warning", ("#c68a0e", ink), ("#d49a24", ink), ("#8f630a", white), ("#7a5408", white), ("rgba(198, 138, 14, 0.16)", "#6f4c06"));
        AddColour(values, "success", ("#4f9e6a", ink), ("#62ad7c", ink), ("#2f7a4a", white), ("#276640", white), ("rgba(79, 158, 106, 0.16)", "#256040"));
        AddColour(values, "danger", ("#a63a2e", white), ("#b54237", white), ("#8f3026", white), ("#7a2920", white), ("rgba(166, 58, 46, 0.14)", "#8a2f25"));
        return values;
    }

    private static Dictionary<string, string> BuildDark()
    {
        const string night = "#0b0e14";
        const string card = "#141922";
        const string text = "#f2efe8";
        const string white = "#ffffff";
        const string primary = "#86ace0";

        var values = new Dictionary<string, string>
        {
            ["--rz-body-background-color"] = night,
            ["--rz-layout-body-background-color"] = night,
            ["--rz-base-background-color"] = card,
            ["--rz-sidebar-background-color"] = night,
            ["--rz-sidebar-color"] = text,
            ["--rz-card-background-color"] = card,
            ["--rz-card-shadow"] = "0 1px 3px rgba(0, 0, 0, 0.4), 0 4px 12px rgba(0, 0, 0, 0.3)",
            ["--rz-header-background-color"] = card,
            ["--rz-header-color"] = text,
            ["--rz-dialog-background-color"] = card,
            ["--rz-dialog-title-background-color"] = card,
            ["--rz-text-color"] = text,
            ["--rz-text-title-color"] = text,
            ["--rz-text-secondary-color"] = "#b8c2d4",
            ["--rz-text-tertiary-color"] = "#9aa6bb",
            ["--rz-link-color"] = primary,
            ["--rz-input-line"] = "#7482a0",
            ["--rz-card-border"] = "var(--rz-border-width) solid #3a4863",
            ["--rz-grid-cell-border"] = "var(--rz-border-width) solid #7482a0",

            // Die Grauleiter des Dunkel-Themes von Radzen in den blauen Grund des Designs gezogen, damit nichts neutral grau absticht.
            ["--rz-base"] = "#1c2330",
            ["--rz-base-50"] = text,
            ["--rz-base-100"] = "#b8c2d4",
            ["--rz-base-200"] = "#7482a0",
            ["--rz-base-300"] = "#5a6880",
            ["--rz-base-400"] = "#3a4863",
            ["--rz-base-500"] = "#2e3847",
            ["--rz-base-600"] = "#252d3b",
            ["--rz-base-700"] = "#1c2330",
            ["--rz-base-800"] = card,
            ["--rz-base-900"] = night,
        };

        foreach (var name in new[] { "primary", "secondary" })
        {
            AddColour(values, name, (primary, night), ("#9dbde8", night), ("#6f98d1", night), ("#5f86bd", night), ("rgba(134, 172, 224, 0.16)", primary));
        }

        AddColour(values, "info", ("#2b6cb0", white), ("#2f70b5", white), ("#235a93", white), ("#1c4a79", white), ("rgba(43, 108, 176, 0.24)", "#a8ccf0"));
        AddColour(values, "warning", ("#c68a0e", night), ("#d49a24", night), ("#a8750c", night), ("#8f630a", white), ("rgba(198, 138, 14, 0.2)", "#e8bd5c"));
        AddColour(values, "success", ("#4f9e6a", night), ("#62ad7c", night), ("#3f8a58", night), ("#2f7a4a", white), ("rgba(79, 158, 106, 0.2)", "#94d6aa"));
        AddColour(values, "danger", ("#a63a2e", white), ("#b54237", white), ("#8f3026", white), ("#7a2920", white), ("rgba(166, 58, 46, 0.28)", "#f4aaa1"));
        return values;
    }

    // Eine Farbe mit ihren Stufen, jeweils Fläche und der Text darauf.
    private static void AddColour(
        Dictionary<string, string> values,
        string name,
        (string Fill, string On) regular,
        (string Fill, string On) light,
        (string Fill, string On) dark,
        (string Fill, string On) darker,
        (string Fill, string On) lighter)
    {
        foreach (var (shade, colour) in new[] { ("", regular), ("-light", light), ("-dark", dark), ("-darker", darker), ("-lighter", lighter) })
        {
            values[$"--rz-{name}{shade}"] = colour.Fill;
            values[$"--rz-on-{name}{shade}"] = colour.On;
        }
    }
}
