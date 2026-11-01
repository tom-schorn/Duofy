using Bunit;
using Duofy.Shared.Layout;
using Duofy.Shared.Services;
using Duofy.Shared.Theme;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 4 (Dunkelmodus): das Layout öffnet dunkel. Die dunklen Werte und ihren Kontrast prüft ThemeVariablesTests.
/// </summary>
public class ThemeDarkTests : UiTestContext
{
    // A4 (soweit ohne echten Browser prüfbar): Das gemeinsame Layout öffnet im Dunkelmodus ohne Fehler,
    // und die Theme-Variablen tragen die dunklen Werte. Der Blick im echten Browser steht aus.
    [Fact]
    public void Main_layout_opens_in_dark_mode_with_the_dark_palette()
    {
        Services.AddSingleton<IThemePreferenceStore>(new DarkStore());
        AddAuthorization();

        var cut = Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(b => b.AddMarkupContent(0, "<p id=\"inhalt\">Hauptinhalt</p>"))));

        cut.WaitForAssertion(() => Assert.True(cut.FindComponent<Duofy.Shared.Layout.ThemeVariables>().Instance.Dark));
        Assert.Contains("Hauptinhalt", cut.Find("#inhalt").TextContent);
        Assert.Contains(RadzenVariables.Css(dark: true), cut.Markup);
    }

    private sealed class DarkStore : IThemePreferenceStore
    {
        public Task<ThemeChoice> LoadAsync() => Task.FromResult(ThemeChoice.Dark);
        public Task SaveAsync(ThemeChoice choice) => Task.CompletedTask;
    }
}
