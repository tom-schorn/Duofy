using Android.App;
using Android.Content;
using Android.Content.PM;
using Duofy.Services.Push;
using Microsoft.Extensions.DependencyInjection;
using Android.Content.Res;
using AColor = Android.Graphics.Color;
using Android.OS;
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
            base.OnCreate(savedInstanceState);
            KeepContentInsideSystemBars();
            WatchThemeChoice();
            AskForNotificationPermission();
            OpenFromNotification(Intent);
        }

        protected override void OnDestroy()
        {
            // Die Standard-SharedPreferences, in denen Preferences.Default schreibt; hier wieder abmelden.
            GetSharedPreferences(PreferencesFileName, FileCreationMode.Private)?.UnregisterOnSharedPreferenceChangeListener(this);
            base.OnDestroy();
        }

        // Ein Tipp auf die Benachrichtigung, während die App schon läuft (SingleTop): neue Absicht statt Neustart.
        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            OpenFromNotification(intent);
        }

        // Erinnerungen (Spec 84): Ab Android 13 zeigt das System Benachrichtigungen nur, wenn der Benutzer es erlaubt hat.
        private void AskForNotificationPermission()
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(33)
                && CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Permission.Granted)
            {
                RequestPermissions([Android.Manifest.Permission.PostNotifications], 1);
            }
        }

        // Die Adresse, die die Benachrichtigung mitgibt, geht an die Hauptseite; nur Wege innerhalb der App zählen.
        private static void OpenFromNotification(Intent? intent)
        {
            if (intent?.GetStringExtra(AndroidReminderNotifier.PathExtra) is { } path && NavigationRequests.IsAppPath(path))
            {
                PushServices.Root?.GetService<NavigationRequests>()?.Request(path);
            }
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
                RunOnUiThread(PaintSystemBarArea);
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
