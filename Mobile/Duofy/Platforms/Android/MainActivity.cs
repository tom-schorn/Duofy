using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using AColor = Android.Graphics.Color;
using Android.OS;
using AndroidX.Core.View;

namespace Duofy
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        // Dieselben Hintergrundfarben wie im Theme (Duofy.Shared/wwwroot/theme.css).
        private static readonly AColor LightBackground = AColor.ParseColor("#fbf9f4");
        private static readonly AColor DarkBackground = AColor.ParseColor("#0b0e14");

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            KeepContentInsideSystemBars();
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

        // Der Streifen hinter den Systemleisten zeigt die Hintergrundfarbe der Inhaltsfläche.
        // Sie folgt dem Systemmodus (hell/dunkel); die Schalter der App kennt Android nicht.
        private void PaintSystemBarArea()
        {
            var isDark = (Resources?.Configuration?.UiMode & UiMode.NightMask) == UiMode.NightYes;
            FindViewById(Android.Resource.Id.Content)?.SetBackgroundColor(isDark ? DarkBackground : LightBackground);
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
