using Duofy.Push;
using Duofy.Services.Push;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;

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

        // Erinnerungen (Spec 84): Die Knöpfe "Bezahlt" und "Später" der App-Benachrichtigung kommen hier an, auch wenn die
        // Benachrichtigung die App erst startet. Ein Tipp ohne Knopf öffnet die App an der Adresse des Postens.
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            base.OnLaunched(args);
            try
            {
                AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
                AppNotificationManager.Default.Register();
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // Ohne Paketidentität (unverpackt am PC) gibt es keine App-Benachrichtigungen; die App läuft normal.
            }
        }

        private static void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
        {
            args.Arguments.TryGetValue(WindowsReminderNotifier.ActionKey, out var action);
            args.Arguments.TryGetValue(WindowsReminderNotifier.PathKey, out var path);
            if (!args.Arguments.TryGetValue(WindowsReminderNotifier.ReminderKey, out var id) || !Guid.TryParse(id, out var reminderId))
            {
                return;
            }

            if (action is WindowsReminderNotifier.PaidValue or WindowsReminderNotifier.LaterValue)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await PushServices.RunAsync(async services =>
                        {
                            var actions = services.GetRequiredService<ReminderActionHandler>();
                            await (action == WindowsReminderNotifier.PaidValue ? actions.PaidAsync(reminderId) : actions.LaterAsync(reminderId));
                        });
                    }
                    catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or TaskCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine($"Aktion {action} der Erinnerung {reminderId} gescheitert: {exception.Message}");
                    }
                });
            }
            else if (NavigationRequests.IsAppPath(path))
            {
                PushServices.Root?.GetService<NavigationRequests>()?.Request(path!);
            }
        }
    }
}
