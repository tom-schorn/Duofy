using Android.App;
using Android.Content;
using Duofy.Push;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Services.Push
{
    /// <summary>
    /// Nimmt die Knöpfe "Bezahlt" und "Später" der Benachrichtigung an (Spec 84, R5). Die Arbeit läuft im Hintergrund;
    /// <c>GoAsync</c> hält den Empfänger offen, bis der Server geantwortet hat.
    /// </summary>
    [BroadcastReceiver(Enabled = true, Exported = false)]
    public class ReminderActionReceiver : BroadcastReceiver
    {
        public const string PaidAction = "de.tomschorn.duofy.REMINDER_PAID";
        public const string LaterAction = "de.tomschorn.duofy.REMINDER_LATER";

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action is not { } action || !Guid.TryParse(intent.GetStringExtra(AndroidReminderNotifier.ReminderExtra), out var reminderId))
            {
                return;
            }
            var pending = GoAsync();
            Task.Run(async () =>
            {
                try
                {
                    await PushServices.RunAsync(async services =>
                    {
                        var actions = services.GetRequiredService<ReminderActionHandler>();
                        if (action == PaidAction)
                        {
                            await actions.PaidAsync(reminderId);
                        }
                        else if (action == LaterAction)
                        {
                            await actions.LaterAsync(reminderId);
                        }
                    });
                }
                catch (Exception exception)
                {
                    Android.Util.Log.Warn("Duofy", $"Aktion {action} der Erinnerung {reminderId} gescheitert: {exception.Message}");
                }
                finally
                {
                    pending?.Finish();
                }
            });
        }
    }
}
