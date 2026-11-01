using Bunit;
using Duofy.Shared.Layout;
using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 5 (Umschalter Hell, Dunkel und wie das Gerät): drei Möglichkeiten, die Wahl bleibt erhalten,
/// ohne Wahl gilt das Gerät, die Anzeige folgt dem Gerät, alte Wahlen (hell, dunkel) bleiben gültig.
/// Den Wechsel des Geräts spielt der Test über RadzenMediaQuery nach,
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

    private static string Checked(IRenderedComponent<MainLayout> cut)
    {
        var active = cut.FindAll("[data-testid^='theme-'][aria-pressed='true']");
        Assert.Single(active);
        return active[0].GetAttribute("data-testid")!;
    }

    // A1: drei Möglichkeiten, genau eine ist aktiv, mit Beschriftung für Screenreader.
    [Fact]
    public void A1_the_switch_offers_light_dark_and_device_and_exactly_one_is_active()
    {
        var cut = OpenLayout();

        var radios = cut.FindAll("[data-testid='theme-choice'] button");
        Assert.Equal(["Hell", "Dunkel", "Wie das Gerät"], radios.Select(r => r.GetAttribute("aria-label")).ToArray());
        Assert.Equal("group", cut.Find("[data-testid='theme-choice']").GetAttribute("role"));
        Assert.Empty(cut.FindAll("[data-testid='theme-choice'] [role]"));
        Assert.Equal("Darstellung", cut.Find("[data-testid='theme-choice']").GetAttribute("aria-label"));
        Assert.Single(cut.FindAll("[data-testid^='theme-'][aria-pressed='true']"));
        Assert.Equal(2, cut.FindAll("[data-testid^='theme-'][aria-pressed='false']").Count);
    }

    // A1: Wer eine Möglichkeit antippt, macht genau diese aktiv.
    [Theory]
    [InlineData("theme-light", ThemeChoice.Light, false)]
    [InlineData("theme-dark", ThemeChoice.Dark, true)]
    [InlineData("theme-device", ThemeChoice.Device, false)]
    public void A1_choosing_an_option_makes_it_the_only_active_one_and_applies_it(string testId, ThemeChoice expected, bool dark)
    {
        store.Stored = expected == ThemeChoice.Light ? ThemeChoice.Dark : ThemeChoice.Light;
        var cut = OpenLayout();
        cut.WaitForAssertion(() => Assert.NotEqual("theme-device", Checked(cut)));

        cut.Find($"[data-testid='{testId}']").Click();

        Assert.Equal(testId, Checked(cut));
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

        cut.Find("[data-testid='theme-dark']").Click();

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

    // A3: Ohne gespeicherte Wahl ist Gerät aktiv.
    [Fact]
    public void A3_without_a_saved_choice_the_device_is_active()
    {
        store.Stored = ThemeChoice.Device;

        var cut = OpenLayout();

        cut.WaitForAssertion(() => Assert.Equal("theme-device", Checked(cut)));
    }

    [Fact]
    public async Task A3_no_entry_in_local_storage_means_device()
    {
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult(null);
        var local = new LocalStorageThemePreferenceStore(JSInterop.JSRuntime);

        Assert.Equal(ThemeChoice.Device, await local.LoadAsync());
    }

    // A4: Bei Gerät und Wechsel des Geräts auf dunkel wird die Anzeige dunkel, ohne neu zu laden.
    [Fact]
    public async Task A4_with_device_the_display_follows_a_change_of_the_device()
    {
        var cut = OpenLayout();
        cut.WaitForAssertion(() => Assert.Equal("theme-device", Checked(cut)));
        Assert.False(IsDark(cut));

        await cut.InvokeAsync(() => cut.FindComponent<Radzen.Blazor.RadzenMediaQuery>().Instance.OnChange(true));
        Assert.True(IsDark(cut));

        await cut.InvokeAsync(() => cut.FindComponent<Radzen.Blazor.RadzenMediaQuery>().Instance.OnChange(false));
        Assert.False(IsDark(cut));
    }

    // A4 (Gegenprobe): Mit gewählter Hell- oder Dunkel-Wahl ändert das Gerät nichts.
    [Theory]
    [InlineData(ThemeChoice.Light, true, false)]
    [InlineData(ThemeChoice.Dark, false, true)]
    public async Task A4_an_explicit_choice_ignores_a_change_of_the_device(ThemeChoice choice, bool deviceDark, bool expectedDark)
    {
        store.Stored = choice;
        var cut = OpenLayout();
        cut.WaitForAssertion(() => Assert.Equal(choice == ThemeChoice.Dark, IsDark(cut)));

        await cut.InvokeAsync(() => cut.FindComponent<Radzen.Blazor.RadzenMediaQuery>().Instance.OnChange(deviceDark));

        Assert.Equal(expectedDark, IsDark(cut));
    }

    // A4: Wer von Hell zurück auf Gerät wechselt, bekommt die zuletzt gemeldete Geräteeinstellung.
    [Fact]
    public async Task A4_switching_back_to_device_uses_the_latest_device_setting()
    {
        store.Stored = ThemeChoice.Light;
        var cut = OpenLayout();
        cut.WaitForAssertion(() => Assert.Equal("theme-light", Checked(cut)));
        await cut.InvokeAsync(() => cut.FindComponent<Radzen.Blazor.RadzenMediaQuery>().Instance.OnChange(true));
        Assert.False(IsDark(cut));

        cut.Find("[data-testid='theme-device']").Click();

        Assert.True(IsDark(cut));
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

        cut.WaitForAssertion(() => Assert.Equal("theme-dark", Checked(cut)));
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
