using MudBlazor;

namespace Duofy.Shared.Theme;

/// <summary>
/// Das Aussehen von Duofy: Marineblau mit Messing, wie in der ursprünglichen Oberfläche
/// (frontend/src/index.css). Die Farbwerte sind von dort übernommen.
/// </summary>
public static class DuofyTheme
{
    /// <summary>Ein einziges Theme für die ganze Anwendung; es hat einen hellen und einen dunklen Teil.</summary>
    public static MudTheme Instance { get; } = Create();

    private static readonly string[] SansFont = ["Geist Variable", "Geist", "sans-serif"];

    private static MudTheme Create() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#1e3a5f",
            PrimaryContrastText = "#fbf9f4",
            Secondary = "#c68a0e",
            SecondaryContrastText = "#0f141c",
            Tertiary = "#4f9e6a",
            Error = "#a63a2e",
            Success = "#4f9e6a",
            Info = "#2b6cb0",
            Warning = "#c68a0e",
            Background = "#fbf9f4",
            Surface = "#ffffff",
            AppbarBackground = "#1e3a5f",
            AppbarText = "#fbf9f4",
            DrawerBackground = "#f6f3ec",
            DrawerText = "#0f141c",
            TextPrimary = "#0f141c",
            TextSecondary = "#5a6270",
            LinesDefault = "#dbd7cc",
            LinesInputs = "#dbd7cc",
            Divider = "#dbd7cc",
            ActionDefault = "#5a6270",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#86ace0",
            PrimaryContrastText = "#0b0e14",
            Secondary = "#c2952f",
            SecondaryContrastText = "#0b0e14",
            Tertiary = "#4da87c",
            Error = "#dd7263",
            Success = "#4da87c",
            Info = "#4285c9",
            Warning = "#c2952f",
            Background = "#0b0e14",
            Surface = "#141922",
            AppbarBackground = "#141922",
            AppbarText = "#eceff3",
            DrawerBackground = "#0e1218",
            DrawerText = "#eceff3",
            TextPrimary = "#eceff3",
            TextSecondary = "#8e98a8",
            LinesDefault = "#252c39",
            LinesInputs = "#2c3441",
            Divider = "#252c39",
            ActionDefault = "#8e98a8",
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "0.5rem",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = SansFont,
            },
            // Knöpfe in normaler Schreibung statt in Großbuchstaben, wie im Original.
            Button = new ButtonTypography
            {
                FontFamily = SansFont,
                TextTransform = "none",
                LetterSpacing = "normal",
            },
            // Überschriften in einer Serifenschrift, wie im Original.
            H1 = Heading("2.25rem"),
            H2 = Heading("1.875rem"),
            H3 = Heading("1.5rem"),
            H4 = Heading("1.25rem"),
            H5 = Heading("1.125rem"),
            H6 = Heading("1rem"),
        },
    };

    private static H1Typography Heading(string size) => new()
    {
        FontFamily = ["Iowan Old Style", "Palatino Linotype", "Palatino", "Georgia", "serif"],
        FontSize = size,
        FontWeight = "600",
    };
}
