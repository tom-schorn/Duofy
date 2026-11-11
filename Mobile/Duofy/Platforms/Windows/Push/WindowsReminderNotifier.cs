using Duofy.Push;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace Duofy.Services.Push
{
    /// <summary>
    /// Die Benachrichtigungen der Erinnerungen auf Windows (Spec 84): App-Benachrichtigungen des Windows App SDK mit den
    /// Knöpfen "Bezahlt" und "Später". Die Erinnerung ist die Marke (Tag) mit fester Gruppe; dieselbe Marke ersetzt die
    /// Benachrichtigung, und "withdraw" findet sie über die Marke. Die Arguments tragen nur Kennung, Aktion und Adresse.
    /// </summary>
    public class WindowsReminderNotifier : IReminderNotifier
    {
        public const string Group = "reminders";
        public const string ActionKey = "action";
        public const string ReminderKey = "reminder";
        public const string PathKey = "path";
        public const string PaidValue = "paid";
        public const string LaterValue = "later";

        public Task ShowAsync(ReminderNotification notification, CancellationToken cancellationToken = default)
        {
            var builder = Start(notification.ReminderId, notification.Title, notification.Body, notification.Path);
            if (notification.WithActions)
            {
                builder.AddButton(new AppNotificationButton(ReminderTexts.PaidAction)
                    .AddArgument(ActionKey, PaidValue).AddArgument(ReminderKey, notification.ReminderId.ToString()));
                if (notification.WithLater)
                {
                    builder.AddButton(new AppNotificationButton(ReminderTexts.LaterAction)
                        .AddArgument(ActionKey, LaterValue).AddArgument(ReminderKey, notification.ReminderId.ToString()));
                }
            }
            Show(notification.ReminderId, builder);
            return Task.CompletedTask;
        }

        public Task ReplaceWithMessageAsync(Guid reminderId, string message, string path, CancellationToken cancellationToken = default)
        {
            Show(reminderId, Start(reminderId, message, "", path));
            return Task.CompletedTask;
        }

        public async Task CancelAsync(Guid reminderId, CancellationToken cancellationToken = default)
        {
            await AppNotificationManager.Default.RemoveByTagAndGroupAsync(reminderId.ToString(), Group);
        }

        private static AppNotificationBuilder Start(Guid reminderId, string title, string body, string path)
        {
            var builder = new AppNotificationBuilder()
                .AddArgument(ReminderKey, reminderId.ToString())
                .AddArgument(PathKey, path)
                .AddText(title);
            if (!string.IsNullOrEmpty(body))
            {
                builder.AddText(body);
            }
            return builder;
        }

        private static void Show(Guid reminderId, AppNotificationBuilder builder)
        {
            var notification = builder.BuildNotification();
            notification.Tag = reminderId.ToString();
            notification.Group = Group;
            AppNotificationManager.Default.Show(notification);
        }
    }
}
