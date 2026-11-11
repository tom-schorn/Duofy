using Microsoft.UI.Xaml;

namespace Duofy.WinUI
{
    // Nur zum Testen am PC; ausgeliefert wird weiter nur Android.
    public partial class App : MauiWinUIApplication
    {
        public App()
        {
            this.InitializeComponent();
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
