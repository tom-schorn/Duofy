using Android.App;
using Duofy.Push;
using Firebase.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Services.Push
{
    /// <summary>
    /// Empfängt die stillen Signale von Firebase Cloud Messaging (Spec 84, R1): Datenfelder mit der Kennung der
    /// Erinnerung und der Art, sonst nichts. Der Text kommt danach mit Anmeldung vom Server. Ein neues Token meldet das
    /// Gerät neu an.
    /// </summary>
    [Service(Exported = false)]
    [IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
    public class PushMessagingService : FirebaseMessagingService
    {
        public override void OnMessageReceived(RemoteMessage message)
        {
            var data = message.Data is { } raw
                ? raw.ToDictionary(pair => pair.Key, pair => pair.Value)
                : new Dictionary<string, string>();
            if (!PushSignal.TryParse(data, out var signal) || signal is null)
            {
                // Alles, was nicht genau ein Signal ist, wird ignoriert.
                return;
            }
            try
            {
                // Android gibt dem Dienst etwa zehn Sekunden; länger darf das Holen des Textes nicht dauern.
                var work = Task.Run(() => PushServices.RunAsync(services => services.GetRequiredService<ReminderSignalHandler>().HandleAsync(signal)));
                if (!work.Wait(TimeSpan.FromSeconds(9)))
                {
                    Android.Util.Log.Warn("Duofy", $"Signal {signal.Type} ({signal.ReminderId}) brauchte zu lange.");
                }
            }
            // Jeder Fehler wird nur protokolliert: Der Dienst darf die App nie zum Absturz bringen.
            catch (Exception exception)
            {
                Android.Util.Log.Warn("Duofy", $"Signal {signal.Type} ({signal.ReminderId}) gescheitert: {(exception as AggregateException)?.InnerException?.Message ?? exception.Message}");
            }
        }

        public override void OnNewToken(string token)
        {
            base.OnNewToken(token);
            try
            {
                Task.Run(() => PushServices.RunAsync(async services =>
                {
                    // Nur wer angemeldet ist, meldet sein Gerät an; sonst geschieht es bei der Anmeldung.
                    if (await services.GetRequiredService<Duofy.ApiClient.ITokenStore>().GetAsync() is not null)
                    {
                        await services.GetRequiredService<DeviceRegistrar>().RegisterAsync();
                    }
                })).Wait(TimeSpan.FromSeconds(9));
            }
            catch (Exception exception)
            {
                Android.Util.Log.Warn("Duofy", $"Neues Token nicht gemeldet: {(exception as AggregateException)?.InnerException?.Message ?? exception.Message}");
            }
        }
    }
}
