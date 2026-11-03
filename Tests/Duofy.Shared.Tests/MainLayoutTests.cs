using Bunit;
using Duofy.Shared.Layout;
using Duofy.Shared.Services;
using Duofy.Shared.Theme;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// A19: Das gemeinsame Layout bringt Theme, App-Leiste und Navigation mit,
/// und die Auswahl schaltet den dunklen Modus ein und merkt sich die Wahl.
/// Die Auswahl selbst (Hell, Dunkel, Gerät) prüfen die ThemeSwitchTests.
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
    public void Choosing_dark_turns_dark_mode_on_and_passes_choice_to_store()
    {
        var cut = Render<MainLayout>();
        Assert.False(cut.FindComponent<Duofy.Shared.Layout.ThemeVariables>().Instance.Dark);

        cut.Find("[data-testid='theme-toggle']").Click();

        Assert.True(cut.FindComponent<Duofy.Shared.Layout.ThemeVariables>().Instance.Dark);
        Assert.Equal(ThemeChoice.Dark, store.Saved);
    }

    [Fact]
    public void Saved_dark_choice_is_applied_on_start()
    {
        store.Stored = ThemeChoice.Dark;

        var cut = Render<MainLayout>();

        cut.WaitForAssertion(() =>
            Assert.True(cut.FindComponent<Duofy.Shared.Layout.ThemeVariables>().Instance.Dark));
    }

    private sealed class FakeThemePreferenceStore : IThemePreferenceStore
    {
        public ThemeChoice Stored { get; set; } = ThemeChoice.Device;
        public ThemeChoice? Saved { get; private set; }

        public Task<ThemeChoice> LoadAsync() => Task.FromResult(Stored);

        public Task SaveAsync(ThemeChoice choice)
        {
            Saved = choice;
            return Task.CompletedTask;
        }
    }

    // Mangel S9: Der Seiteninhalt steht in einem main-Element, sonst fehlt Screenreadern der Sprung zum Hauptinhalt.
    [Fact]
    public void The_page_content_sits_in_one_main_element_that_the_skip_link_targets()
    {
        var cut = Render<MainLayout>();

        var main = Assert.Single(cut.FindAll("main"));
        Assert.Equal("main-content", main.Id);
    }
}
