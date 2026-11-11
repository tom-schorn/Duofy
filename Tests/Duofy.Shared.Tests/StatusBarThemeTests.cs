namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 101, A5: Die Symbole der Android-Statusleiste sind im hellen Theme dunkel und im dunklen hell. Ohne Emulator
/// wird der Quelltext der Activity geprüft; die Prüfung auf dem Gerät steht im Bericht.
/// </summary>
public class StatusBarThemeTests
{
    private static string Activity => File.ReadAllText(RepositoryFiles.Path("Mobile", "Duofy", "Platforms", "Android", "MainActivity.cs"));

    [Fact]
    public void A5_status_bar_icons_follow_the_theme()
    {
        var activity = Activity;

        // Dunkle Symbole nur im hellen Theme: AppearanceLightStatusBars bedeutet "heller Hintergrund, dunkle Symbole".
        Assert.Contains("AppearanceLightStatusBars = !isDark", activity);
        Assert.Contains("WindowInsetsControllerCompat", activity);
    }

    // Die Wahl in der App (hell, dunkel, wie das Gerät) gilt, nicht nur der Systemmodus; sie wird bei Änderung neu angewendet.
    [Fact]
    public void The_choice_in_the_app_decides_and_a_change_is_applied_at_once()
    {
        var activity = Activity;

        Assert.Contains("duofy.theme_dark", activity);
        Assert.Contains("OnSharedPreferenceChangeListener", activity);
        Assert.Contains("UnregisterOnSharedPreferenceChangeListener", activity);
    }

    // Der Hörer muss auf der Datei lauschen, in die Preferences.Default (ohne sharedName) wirklich schreibt:
    // die Standard-SharedPreferences "<Paket>_preferences". Das ist eine Quelltextprüfung; dass die Leiste auf dem
    // Gerät mitgeht, zeigt nur der Emulator.
    [Fact]
    public void The_listener_watches_the_file_preferences_default_writes_to()
    {
        var activity = Activity;

        Assert.Contains("$\"{PackageName}_preferences\"", activity);
        Assert.DoesNotContain("microsoft.maui.essentials.preferences\";", activity);
        Assert.Contains("DarkChoice() is { } choice", activity);
    }
}
