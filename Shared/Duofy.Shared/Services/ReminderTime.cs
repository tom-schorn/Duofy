using System.Globalization;
using Duofy.Schemas;

namespace Duofy.Shared.Services;

/// <summary>
/// Eine Uhrzeit aus dem Zeitfeld des Geräts (Issue 117, 118). Android und der Browser liefern "HH:mm", mit Sekunden auch
/// "HH:mm:ss"; ohne Wahl ("Löschen" im Zeitfenster) kommt ein leerer Text. Die API bekommt immer "HH:mm".
/// </summary>
public static class ReminderTime
{
    private static readonly string[] Formats = ["HH:mm", "H:mm", "HH:mm:ss", "H:mm:ss", "HH:mm:ss.FFFFFFF", "H:mm:ss.FFFFFFF"];

    /// <summary>True mit der Zeit als "HH:mm", wenn der Text eine Uhrzeit ist; die Sekunden fallen weg.</summary>
    public static bool TryNormalize(string? text, out string normal)
    {
        if (TimeOnly.TryParseExact(text?.Trim(), Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            normal = time.ToString("HH:mm", CultureInfo.InvariantCulture);
            return true;
        }
        normal = "";
        return false;
    }
}

/// <summary>Die Grenzen der Vorwarnung, wie der Server sie kennt (<see cref="ReminderSettings"/> im Backend).</summary>
public static class ReminderSettingsLimits
{
    public const int MinWarningDays = 1;
    public const int MaxWarningDays = 7;
}
