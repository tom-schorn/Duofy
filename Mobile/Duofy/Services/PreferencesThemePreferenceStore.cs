using Duofy.Shared.Services;

namespace Duofy.Services
{
    /// <summary>
    /// Merkt sich die Wahl "hell oder dunkel" in den Preferences des Telefons,
    /// also pro Gerät und über das Schließen der App hinaus.
    /// </summary>
    public class PreferencesThemePreferenceStore : IThemePreferenceStore
    {
        private const string Key = "duofy.theme_dark";

        public Task<bool?> LoadIsDarkAsync()
        {
            bool? result = Preferences.Default.ContainsKey(Key)
                ? Preferences.Default.Get(Key, false)
                : null;
            return Task.FromResult(result);
        }

        public Task SaveIsDarkAsync(bool isDark)
        {
            Preferences.Default.Set(Key, isDark);
            return Task.CompletedTask;
        }
    }
}
