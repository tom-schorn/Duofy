namespace Duofy.Shared.Layout;

/// <summary>
/// Die Breitenstufen, einmal festgelegt (Spec 101, Entscheidung vom 05.11.2026): Handy bis 599 px mit der unteren Leiste, Tablet
/// 600 bis 839 px mit der eingeklappten Seitenleiste, ab 840 px die volle Seitenleiste, ab 1280 px zusätzlich die Erklärspalte.
/// Das Stylesheet (wwwroot/duofy.css) kann keine C#-Werte lesen und wiederholt sie in seinen Medienabfragen; ein Test prüft,
/// dass beide übereinstimmen.
/// </summary>
public static class WidthRanges
{
    /// <summary>Größte Breite des Handy-Bereichs in Pixeln.</summary>
    public const int PhoneMax = 599;

    /// <summary>Kleinste Breite des Tablet-Bereichs in Pixeln.</summary>
    public const int TabletMin = 600;

    /// <summary>Größte Breite des Tablet-Bereichs in Pixeln.</summary>
    public const int TabletMax = 839;

    /// <summary>Kleinste Breite des PC-Bereichs in Pixeln; ab hier steht die Seitenleiste ausgeklappt.</summary>
    public const int DesktopMin = 840;

    /// <summary>Kleinste Breite, ab der rechts die Erklärspalte steht.</summary>
    public const int HelpColumnMin = 1280;

    /// <summary>Größte Breite, bei der noch mit dem Finger getippt wird (Tippflächen mindestens 44 px); eins unter der Erklärspalte.</summary>
    public const int TouchMax = HelpColumnMin - 1;
}
