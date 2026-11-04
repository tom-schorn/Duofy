using Duofy.ApiClient;
using Duofy.Schemas;
using Microsoft.Extensions.Logging;

namespace Duofy.Push;

/// <summary>
/// Was die App mit einem stillen Signal tut (Spec 84, R1, R4, R7): bei "due" den Text vom Server holen und die
/// Benachrichtigung zeigen, bei "withdraw" sie wegnehmen. Scheitert das Holen (offline), erscheint der Text ohne Namen,
/// damit der Hinweis nie ausbleibt.
/// </summary>
public sealed class ReminderSignalHandler(IReminderApi reminders, IReminderNotifier notifier, ILogger<ReminderSignalHandler> logger)
{
    public async Task HandleAsync(PushSignal signal, CancellationToken cancellationToken = default)
    {
        if (signal.Type == PushSignal.Withdraw)
        {
            await notifier.CancelAsync(signal.ReminderId, cancellationToken);
            return;
        }

        var result = await reminders.GetAsync(signal.ReminderId, cancellationToken);
        if (result.Succeeded)
        {
            var reminder = result.Value!;
            await notifier.ShowAsync(new ReminderNotification(
                reminder.Id,
                ReminderTexts.Title(reminder.Label),
                ReminderTexts.Body,
                ReminderPaths.Month(reminder.Year, reminder.Month, reminder.PositionId, reminder.OwnerId),
                WithActions: true), cancellationToken);
            return;
        }

        if (result.ErrorCode == ClientErrorCodes.NotFound)
        {
            // Die Erinnerung gehört der Person nicht mehr (Freigabe entzogen) oder ist erledigt: nichts zeigen.
            logger.LogInformation("Erinnerung {ReminderId} gibt es für diese Person nicht (mehr); keine Benachrichtigung.", signal.ReminderId);
            return;
        }

        if (result.ErrorCode == ClientErrorCodes.NotSignedIn)
        {
            // Niemand angemeldet (abgemeldet oder abgelaufen): Ein spätes Signal zeigt nichts, auch keinen Hinweis ohne Namen.
            logger.LogInformation("Signal für Erinnerung {ReminderId} ohne Anmeldung; keine Benachrichtigung.", signal.ReminderId);
            return;
        }

        logger.LogWarning("Text der Erinnerung {ReminderId} nicht geholt ({ErrorCode}); zeige den Hinweis ohne Namen.", signal.ReminderId, result.ErrorCode);
        await notifier.ShowAsync(new ReminderNotification(
            signal.ReminderId, ReminderTexts.FallbackTitle, ReminderTexts.FallbackBody, ReminderPaths.CurrentMonth, WithActions: false), cancellationToken);
    }
}

/// <summary>Das Ergebnis eines Knopfs der Benachrichtigung.</summary>
public enum ReminderActionOutcome
{
    /// <summary>Erledigt; die Benachrichtigung ist weg.</summary>
    Done,

    /// <summary>Abgelehnt oder gescheitert; eine kurze Meldung steht an ihrer Stelle, ein Tipp öffnet die App.</summary>
    Refused,
}

/// <summary>Der Weg, einen Posten abzuhaken; im Betrieb der Plan-Client, in den Tests ein Ersatz.</summary>
public interface IPositionTicker
{
    /// <summary>Hakt den Posten des Besitzers mit diesem Betrag ab.</summary>
    Task<ApiResult> TickAsync(int year, int month, Guid positionId, Guid ownerId, decimal amount, CancellationToken cancellationToken = default);
}

/// <summary>Hakt über <see cref="IPlanApi"/> ab, also über denselben Weg und dieselben Rechte wie die App.</summary>
public sealed class PlanPositionTicker(IPlanApi plans) : IPositionTicker
{
    public async Task<ApiResult> TickAsync(int year, int month, Guid positionId, Guid ownerId, decimal amount, CancellationToken cancellationToken = default)
    {
        var result = await plans.TickAsync(year, month, positionId, new TickPositionRequest(Amount: amount), ownerId, cancellationToken);
        return result.Succeeded ? ApiResult.Success() : ApiResult.Failure(result.ErrorCode!);
    }
}

/// <summary>"Bezahlt" und "Später" in der Benachrichtigung (Spec 84, R5).</summary>
public sealed class ReminderActionHandler(
    IReminderApi reminders, IPositionTicker ticker, IReminderNotifier notifier, ILogger<ReminderActionHandler> logger)
{
    /// <summary>
    /// Hakt den Posten mit dem geplanten Betrag ab, über denselben Weg wie die App; die Rechte prüft der Server.
    /// Wird das Abhaken abgelehnt, ersetzt eine kurze Meldung die Benachrichtigung.
    /// </summary>
    public async Task<ReminderActionOutcome> PaidAsync(Guid reminderId, CancellationToken cancellationToken = default)
    {
        var fetched = await reminders.GetAsync(reminderId, cancellationToken);
        if (!fetched.Succeeded)
        {
            return await RefuseAsync(reminderId, fetched.ErrorCode, ReminderPaths.CurrentMonth, cancellationToken);
        }
        var reminder = fetched.Value!;
        var path = ReminderPaths.Month(reminder.Year, reminder.Month, reminder.PositionId, reminder.OwnerId);
        if (reminder.AmountPlanned is not { } amount)
        {
            // Ohne Recht auf den Plan kennt der Server den Betrag nicht preis; dann gibt es auch kein Abhaken von hier.
            return await RefuseAsync(reminderId, ErrorCodes.NoEditGranted, path, cancellationToken);
        }

        var tick = await ticker.TickAsync(
            reminder.Year, reminder.Month, reminder.PositionId, reminder.OwnerId, amount, cancellationToken);
        if (!tick.Succeeded)
        {
            return await RefuseAsync(reminderId, tick.ErrorCode, path, cancellationToken);
        }

        await notifier.CancelAsync(reminderId, cancellationToken);
        return ReminderActionOutcome.Done;
    }

    /// <summary>Verschiebt die Erinnerung auf morgen und nimmt die Benachrichtigung weg.</summary>
    public async Task<ReminderActionOutcome> LaterAsync(Guid reminderId, CancellationToken cancellationToken = default)
    {
        var result = await reminders.LaterAsync(reminderId, cancellationToken);
        if (!result.Succeeded)
        {
            return await RefuseAsync(reminderId, result.ErrorCode, ReminderPaths.CurrentMonth, cancellationToken);
        }
        await notifier.CancelAsync(reminderId, cancellationToken);
        return ReminderActionOutcome.Done;
    }

    private async Task<ReminderActionOutcome> RefuseAsync(Guid reminderId, string? errorCode, string path, CancellationToken cancellationToken)
    {
        logger.LogWarning("Aktion der Erinnerung {ReminderId} abgelehnt oder gescheitert ({ErrorCode}).", reminderId, errorCode);
        await notifier.ReplaceWithMessageAsync(reminderId, ReminderTexts.RefusedMessage(errorCode), path, cancellationToken);
        return ReminderActionOutcome.Refused;
    }
}
