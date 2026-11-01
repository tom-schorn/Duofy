using Bunit;
using Duofy.Shared.Layout;
using Duofy.Shared.Services;
using Duofy.Shared.Theme;
using Microsoft.Extensions.DependencyInjection;
using Radzen;
using Radzen.Blazor;

namespace Duofy.Shared.Tests;

/// <summary>
/// Radzen R4 (#56): Die Wahl Hell, Dunkel oder Gerät schaltet die Theme-Datei von Radzen und die Variablen ohne Neuladen um.
/// Die Einstellung des Geräts kommt von RadzenMediaQuery.
/// </summary>
public class ThemeModeTests : UiTestContext
{
    private readonly MemoryStore store = new();

    public ThemeModeTests()
    {
        Services.AddSingleton<IThemePreferenceStore>(store);
        AddAuthorization();
    }

    private ThemeService RadzenService => Services.GetRequiredService<ThemeService>();

    private static Task DeviceAsync(IRenderedComponent<MainLayout> cut, bool dark) =>
        cut.InvokeAsync(() => cut.FindComponent<RadzenMediaQuery>().Instance.OnChange(dark));

    private void AssertMode(IRenderedComponent<MainLayout> cut, bool dark)
    {
        Assert.Equal(dark ? "material-dark" : "material", RadzenService.Theme);
        Assert.Equal(dark, cut.FindComponent<ThemeVariables>().Instance.Dark);
        Assert.Equal(RadzenVariables.Css(dark), cut.FindComponent<ThemeVariables>().Find("style").TextContent);
    }

    [Fact]
    public void Choosing_dark_and_light_switches_the_theme_without_a_reload()
    {
        store.Stored = ThemeChoice.Light;
        var cut = Render<MainLayout>();
        cut.WaitForAssertion(() => AssertMode(cut, dark: false));

        cut.Find("[data-testid='theme-dark']").Click();
        AssertMode(cut, dark: true);
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "Radzen.setTheme" && ((string?)i.Arguments[0])!.Contains("material-dark"));

        cut.Find("[data-testid='theme-light']").Click();
        AssertMode(cut, dark: false);
    }

    [Theory]
    [InlineData(ThemeChoice.Dark, true)]
    [InlineData(ThemeChoice.Light, false)]
    public void The_saved_choice_applies_after_a_restart(ThemeChoice saved, bool dark)
    {
        store.Stored = saved;

        var cut = Render<MainLayout>();

        cut.WaitForAssertion(() => AssertMode(cut, dark));
    }

    [Fact]
    public void A_new_choice_is_saved()
    {
        var cut = Render<MainLayout>();

        cut.Find("[data-testid='theme-dark']").Click();

        cut.WaitForAssertion(() => Assert.Equal(ThemeChoice.Dark, store.Saved));
    }

    [Fact]
    public async Task Without_a_saved_choice_the_device_decides_and_is_followed()
    {
        var cut = Render<MainLayout>();
        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find("[data-testid='theme-device']").GetAttribute("aria-pressed")));

        await DeviceAsync(cut, dark: true);
        AssertMode(cut, dark: true);

        await DeviceAsync(cut, dark: false);
        AssertMode(cut, dark: false);
    }

    [Theory]
    [InlineData(ThemeChoice.Dark, false, true)]
    [InlineData(ThemeChoice.Light, true, false)]
    public async Task A_chosen_mode_ignores_the_device(ThemeChoice saved, bool deviceDark, bool dark)
    {
        store.Stored = saved;
        var cut = Render<MainLayout>();
        cut.WaitForAssertion(() => AssertMode(cut, dark));

        await DeviceAsync(cut, deviceDark);

        AssertMode(cut, dark);
    }

    [Fact]
    public void The_device_is_asked_through_a_media_query()
    {
        var cut = Render<MainLayout>();

        Assert.Equal("(prefers-color-scheme: dark)", cut.FindComponent<RadzenMediaQuery>().Instance.Query);
    }

    [Fact]
    public void The_layout_asks_radzen_for_the_device()
    {
        var source = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "Layout", "ThemedLayoutBase.cs"));

        Assert.DoesNotContain("GetSystemDarkModeAsync", source);
        Assert.DoesNotContain("WatchSystemDarkModeAsync", source);
    }

    [Fact]
    public void The_switch_is_drawn_with_radzen()
    {
        var cut = Render<ThemeChoiceSwitch>(p => p.Add(c => c.Choice, ThemeChoice.Dark));

        Assert.Equal(3, cut.FindComponents<RadzenButton>().Count);
        Assert.Equal("true", cut.Find("[data-testid='theme-dark']").GetAttribute("aria-pressed"));
        Assert.Equal("Dunkel", cut.Find("[data-testid='theme-dark']").GetAttribute("aria-label"));
    }

    [Fact]
    public void The_switch_keeps_its_tap_targets_and_the_colour_of_the_top_bar()
    {
        var css = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "app.css")).Replace("\r\n", "\n");

        Assert.Contains(".mode-switch .rz-button {\n    min-width: var(--tap-min);\n    min-height: var(--tap-min);", css);
        Assert.Contains(".rz-header .mode-switch .rz-button.rz-base.rz-shade-default {\n    color: inherit;", css);
    }

    private sealed class MemoryStore : IThemePreferenceStore
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
}
