using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace Duofy.Api;

/// <summary>
/// Die Namen der Einstellungen der Anbieter (Spec 84), alle aus Umgebungsvariablen oder Dateien außerhalb des
/// Repositorys (zum Beispiel Duofy__Reminders__Wns__ClientSecret). Ohne Werte läuft der Server weiter und schickt nichts.
/// </summary>
internal static class PushSettings
{
    public const string FcmServiceAccountFile = "Duofy:Reminders:Fcm:ServiceAccountFile";
    public const string WnsClientId = "Duofy:Reminders:Wns:ClientId";
    public const string WnsClientSecret = "Duofy:Reminders:Wns:ClientSecret";
}

/// <summary>
/// Firebase Cloud Messaging für Android (Spec 84, R1): eine Datennachricht mit hoher Priorität, die nur Kennung und Art
/// trägt. Kein Notification-Block, damit Android nichts selbst zeigt und die App den Text vom Server holt. Das Dienstkonto
/// wird erst beim ersten Senden gelesen, damit ein Server ohne Erinnerungen nie danach fragt.
/// </summary>
internal sealed class FcmClient(IConfiguration configuration) : IFcmClient
{
    // Nur ein Erfolg bleibt im Speicher: ein Fehler beim Einrichten (Datei fehlt noch) wird beim nächsten Senden neu versucht.
    private readonly object setupLock = new();
    private FirebaseMessaging? messaging;

    public async Task SendAsync(string token, PushPayload payload, CancellationToken cancellationToken)
    {
        try
        {
            await GetMessaging().SendAsync(BuildMessage(token, payload), cancellationToken);
        }
        catch (FirebaseMessagingException exception)
        {
            // Der Fehlercode des Anbieters genügt; das Token steht weder in der Meldung noch im Log.
            throw new InvalidOperationException($"FCM hat das Signal abgelehnt ({exception.MessagingErrorCode?.ToString() ?? exception.ErrorCode.ToString()}).", exception);
        }
    }

    private FirebaseMessaging GetMessaging()
    {
        lock (setupLock)
        {
            return messaging ??= Create(configuration);
        }
    }

    internal static Message BuildMessage(string token, PushPayload payload) => new()
    {
        Token = token,
        Data = new Dictionary<string, string> { ["reminderId"] = payload.ReminderId.ToString(), ["type"] = payload.Type },
        Android = new AndroidConfig { Priority = Priority.High },
    };

    private static FirebaseMessaging Create(IConfiguration configuration)
    {
        var path = configuration[PushSettings.FcmServiceAccountFile];
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new InvalidOperationException($"FCM ist nicht eingerichtet: {PushSettings.FcmServiceAccountFile} fehlt oder zeigt auf keine Datei.");
        }
        var app = FirebaseApp.GetInstance("duofy-reminders")
            ?? FirebaseApp.Create(new AppOptions { Credential = GoogleCredential.FromFile(path) }, "duofy-reminders");
        return FirebaseMessaging.GetMessaging(app);
    }
}

/// <summary>
/// Windows Push Notification Services für die Windows-App (Spec 84, R1) über HttpClient: ein Zugriffstoken mit
/// Client-Kennung (die Paket-SID) und Geheimnis, dann eine Raw-Benachrichtigung an die Kanal-Adresse. Das Zugriffstoken
/// wird bis kurz vor dem Ablauf wiederverwendet. Die Kanal-Adresse kommt vom Gerät und wird vor dem Senden geprüft.
/// </summary>
internal sealed class WnsClient(HttpClient http, IConfiguration configuration, TimeProvider time, ILogger<WnsClient> logger) : IWnsClient
{
    private const string TokenAddress = "https://login.live.com/accesstoken.srf";

    private readonly SemaphoreSlim tokenLock = new(1, 1);
    private string? accessToken;
    private DateTimeOffset accessTokenExpires;

    public async Task SendAsync(string channel, PushPayload payload, CancellationToken cancellationToken)
    {
        if (!IsWnsChannel(channel))
        {
            // Nie das Zugriffstoken an einen Rechner schicken, den ein Gerät uns genannt hat und der nicht zu WNS gehört.
            throw new InvalidOperationException("Der WNS-Kanal ist keine Adresse von notify.windows.com.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, channel)
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes(BuildContent(payload))),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(cancellationToken));
        request.Headers.Add("X-WNS-Type", "wns/raw");

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // Nur der Statuscode: Die Kanal-Adresse ist ein Geheimnis des Geräts und steht in keiner Meldung.
            throw new HttpRequestException($"WNS hat das Signal abgelehnt ({(int)response.StatusCode}).", null, response.StatusCode);
        }
    }

    internal static string BuildContent(PushPayload payload) =>
        JsonSerializer.Serialize(new Dictionary<string, string> { ["reminderId"] = payload.ReminderId.ToString(), ["type"] = payload.Type });

    /// <summary>True für eine https-Adresse auf notify.windows.com (oder einer Unterdomäne), ohne Zugangsdaten in der Adresse.</summary>
    internal static bool IsWnsChannel(string? channel) =>
        Uri.TryCreate(channel, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.IsNullOrEmpty(uri.UserInfo)
        && uri.Host.EndsWith(".notify.windows.com", StringComparison.OrdinalIgnoreCase);

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        await tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (accessToken is not null && time.GetUtcNow() < accessTokenExpires)
            {
                return accessToken;
            }
            var clientId = configuration[PushSettings.WnsClientId];
            var clientSecret = configuration[PushSettings.WnsClientSecret];
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new InvalidOperationException($"WNS ist nicht eingerichtet: {PushSettings.WnsClientId} und {PushSettings.WnsClientSecret} fehlen.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, TokenAddress)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["scope"] = "notify.windows.com",
                }),
            };
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"WNS hat kein Zugriffstoken gegeben ({(int)response.StatusCode}).", null, response.StatusCode);
            }
            JsonDocument json;
            try
            {
                json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException("WNS hat eine Antwort ohne lesbares Zugriffstoken gegeben.", exception);
            }
            using var _ = json;
            accessToken = json.RootElement.TryGetProperty("access_token", out var tokenElement) ? tokenElement.GetString() : null;
            if (string.IsNullOrEmpty(accessToken))
            {
                throw new InvalidOperationException("WNS hat ein leeres Zugriffstoken gegeben.");
            }
            var seconds = json.RootElement.TryGetProperty("expires_in", out var expires) && expires.TryGetInt32(out var value) ? value : 3600;
            // Fünf Minuten früher erneuern, damit kein Signal mit einem Token am Rand des Ablaufs geht.
            accessTokenExpires = time.GetUtcNow().AddSeconds(Math.Max(60, seconds - 300));
            logger.LogInformation("WNS-Zugriffstoken erneuert (gültig {Seconds} Sekunden).", seconds);
            return accessToken;
        }
        finally
        {
            tokenLock.Release();
        }
    }
}
