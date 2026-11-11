using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using AColor = Android.Graphics.Color;
using Android.OS;
using AndroidX.AppCompat.App;
using AndroidX.Core.View;

namespace Duofy
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity, ISharedPreferencesOnSharedPreferenceChangeListener
    {
        // Dieselben Hintergrundfarben wie im Theme (Duofy.Shared/wwwroot/theme.css).
        private static readonly AColor LightBackground = AColor.ParseColor("#fbf9f4");
        private static readonly AColor DarkBackground = AColor.ParseColor("#0b0e14");

        // Schlüssel der Wahl hell/dunkel in den Preferences (PreferencesThemePreferenceStore); true = dunkel, fehlt = wie das Gerät.
        private const string ThemeKey = "duofy.theme_dark";

        // Preferences.Default ohne sharedName liegt bei MAUI in den Standard-SharedPreferences von Android
        // (PreferenceManager.GetDefaultSharedPreferences), also in der Datei "<Paket>_preferences". Die Datei
        // "<Paket>.microsoft.maui.essentials.preferences" gehört nur Preferences mit sharedName und bekommt die Wahl nie.
        private string PreferencesFileName => $"{PackageName}_preferences";

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            // Vor base.OnCreate, damit schon das erste Fenster der Activity den richtigen Nachtmodus hat (Spec 124, R5).
            ApplyNightMode();
            base.OnCreate(savedInstanceState);
            KeepContentInsideSystemBars();
            WatchThemeChoice();
        }

        protected override void OnDestroy()
        {
            // Die Standard-SharedPreferences, in denen Preferences.Default schreibt; hier wieder abmelden.
            GetSharedPreferences(PreferencesFileName, FileCreationMode.Private)?.UnregisterOnSharedPreferenceChangeListener(this);
            base.OnDestroy();
        }

        public override void OnConfigurationChanged(Configuration newConfig)
        {
            base.OnConfigurationChanged(newConfig);
            PaintSystemBarArea();
        }

        // Ab Android 15 (targetSdk 35) zeichnet jede App unter die Statusleiste und die
        // Navigationsleiste ("edge-to-edge"); die Web-Oberfläche läge dann unter der Uhr.
        // Wir schieben deshalb die ganze Inhaltsfläche um die Höhe der Systemleisten (und der
        // Tastatur) nach innen. So bleibt die Oberfläche selbst unverändert und braucht keine
        // Sonderfälle fürs Handy; ein env(safe-area-inset-*) im WebView wäre je nach
        // WebView-Version nicht verlässlich.
        private void KeepContentInsideSystemBars()
        {
            var content = FindViewById(Android.Resource.Id.Content);
            if (content is null) return;

            ViewCompat.SetOnApplyWindowInsetsListener(content, new InsetsListener());
            PaintSystemBarArea();
            ViewCompat.RequestApplyInsets(content);
        }

        // Die Wahl der Person in der App (true = dunkel), oder null, wenn sie keine getroffen hat. Direkt aus den SharedPreferences gelesen,
        // denn Preferences.Default gehört zu MAUI und ist vor base.OnCreate noch nicht verlässlich bereit.
        private bool? DarkChoice()
        {
            var preferences = GetSharedPreferences(PreferencesFileName, FileCreationMode.Private);
            return preferences is not null && preferences.Contains(ThemeKey) ? preferences.GetBoolean(ThemeKey, false) : null;
        }

        // Spec 124, R5: Die Datums- und Zeitwähler sind Fenster des Systems; sie nehmen den Nachtmodus der Activity, nicht das Theme der
        // Web-Oberfläche. Ohne diese Zeile waren sie auf einem Gerät im Dunkelmodus dunkel, obwohl die App hell war (und umgekehrt).
        // AppCompat wendet den Wert auf die offene Activity an und meldet ihr die geänderte Konfiguration (UiMode ist in
        // ConfigurationChanges, die Activity wird also nicht neu gebaut).
        private void ApplyNightMode() => AppCompatDelegate.DefaultNightMode = NightModeChoice.For(DarkChoice());

        // Die Wahl in der App gilt (Preferences, true = dunkel); ohne Wahl folgt die App dem Systemmodus.
        private bool IsDark()
        {
            if (Preferences.Default.ContainsKey(ThemeKey))
            {
                return Preferences.Default.Get(ThemeKey, false);
            }
            return (Resources?.Configuration?.UiMode & UiMode.NightMask) == UiMode.NightYes;
        }

        // Ändert die Person das Theme in der App, soll die Statusleiste sofort mitgehen, nicht erst beim nächsten Start.
        private void WatchThemeChoice() =>
            GetSharedPreferences(PreferencesFileName, FileCreationMode.Private)?.RegisterOnSharedPreferenceChangeListener(this);

        public void OnSharedPreferenceChanged(ISharedPreferences? sharedPreferences, string? key)
        {
            if (key == ThemeKey || key is null)
            {
                RunOnUiThread(() =>
                {
                    ApplyNightMode();
                    PaintSystemBarArea();
                });
            }
        }

        // Der Streifen hinter den Systemleisten zeigt die Hintergrundfarbe der Inhaltsfläche, und die Symbole der Statusleiste
        // (Uhr, Akku, Netz) bekommen die Gegenfarbe: dunkel auf hellem Theme, hell auf dunklem.
        private void PaintSystemBarArea()
        {
            var isDark = IsDark();
            FindViewById(Android.Resource.Id.Content)?.SetBackgroundColor(isDark ? DarkBackground : LightBackground);
            if (Window is { } window)
            {
                // AppearanceLightStatusBars = true heißt: heller Hintergrund, dunkle Symbole.
                new WindowInsetsControllerCompat(window, window.DecorView).AppearanceLightStatusBars = !isDark;
            }
        }

        private sealed class InsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
        {
            public WindowInsetsCompat? OnApplyWindowInsets(Android.Views.View? v, WindowInsetsCompat? insets)
            {
                if (v is null || insets is null) return insets;

                var types = WindowInsetsCompat.Type.SystemBars() | WindowInsetsCompat.Type.DisplayCutout() | WindowInsetsCompat.Type.Ime();
                var bars = insets.GetInsets(types)!;
                v.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);
                return WindowInsetsCompat.Consumed;
            }
        }
    }
}
