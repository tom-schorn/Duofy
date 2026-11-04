using Android.App;
using Android.Content;
using AndroidX.Core.App;
using Duofy.Push;

namespace Duofy.Services.Push
{
    /// <summary>
    /// Die Benachrichtigungen der Erinnerungen auf Android (Spec 84). Die Kennung ist die der Erinnerung als Marke
    /// (Tag), damit dieselbe Erinnerung nie zweimal erscheint und "withdraw" genau diese findet. "Bezahlt" und "Später"
    /// sind Knöpfe, die einen Rundruf an <see cref="ReminderActionReceiver"/> schicken; ein Tipp öffnet die App an der Adresse.
    /// </summary>
    public class AndroidReminderNotifier : IReminderNotifier
    {
        public const string ChannelId = "reminders";
        public const string PathExtra = "duofy.path";
        public const string ReminderExtra = "duofy.reminder";

        private static Context Context => Android.App.Application.Context;

        public Task ShowAsync(ReminderNotification notification, CancellationToken cancellationToken = default)
        {
            var builder = Start(notification.ReminderId, notification.Title, notification.Body, notification.Path);
            if (notification.WithActions)
            {
                builder.AddAction(0, ReminderTexts.PaidAction, ActionIntent(ReminderActionReceiver.PaidAction, notification.ReminderId));
                builder.AddAction(0, ReminderTexts.LaterAction, ActionIntent(ReminderActionReceiver.LaterAction, notification.ReminderId));
            }
            Notify(notification.ReminderId, builder);
            return Task.CompletedTask;
        }

        public Task ReplaceWithMessageAsync(Guid reminderId, string message, string path, CancellationToken cancellationToken = default)
        {
            // Ersetzt die Benachrichtigung gleicher Marke: dieselbe Erinnerung, jetzt ohne Knöpfe und mit dem Grund.
            Notify(reminderId, Start(reminderId, message, "", path));
            return Task.CompletedTask;
        }

        public Task CancelAsync(Guid reminderId, CancellationToken cancellationToken = default)
        {
            NotificationManagerCompat.From(Context)!.Cancel(reminderId.ToString(), 0);
            return Task.CompletedTask;
        }

        private static NotificationCompat.Builder Start(Guid reminderId, string title, string body, string path)
        {
            EnsureChannel();
            var open = new Intent(Context, typeof(MainActivity));
            open.SetFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);
            open.PutExtra(PathExtra, path);
            var tap = PendingIntent.GetActivity(Context, RequestCode(reminderId, "open"), open, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            return new NotificationCompat.Builder(Context, ChannelId)
                .SetSmallIcon(Context.ApplicationInfo!.Icon)
                .SetContentTitle(title)
                .SetContentText(body)
                .SetStyle(new NotificationCompat.BigTextStyle().BigText(body))
                .SetPriority(NotificationCompat.PriorityHigh)
                .SetCategory(NotificationCompat.CategoryReminder)
                .SetVisibility(NotificationCompat.VisibilityPrivate)
                .SetAutoCancel(true)
                .SetContentIntent(tap);
        }

        private static PendingIntent ActionIntent(string action, Guid reminderId)
        {
            var intent = new Intent(Context, typeof(ReminderActionReceiver)).SetAction(action);
            intent.PutExtra(ReminderExtra, reminderId.ToString());
            return PendingIntent.GetBroadcast(Context, RequestCode(reminderId, action), intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
        }

        private static void Notify(Guid reminderId, NotificationCompat.Builder builder)
        {
            var manager = NotificationManagerCompat.From(Context)!;
            if (!manager.AreNotificationsEnabled())
            {
                // Der Benutzer hat Benachrichtigungen verboten; die Erlaubnis fragt die Hauptseite beim Start.
                return;
            }
            manager.Notify(reminderId.ToString(), 0, builder.Build()!);
        }

        private static int RequestCode(Guid reminderId, string purpose) => HashCode.Combine(reminderId, purpose);

        private static void EnsureChannel()
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                return;
            }
            var manager = (NotificationManager)Context.GetSystemService(Context.NotificationService)!;
            if (manager.GetNotificationChannel(ChannelId) is null)
            {
                var channel = new NotificationChannel(ChannelId, ReminderTexts.ChannelName, NotificationImportance.High)
                {
                    Description = ReminderTexts.ChannelDescription,
                };
                manager.CreateNotificationChannel(channel);
            }
        }
    }
}
