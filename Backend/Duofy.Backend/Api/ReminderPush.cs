using Duofy.Domain;

namespace Duofy.Api;

/// <summary>
/// Ob es Erinnerungen auf diesem Server gibt (Spec 84, R8): nur auf der gehosteten Version und nur, wenn
/// Duofy:Reminders:Enabled (Umgebungsvariable Duofy__Reminders__Enabled) gesetzt ist; ohne Einstellung aus.
/// </summary>
internal sealed class ReminderSwitch(IConfiguration configuration, HostedSettings hosted)
{
    public const string EnabledKey = "Duofy:Reminders:Enabled";

    public bool Enabled => hosted.Hosted && configuration.GetValue(EnabledKey, false);
}

/// <summary>
/// Was an den Anbieter geht (Spec 84, R4): nur die zufällige Kennung der Erinnerung und die Art des Signals, nie ein
/// Name, ein Betrag oder eine Kategorie. Die App holt den Text danach mit Anmeldung vom Server.
/// </summary>
internal sealed record PushPayload(Guid ReminderId, string Type)
{
    /// <summary>Ein Posten ist fällig: Die App holt den Text und zeigt die Benachrichtigung.</summary>
    public const string Due = "due";

    /// <summary>Die Abendzusammenfassung des Haushalts liegt bereit: Die App holt den Text und zeigt die Benachrichtigung.</summary>
    public const string Household = "household";

    /// <summary>Der Posten ist abgehakt: Die App nimmt die Benachrichtigung weg.</summary>
    public const string Withdraw = "withdraw";
}

/// <summary>Der Weg zu Firebase Cloud Messaging (Android); im Betrieb mit dem Dienstkonto, in den Tests ein Ersatz.</summary>
internal interface IFcmClient
{
    /// <summary>Schickt eine Datennachricht mit hoher Priorität an dieses Token.</summary>
    Task SendAsync(string token, PushPayload payload, CancellationToken cancellationToken);
}

/// <summary>Der Weg zu den Windows Push Notification Services; im Betrieb mit Client-Kennung und Geheimnis, in den Tests ein Ersatz.</summary>
internal interface IWnsClient
{
    /// <summary>Schickt eine Raw-Benachrichtigung an diesen Kanal.</summary>
    Task SendAsync(string channel, PushPayload payload, CancellationToken cancellationToken);
}

/// <summary>
/// Der Platzhalter, solange die Zugangsdaten der Anbieter fehlen: Er schickt nichts und sagt es im Log, damit eine
/// gehostete Version ohne Zugang nicht still ohne Erinnerungen bleibt.
/// </summary>
internal sealed class UnconfiguredPush(ILoggerFactory loggers) : IFcmClient, IWnsClient
{
    private readonly ILogger logger = loggers.CreateLogger(ReminderRunner.LogCategory);

    Task IFcmClient.SendAsync(string token, PushPayload payload, CancellationToken cancellationToken) => Skip("FCM", payload);

    Task IWnsClient.SendAsync(string channel, PushPayload payload, CancellationToken cancellationToken) => Skip("WNS", payload);

    private Task Skip(string provider, PushPayload payload)
    {
        logger.LogWarning("Signal {Type} ({ReminderId}) nicht verschickt: {Provider} ist nicht eingerichtet.", payload.Type, payload.ReminderId, provider);
        return Task.CompletedTask;
    }
}

/// <summary>Schickt ein Signal an alle Geräte einer Person, je Gerät über den Anbieter seines Systems.</summary>
internal sealed class PushSender(IFcmClient fcm, IWnsClient wns, ILoggerFactory loggers)
{
    private readonly ILogger logger = loggers.CreateLogger(ReminderRunner.LogCategory);

    /// <summary>
    /// Schickt das Signal an jedes Gerät. Scheitert eines, steht es mit Person und Gerät (nie mit dem Token) im Log und
    /// die anderen bekommen es trotzdem. Gibt zurück, an wie viele Geräte es ging.
    /// </summary>
    public async Task<int> SendAsync(IEnumerable<ReminderDevice> devices, PushPayload payload, CancellationToken cancellationToken)
    {
        var delivered = 0;
        foreach (var device in devices)
        {
            try
            {
                if (device.Platform == DevicePlatform.Android)
                {
                    await fcm.SendAsync(device.PushToken, payload, cancellationToken);
                }
                else
                {
                    await wns.SendAsync(device.PushToken, payload, cancellationToken);
                }
                delivered++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            // Jeder andere Fehler eines Anbieters: ein Gerät darf die anderen und den Durchlauf nie aufhalten.
            catch (Exception exception)
            {
                logger.LogError(exception, "Signal {Type} ({ReminderId}) an Gerät {InstallationId} von {UserId} ({Platform}) gescheitert.",
                    payload.Type, payload.ReminderId, device.InstallationId, device.UserId, device.Platform);
            }
        }
        return delivered;
    }
}
