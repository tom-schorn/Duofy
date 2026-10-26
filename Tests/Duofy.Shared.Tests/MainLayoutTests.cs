using Bunit;
using Duofy.Shared.Layout;
using Duofy.Shared.Services;
using Duofy.Shared.Theme;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Duofy.Shared.Tests;

/// <summary>
/// A19: Das gemeinsame Layout bringt Theme, App-Leiste und Navigation mit,
/// und der Modus-Schalter schaltet den dunklen Modus ein und merkt sich die Wahl.
/// </summary>
public class MainLayoutTests : UiTestContext
{
    private readonly FakeThemePreferenceStore store = new();

    public MainLayoutTests()
    {
        Services.AddSingleton<IThemePreferenceStore>(store);
        AddAuthorization();
    }

    [Fact]
    public void Layout_contains_theme_provider_with_duofy_theme_app_bar_and_navigation()
    {
        var cut = Render<MainLayout>();

        var themeProvider = cut.FindComponent<MudThemeProvider>();
        Assert.Same(DuofyTheme.Instance, themeProvider.Instance.Theme);
        Assert.NotEmpty(cut.FindComponents<MudAppBar>());
        Assert.NotEmpty(cut.FindComponents<MudNavMenu>());
    }

    [Fact]
    public void Mode_switch_turns_dark_mode_on_and_passes_choice_to_store()
    {
        var cut = Render<MainLayout>();
        Assert.False(cut.FindComponent<MudThemeProvider>().Instance.IsDarkMode);

        cut.Find(".mode-switch").Click();

        Assert.True(cut.FindComponent<MudThemeProvider>().Instance.IsDarkMode);
        Assert.Equal(true, store.Saved);
    }

    [Fact]
    public void Saved_dark_choice_is_applied_on_start()
    {
        store.Stored = true;

        var cut = Render<MainLayout>();

        cut.WaitForAssertion(() =>
            Assert.True(cut.FindComponent<MudThemeProvider>().Instance.IsDarkMode));
    }

    private sealed class FakeThemePreferenceStore : IThemePreferenceStore
    {
        public bool? Stored { get; set; }
        public bool? Saved { get; private set; }

        public Task<bool?> LoadIsDarkAsync() => Task.FromResult(Stored);

        public Task SaveIsDarkAsync(bool isDark)
        {
            Saved = isDark;
            return Task.CompletedTask;
        }
    }
}
