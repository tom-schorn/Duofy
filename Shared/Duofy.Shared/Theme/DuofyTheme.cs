using MudBlazor;

namespace Duofy.Shared.Theme;

/// <summary>
/// Das Aussehen von Duofy und Plagevo: helle Marken-Palette (Tinte, Papier, Pastellflächen), eckige Formen,
/// Linien statt Schatten. Die Werte stammen aus plagevo-landing/design/design-system.md (Spec 3).
/// Der dunkle Teil hat eigene Werte (Spec 4, plagevo-landing/STRATEGIE.md, Abschnitt Dunkelmodus).
/// </summary>
public static class DuofyTheme
{
    // Muss vor Instance stehen: Statische Felder werden der Reihe nach gesetzt, sonst ist die Liste beim Bauen noch leer.
    private static readonly string[] SansFont = ["Inter Variable", "Inter", "sans-serif"];

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

    /// <summary>Ein einziges Theme für die ganze Anwendung; es hat einen hellen und einen dunklen Teil.</summary>
    public static MudTheme Instance { get; } = Create();

    private static MudTheme Create() => new()
    {
        // Text auf Pastellflächen ist immer Tinte, nie Weiß (Kontrast mindestens 4,5 zu 1, geprüft in ThemePaletteTests).
        PaletteLight = new PaletteLight
        {
            Primary = "#14213d",
            PrimaryContrastText = "#fbfaf6",
            Secondary = "#f6d55c",
            SecondaryContrastText = "#14213d",
            Tertiary = "#bfd8c8",
            TertiaryContrastText = "#14213d",
            Error = "#f2b8a0",
            ErrorContrastText = "#14213d",
            Success = "#bfd8c8",
            SuccessContrastText = "#14213d",
            Info = "#d9e1eb",
            InfoContrastText = "#14213d",
            Warning = "#f6d55c",
            WarningContrastText = "#14213d",
            Background = "#fbfaf6",
            Surface = "#fbfaf6",
            AppbarBackground = "#14213d",
            AppbarText = "#fbfaf6",
            DrawerBackground = "#e9eee8",
            DrawerText = "#14213d",
            TextPrimary = "#14213d",
            TextSecondary = "#3d4a60",
            LinesDefault = "#14213d",
            LinesInputs = "#14213d",
            Divider = "#14213d",
            ActionDefault = "#14213d",
        },
        // Eigener Dunkelmodus, nicht invertiert (Spec 4): Marineblau als Grund, gedämpfte Blöcke, gelber Hauptknopf.
        // Auf den Blöcken steht nur der Haupttext, nie der Nebentext (unter 4,5 zu 1, geprüft in ThemeDarkTests).
        PaletteDark = new PaletteDark
        {
            Primary = "#f6d55c",
            PrimaryContrastText = "#14213d",
            Secondary = "#745a0d",
            SecondaryContrastText = "#f2efe8",
            Tertiary = "#2e5a47",
            TertiaryContrastText = "#f2efe8",
            Error = "#7c4330",
            ErrorContrastText = "#f2efe8",
            Success = "#2e5a47",
            SuccessContrastText = "#f2efe8",
            Info = "#3a4863",
            InfoContrastText = "#f2efe8",
            Warning = "#745a0d",
            WarningContrastText = "#f2efe8",
            Background = "#0f1729",
            Surface = "#18233b",
            AppbarBackground = "#18233b",
            AppbarText = "#f2efe8",
            DrawerBackground = "#18233b",
            DrawerText = "#f2efe8",
            TextPrimary = "#f2efe8",
            TextSecondary = "#b8c2d4",
            LinesDefault = "#3a4863",
            LinesInputs = "#7482a0",
            Divider = "#3a4863",
            ActionDefault = "#b8c2d4",
            // MudBlazor setzt hier sonst reines Weiß oder Schwarz (R3): stattdessen Texttöne und Linie, durchscheinend wie vorher.
            White = "#f2efe8",
            DarkContrastText = "#f2efe8",
            TextDisabled = "rgba(242,239,232,0.5)",
            ActionDisabled = "rgba(242,239,232,0.26)",
            ActionDisabledBackground = "rgba(242,239,232,0.12)",
            DrawerIcon = "#b8c2d4",
            TableLines = "#3a4863",
            TableStriped = "rgba(242,239,232,0.04)",
            TableHover = "rgba(242,239,232,0.06)",
            DividerLight = "#3a4863",
            Skeleton = "rgba(242,239,232,0.11)",
        },
        LayoutProperties = new LayoutProperties
        {
            // Eckig: höchstens 4 px (Bedienelemente laut Design-System höchstens 2 px).
            DefaultBorderRadius = "2px",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = SansFont,
                FontSize = TypeScale["Text"].Size,
                FontWeight = TypeScale["Text"].Weight,
                LineHeight = TypeScale["Text"].LineHeight,
            },
            // Knöpfe in normaler Schreibung statt in Großbuchstaben.
            Button = new ButtonTypography
            {
                FontFamily = SansFont,
                TextTransform = "none",
                LetterSpacing = "normal",
            },
            H1 = Heading("Title"),
            H2 = Heading("Title", "1.75rem"),
            H3 = Heading("Section"),
            H4 = Heading("Section", "1.25rem"),
            H5 = Heading("Section", "1.125rem"),
            H6 = Heading("Section", "1rem"),
            Subtitle1 = new Subtitle1Typography { FontFamily = SansFont, FontSize = TypeScale["Text"].Size, FontWeight = "600", LineHeight = TypeScale["Text"].LineHeight },
            Subtitle2 = new Subtitle2Typography { FontFamily = SansFont, FontSize = TypeScale["Extra"].Size, FontWeight = "600", LineHeight = TypeScale["Extra"].LineHeight },
            Body1 = new Body1Typography { FontFamily = SansFont, FontSize = TypeScale["Text"].Size, FontWeight = TypeScale["Text"].Weight, LineHeight = TypeScale["Text"].LineHeight },
            Body2 = new Body2Typography { FontFamily = SansFont, FontSize = TypeScale["Extra"].Size, FontWeight = TypeScale["Extra"].Weight, LineHeight = TypeScale["Extra"].LineHeight },
            Caption = new CaptionTypography { FontFamily = SansFont, FontSize = TypeScale["Extra"].Size, FontWeight = TypeScale["Extra"].Weight, LineHeight = TypeScale["Extra"].LineHeight },
        },
    };

    // Überschriften in der UI-Schrift (Inter) statt einer Serifenschrift, Größe/Gewicht/Zeilenhöhe aus der Skala.
    private static H1Typography Heading(string role, string? size = null) => new()
    {
        FontFamily = SansFont,
        FontSize = size ?? TypeScale[role].Size,
        FontWeight = TypeScale[role].Weight,
        LineHeight = TypeScale[role].LineHeight,
    };
}
