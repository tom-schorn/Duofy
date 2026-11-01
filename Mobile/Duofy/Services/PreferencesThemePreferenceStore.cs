using Duofy.Shared.Services;

namespace Duofy.Services
{
    /// <summary>
    /// Merkt sich die Wahl Hell, Dunkel oder "wie das Gerät" in den Preferences des Telefons,
    /// also pro Gerät und über das Schließen der App hinaus.
    /// </summary>
    /// <remarks>
    /// Der Schlüssel und der Wahrheitswert (true = dunkel, false = hell) stammen noch aus der Zeit mit
    /// nur zwei Zuständen, damit gespeicherte Wahlen gültig bleiben. "Wie das Gerät" ist kein Eintrag.
    /// </remarks>
    public class PreferencesThemePreferenceStore : IThemePreferenceStore
    {
        private const string Key = "duofy.theme_dark";

        public Task<ThemeChoice> LoadAsync()
        {
            var choice = !Preferences.Default.ContainsKey(Key)
                ? ThemeChoice.Device
                : Preferences.Default.Get(Key, false) ? ThemeChoice.Dark : ThemeChoice.Light;
            return Task.FromResult(choice);
        }

        // Issue 5: Preferences und Systemdarstellung sind sofort da, das Layout wendet sie vor dem ersten Zeichnen an.
        public ThemeStart? LoadImmediately()
        {
            var choice = LoadAsync().GetAwaiter().GetResult();
            var systemIsDark = AppInfo.Current.RequestedTheme == AppTheme.Dark;
            return new ThemeStart(choice, systemIsDark);
        }

        public Task SaveAsync(ThemeChoice choice)
        {
            if (choice == ThemeChoice.Device)
            {
                Preferences.Default.Remove(Key);
            }
            else
            {
                Preferences.Default.Set(Key, choice == ThemeChoice.Dark);
            }

            return Task.CompletedTask;
        }
    }
}
