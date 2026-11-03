namespace Duofy.Shared.Layout;

/// <summary>
/// Die drei Breitenbereiche, einmal festgelegt: Handy bis 767 px, Tablet 768 bis 1279 px, PC ab 1280 px (Spec 82, Änderung vom 03.11.2026). Das
/// Stylesheet (wwwroot/duofy.css) kann keine C#-Werte lesen und wiederholt sie in seinen Medienabfragen; ein Test prüft,
/// dass beide übereinstimmen.
/// </summary>
public static class WidthRanges
{
    /// <summary>Größte Breite des Handy-Bereichs in Pixeln.</summary>
    public const int PhoneMax = 767;

    /// <summary>Kleinste Breite des Tablet-Bereichs in Pixeln.</summary>
    public const int TabletMin = 768;

    /// <summary>Größte Breite des Tablet-Bereichs in Pixeln.</summary>
    public const int TabletMax = 1279;

    /// <summary>Kleinste Breite des PC-Bereichs in Pixeln; ab hier steht die Seitenleiste ausgeklappt und die Erklärspalte.</summary>
    public const int DesktopMin = 1280;
}
