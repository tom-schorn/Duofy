using Duofy.ApiClient;
using Microsoft.Extensions.Logging;

namespace Duofy.Push;

/// <summary>Der Push-Token dieses Geräts bei seinem Anbieter: auf Android das FCM-Token, auf Windows die WNS-Kanal-Adresse.</summary>
public interface IPushTokenSource
{
    /// <summary>Die Plattform, so wie die API sie nennt (<see cref="Duofy.Schemas.DevicePlatforms"/>).</summary>
    string Platform { get; }

    /// <summary>
    /// Das Token, oder null, wenn es keines gibt (die Firebase-Datei fehlt, der Benutzer hat Benachrichtigungen
    /// verboten, kein Netz). Ohne Token wird das Gerät nicht angemeldet; die App läuft normal weiter.
    /// </summary>
    Task<string?> GetTokenAsync(CancellationToken cancellationToken = default);
}

/// <summary>Die Kennung dieser Installation; sie bleibt beim Wechsel des Tokens gleich.</summary>
public interface IInstallationIdStore
{
    /// <summary>Die Kennung; sie wird beim ersten Mal angelegt und bleibt, bis die App entfernt wird.</summary>
    string GetOrCreate();
}

/// <summary>
/// Meldet dieses Gerät beim Server an und ab (Spec 84): nach der Anmeldung und bei jedem Start, beim Abmelden wieder ab.
/// Nichts davon darf die App behindern: Ein Fehler steht im Log, die Anmeldung und das Abmelden gehen weiter.
/// </summary>
public sealed class DeviceRegistrar(
    IReminderApi reminders, IPushTokenSource tokens, IInstallationIdStore installation, ILogger<DeviceRegistrar> logger)
{
    /// <summary>Meldet das Gerät mit seinem aktuellen Token an. Gibt true zurück, wenn der Server es angenommen hat.</summary>
    public async Task<bool> RegisterAsync(CancellationToken cancellationToken = default)
    {
        string? token;
        try
        {
            token = await tokens.GetTokenAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or TimeoutException)
        {
            logger.LogWarning(exception, "Push-Token ({Platform}) nicht geholt; das Gerät wird nicht angemeldet.", tokens.Platform);
            return false;
        }
        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogInformation("Kein Push-Token ({Platform}); das Gerät wird nicht angemeldet.", tokens.Platform);
            return false;
        }

        var result = await reminders.RegisterDeviceAsync(installation.GetOrCreate(), tokens.Platform, token, cancellationToken);
        if (result.Succeeded)
        {
            logger.LogInformation("Gerät ({Platform}) für Erinnerungen angemeldet.", tokens.Platform);
            return true;
        }
        // 404 heißt: Auf diesem Server gibt es keine Erinnerungen (selbst betrieben); das ist kein Fehler.
        if (result.ErrorCode == ClientErrorCodes.NotFound)
        {
            logger.LogInformation("Dieser Server hat keine Erinnerungen; das Gerät wird nicht angemeldet.");
        }
        else
        {
            logger.LogWarning("Gerät ({Platform}) nicht angemeldet: {ErrorCode}.", tokens.Platform, result.ErrorCode);
        }
        return false;
    }

    /// <summary>Meldet das Gerät ab; muss vor dem Löschen der Anmeldung geschehen, denn der Server prüft sie.</summary>
    public async Task<bool> RemoveAsync(CancellationToken cancellationToken = default)
    {
        var result = await reminders.RemoveDeviceAsync(installation.GetOrCreate(), cancellationToken);
        if (!result.Succeeded && result.ErrorCode != ClientErrorCodes.NotFound)
        {
            logger.LogWarning("Gerät nicht abgemeldet: {ErrorCode}.", result.ErrorCode);
            return false;
        }
        return result.Succeeded;
    }
}
