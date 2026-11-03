using Bunit;
using Duofy.Shared.Layout;
using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Die Wahl Hell, Dunkel oder Gerät schaltet die Klasse dark an html ohne Neuladen um (theme.js). Die Einstellung des Geräts
/// meldet theme.js an ThemeVariables.
/// </summary>
public class ThemeModeTests : UiTestContext
{
    private readonly MemoryStore store = new();
    private readonly BunitJSModuleInterop module;

    public ThemeModeTests()
    {
        Services.AddSingleton<IThemePreferenceStore>(store);
        AddAuthorization();
        module = JSInterop.SetupModule("./_content/Duofy.Shared/theme.js");
        module.Mode = JSRuntimeMode.Loose;
    }

    private static Task DeviceAsync(IRenderedComponent<MainLayout> cut, bool dark) =>
        cut.InvokeAsync(() => cut.FindComponent<ThemeVariables>().Instance.OnDeviceDarkChanged(dark));

    private void AssertMode(IRenderedComponent<MainLayout> cut, bool dark)
    {
        Assert.Equal(dark, cut.FindComponent<ThemeVariables>().Instance.Dark);
        var last = module.Invocations.Last(i => i.Identifier == "apply");
        Assert.Equal(dark, last.Arguments[0]);
    }

    [Fact]
    public void Choosing_dark_and_light_switches_the_theme_without_a_reload()
    {
        store.Stored = ThemeChoice.Light;
        var cut = Render<MainLayout>();
        cut.WaitForAssertion(() => AssertMode(cut, dark: false));

        cut.Find("[data-testid='theme-toggle']").Click();
        cut.WaitForAssertion(() => AssertMode(cut, dark: true));

        cut.Find("[data-testid='theme-toggle']").Click();
        cut.WaitForAssertion(() => AssertMode(cut, dark: false));
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

        cut.Find("[data-testid='theme-toggle']").Click();

        cut.WaitForAssertion(() => Assert.Equal(ThemeChoice.Dark, store.Saved));
    }

    [Fact]
    public async Task Without_a_saved_choice_the_device_decides_and_is_followed()
    {
        var cut = Render<MainLayout>();
        cut.WaitForAssertion(() => Assert.Equal("false", cut.Find("[data-testid='theme-toggle']").GetAttribute("aria-pressed")));
        Assert.Null(store.Saved);

        await DeviceAsync(cut, dark: true);
        cut.WaitForAssertion(() => AssertMode(cut, dark: true));

        await DeviceAsync(cut, dark: false);
        cut.WaitForAssertion(() => AssertMode(cut, dark: false));
    }

    // Solange das Gerät nicht geantwortet hat, bleibt die Farbe des frühen Skripts: Sonst blitzte es hell auf.
    [Fact]
    public void Before_the_device_has_answered_the_class_stays_as_the_early_script_set_it()
    {
        var cut = Render<MainLayout>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='theme-toggle']")));

        Assert.DoesNotContain(module.Invocations, i => i.Identifier == "apply");
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

        cut.WaitForAssertion(() => AssertMode(cut, dark));
    }

    [Fact]
    public void The_device_is_asked_through_a_media_query()
    {
        var script = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "theme.js"));

        Render<MainLayout>();

        Assert.Contains("(prefers-color-scheme: dark)", script);
        Assert.Contains(module.Invocations, i => i.Identifier == "watch");
    }

    [Fact]
    public void The_toggle_is_one_named_button_that_switches_to_the_other_mode()
    {
        ThemeChoice? chosen = null;
        var cut = Render<ThemeToggle>(p => p
            .Add(c => c.Dark, true)
            .Add(c => c.ChoiceSelected, (ThemeChoice c) => chosen = c));

        Assert.Single(cut.FindAll("button"));
        var button = cut.Find("[data-testid='theme-toggle']");
        Assert.Equal("true", button.GetAttribute("aria-pressed"));
        Assert.Equal("Dunkles Design", button.GetAttribute("aria-label"));

        button.Click();

        Assert.Equal(ThemeChoice.Light, chosen);
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
