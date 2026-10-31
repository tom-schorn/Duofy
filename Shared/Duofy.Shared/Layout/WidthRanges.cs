namespace Duofy.Shared.Layout;

/// <summary>
/// Die drei Breitenbereiche und die festen Maße des Rahmens, einmal festgelegt (Spec 9, R1 und R2).
/// Handy bis 599 px, Tablet 600 bis 1279 px, Desktop ab 1280 px. Das Stylesheet (wwwroot/app.css) kann keine
/// C#-Werte lesen und wiederholt sie in seinen Medienabfragen; ein Test prüft, dass beide übereinstimmen.
/// Die Doku steht in docs/layout.md.
/// </summary>
public static class WidthRanges
{
    /// <summary>Größte Breite des Handy-Bereichs in Pixeln.</summary>
    public const int PhoneMax = 599;

    /// <summary>Kleinste Breite des Tablet-Bereichs in Pixeln.</summary>
    public const int TabletMin = 600;

    /// <summary>Größte Breite des Tablet-Bereichs in Pixeln.</summary>
    public const int TabletMax = 1279;

    /// <summary>Kleinste Breite des Desktop-Bereichs in Pixeln; ab hier ist der Inhalt begrenzt und zentriert.</summary>
    public const int DesktopMin = 1280;

    /// <summary>Höchste Breite des Inhalts auf dem Desktop in Pixeln.</summary>
    public const int ContentMaxWidth = 1180;

    /// <summary>Kleinste Höhe und Breite einer Tippfläche in Pixeln.</summary>
    public const int TapTargetMin = 44;
}
