using Duofy;

namespace Duofy.App.Tests;

/// <summary>
/// Spec 124, A5 (R5): Die Datums- und Zeitwähler von Android folgen dem Theme der App, nicht dem Nachtmodus des Geräts. Die App setzt
/// dazu den Nachtmodus der Activity aus ihrer eigenen Wahl. Ein Gerät lässt sich hier nicht bedienen; geprüft wird die Einstellung
/// im Code, das Aussehen auf dem Handy prüft die Leitung.
/// </summary>
public class AndroidNightModeTests
{
    private static readonly string MainActivity =
        File.ReadAllText(RepositoryFiles.Path("Mobile", "Duofy", "Platforms", "Android", "MainActivity.cs")).Replace("\r\n", "\n");

    // Light theme on a dark device: the choice "hell" must give MODE_NIGHT_NO, not the device's mode.
    [Fact]
    public void The_light_app_theme_gives_light_system_dialogs_even_on_a_dark_device()
    {
        Assert.Equal(NightModeChoice.No, NightModeChoice.For(darkChoice: false));
        Assert.Equal(1, NightModeChoice.No); // AppCompatDelegate.MODE_NIGHT_NO
    }

    [Fact]
    public void The_dark_app_theme_gives_dark_system_dialogs_even_on_a_light_device()
    {
        Assert.Equal(NightModeChoice.Yes, NightModeChoice.For(darkChoice: true));
        Assert.Equal(2, NightModeChoice.Yes); // AppCompatDelegate.MODE_NIGHT_YES
    }

    [Fact]
    public void Without_a_choice_the_app_follows_the_device()
    {
        Assert.Equal(NightModeChoice.FollowSystem, NightModeChoice.For(darkChoice: null));
        Assert.Equal(-1, NightModeChoice.FollowSystem); // AppCompatDelegate.MODE_NIGHT_FOLLOW_SYSTEM
    }

    [Fact]
    public void The_activity_sets_the_night_mode_before_it_is_created_and_again_when_the_choice_changes()
    {
        Assert.Contains("AppCompatDelegate.DefaultNightMode = NightModeChoice.For(DarkChoice())", MainActivity);

        var onCreate = MainActivity[MainActivity.IndexOf("protected override void OnCreate", StringComparison.Ordinal)..];
        Assert.True(onCreate.IndexOf("ApplyNightMode();", StringComparison.Ordinal) < onCreate.IndexOf("base.OnCreate(", StringComparison.Ordinal),
            "ApplyNightMode muss vor base.OnCreate laufen");

        var changed = MainActivity[MainActivity.IndexOf("public void OnSharedPreferenceChanged", StringComparison.Ordinal)..];
        Assert.Contains("ApplyNightMode();", changed[..changed.IndexOf("PaintSystemBarArea();", StringComparison.Ordinal)]);
    }

    [Fact]
    public void The_activity_reads_the_same_preference_key_the_theme_store_writes()
    {
        var store = File.ReadAllText(RepositoryFiles.Path("Mobile", "Duofy", "Services", "PreferencesThemePreferenceStore.cs"));

        Assert.Contains("\"duofy.theme_dark\"", store);
        Assert.Contains("private const string ThemeKey = \"duofy.theme_dark\"", MainActivity);
    }
}
