using Bunit;
using Duofy.Shared.Layout;
using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 5 (hell und dunkel), geändert durch Spec 82 R11 (Tom, 03.11.2026: "1 hätte geriecht"): ein einziger Knopf wechselt
/// zum anderen Modus, die Wahl bleibt erhalten, ohne Wahl gilt das Gerät, alte Wahlen (hell, dunkel) bleiben gültig.
/// Den Wechsel des Geräts spielt der Test über ThemeVariables nach,
/// so wie das JavaScript im Browser ihn meldet. Der echte Browser ist damit nicht gesehen.
/// </summary>
public class ThemeSwitchTests : UiTestContext
{
    private readonly MemoryStore store = new();

    public ThemeSwitchTests()
    {
        Services.AddSingleton<IThemePreferenceStore>(store);
        AddAuthorization();
    }

    private IRenderedComponent<MainLayout> OpenLayout() => Render<MainLayout>();

    private static bool IsDark(IRenderedComponent<MainLayout> cut) => cut.FindComponent<Duofy.Shared.Layout.ThemeVariables>().Instance.Dark;

    private static string Pressed(IRenderedComponent<MainLayout> cut) =>
        Assert.Single(cut.FindAll("[data-testid='theme-toggle']")).GetAttribute("aria-pressed")!;

    // A1: genau ein Knopf, mit Beschriftung für Screenreader; gedrückt heißt dunkel.
    [Fact]
    public void A1_there_is_exactly_one_named_toggle()
    {
        var cut = OpenLayout();

        var toggle = Assert.Single(cut.FindAll("[data-testid='theme-toggle']"));
        Assert.Equal("Dunkles Design", toggle.GetAttribute("aria-label"));
        Assert.Empty(cut.FindAll("[data-testid='theme-choice']"));
        Assert.Equal("false", Pressed(cut));
    }

    // A1: Der Knopf wechselt zum anderen Modus und merkt sich die Wahl.
    [Theory]
    [InlineData(ThemeChoice.Light, ThemeChoice.Dark, true)]
    [InlineData(ThemeChoice.Dark, ThemeChoice.Light, false)]
    public void A1_the_toggle_switches_to_the_other_mode_and_applies_it(ThemeChoice stored, ThemeChoice expected, bool dark)
    {
        store.Stored = stored;
        var cut = OpenLayout();
        cut.WaitForAssertion(() => Assert.Equal(stored == ThemeChoice.Dark ? "true" : "false", Pressed(cut)));

        cut.Find("[data-testid='theme-toggle']").Click();

        Assert.Equal(dark ? "true" : "false", Pressed(cut));
        Assert.Equal(dark, IsDark(cut));
        Assert.Equal(expected, store.Saved);
    }

    // A2: Dunkel speichern, neu laden: wieder Dunkel (Ablage im Browser).
    [Fact]
    public async Task A2_saved_dark_choice_is_loaded_as_dark_from_local_storage()
    {
        JSInterop.SetupVoid("localStorage.setItem", _ => true).SetVoidResult();
        var local = new LocalStorageThemePreferenceStore(JSInterop.JSRuntime);

        await local.SaveAsync(ThemeChoice.Dark);

        var written = Assert.Single(JSInterop.Invocations["localStorage.setItem"]);
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult((string?)written.Arguments[1]);
        Assert.Equal(ThemeChoice.Dark, await local.LoadAsync());
    }

    // A2: Dasselbe über die Oberfläche: Wahl Dunkel gespeichert, neues Layout startet dunkel.
    [Fact]
    public async Task A2_a_new_layout_starts_with_the_saved_choice()
    {
        var cut = OpenLayout();

        cut.Find("[data-testid='theme-toggle']").Click();

        Assert.Equal(ThemeChoice.Dark, await store.LoadAsync());
    }

    // A2: Hell und Gerät werden ebenfalls erhalten; Gerät löscht den Eintrag.
    [Fact]
    public async Task A2_light_is_stored_as_false_and_device_removes_the_entry()
    {
        JSInterop.SetupVoid("localStorage.setItem", _ => true).SetVoidResult();
        JSInterop.SetupVoid("localStorage.removeItem", _ => true).SetVoidResult();
        var local = new LocalStorageThemePreferenceStore(JSInterop.JSRuntime);

        await local.SaveAsync(ThemeChoice.Light);
        await local.SaveAsync(ThemeChoice.Device);

        Assert.Equal("false", Assert.Single(JSInterop.Invocations["localStorage.setItem"]).Arguments[1]);
        Assert.Equal("duofy-theme-dark", Assert.Single(JSInterop.Invocations["localStorage.removeItem"]).Arguments[0]);
    }

    // A3: Ohne gespeicherte Wahl gilt das Gerät; hier ist es hell.
    [Fact]
    public void A3_without_a_saved_choice_the_device_applies()
    {
        store.Stored = ThemeChoice.Device;

        var cut = OpenLayout();

        cut.WaitForAssertion(() => Assert.Equal("false", Pressed(cut)));
        Assert.False(IsDark(cut));
        Assert.Null(store.Saved);
    }

    [Fact]
    public async Task A3_no_entry_in_local_storage_means_device()
    {
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult(null);
        var local = new LocalStorageThemePreferenceStore(JSInterop.JSRuntime);

        Assert.Equal(ThemeChoice.Device, await local.LoadAsync());
    }

    // A5: Früher gespeicherte Werte true und false werden als Dunkel und Hell gelesen.
    [Theory]
    [InlineData("true", ThemeChoice.Dark)]
    [InlineData("false", ThemeChoice.Light)]
    public async Task A5_old_boolean_values_in_local_storage_read_as_dark_and_light(string stored, ThemeChoice expected)
    {
        JSInterop.Setup<string?>("localStorage.getItem", "duofy-theme-dark").SetResult(stored);
        var local = new LocalStorageThemePreferenceStore(JSInterop.JSRuntime);

        Assert.Equal(expected, await local.LoadAsync());
    }

    // A5: Eine früher gespeicherte Dunkel-Wahl zeigt im Layout Dunkel als aktiv.
    [Fact]
    public void A5_a_formerly_saved_dark_choice_shows_dark_as_active()
    {
        JSInterop.Setup<string?>("localStorage.getItem", "duofy-theme-dark").SetResult("true");
        Services.AddSingleton<IThemePreferenceStore>(sp => new LocalStorageThemePreferenceStore(JSInterop.JSRuntime));

        var cut = OpenLayout();

        cut.WaitForAssertion(() => Assert.Equal("true", Pressed(cut)));
        Assert.True(IsDark(cut));
    }

    private sealed class MemoryStore : IThemePreferenceStore
    {
        public ThemeChoice Stored { get; set; } = ThemeChoice.Device;
        public ThemeChoice? Saved { get; private set; }

        public Task<ThemeChoice> LoadAsync() => Task.FromResult(Stored);

        public Task SaveAsync(ThemeChoice choice)
        {
            Saved = choice;
            Stored = choice;
            return Task.CompletedTask;
        }
    }
}
