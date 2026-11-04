using System.Runtime.InteropServices;
using Duofy.Push;
using Duofy.Schemas;
using Microsoft.Extensions.DependencyInjection;
using Windows.Networking.PushNotifications;

namespace Duofy.Services.Push
{
    /// <summary>
    /// Der WNS-Kanal dieser Windows-App (Spec 84). Raw-Benachrichtigungen, solange die App läuft, kommen hier an und gehen
    /// an denselben Behandler wie das Signal von FCM. Ohne Paketidentität (die unverpackte Testversion am PC) gibt es keinen
    /// Kanal: Dann bleibt das Gerät ohne Erinnerungen, und die App läuft normal. Dass ein Signal die geschlossene App
    /// erreicht, braucht den Store-Build mit Hintergrundaufgabe (siehe docs/operations, Erinnerungen).
    /// </summary>
    public class WindowsPushTokenSource : IPushTokenSource
    {
        private PushNotificationChannel? channel;

        public string Platform => DevicePlatforms.Windows;

        public async Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                channel ??= await PushNotificationChannelManager.CreatePushNotificationChannelForApplicationAsync();
                channel.PushNotificationReceived -= OnReceived;
                channel.PushNotificationReceived += OnReceived;
                return channel.Uri;
            }
            catch (Exception exception) when (exception is COMException or InvalidOperationException)
            {
                // Unverpackt oder ohne Store-Identität: keine Erinnerungen auf diesem Gerät.
                return null;
            }
        }

        private static void OnReceived(PushNotificationChannel sender, PushNotificationReceivedEventArgs args)
        {
            if (args.NotificationType != PushNotificationType.Raw)
            {
                return;
            }
            // Die Raw-Benachrichtigung ist unser Signal; Windows soll daraus nichts zeigen.
            args.Cancel = true;
            if (!PushSignal.TryParseJson(args.RawNotification.Content, out var signal) || signal is null)
            {
                return;
            }
            _ = Task.Run(async () =>
            {
                try
                {
                    await PushServices.RunAsync(services => services.GetRequiredService<ReminderSignalHandler>().HandleAsync(signal));
                }
                // Jeder Fehler wird nur protokolliert: Ein Signal darf die App nie zum Absturz bringen.
                catch (Exception exception)
                {
                    System.Diagnostics.Debug.WriteLine($"Signal {signal.Type} ({signal.ReminderId}) gescheitert: {exception.Message}");
                }
            });
        }
    }
}
