using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Duofy.Schemas;
using Microsoft.Extensions.Logging;

namespace Duofy.ApiClient;

/// <summary>
/// Steht zwischen jedem Client und der API. Er hängt das Access-Token an jede
/// Anfrage. Antwortet die API mit 401, weil das Access-Token abgelaufen ist, tauscht
/// er das Refresh-Token genau einmal gegen ein neues Paar und schickt die Anfrage
/// noch einmal. Lehnt die API auch das Refresh-Token ab, löscht er die Tokens: Die
/// Person ist dann abgemeldet.
/// </summary>
internal sealed class AuthTokenHandler : DelegatingHandler
{
    /// <summary>
    /// Markiert Anfragen, die ohne Token laufen (Registrierung, Anmeldung). Bei
    /// ihnen heißt 401 "falsches Passwort" und nicht "Token abgelaufen".
    /// </summary>
    public static readonly HttpRequestOptionsKey<bool> Anonymous = new("Duofy.Anonymous");

    /// <summary>Die Adresse des Servers, an den die Anfrage geht; der Refresh geht an denselben.</summary>
    public static readonly HttpRequestOptionsKey<Uri> Server = new("Duofy.Server");

    // Gilt, solange der Handler die Anmeldung löscht, weil das Refresh-Token abgelehnt wurde. Ein Aufräumen, das dabei selbst
    // eine Anfrage schickt (das Gerät abmelden), bekäme wieder 401 und würde wieder einen Refresh anstoßen: ein Kreis.
    // Ein AsyncLocal, weil die Anfrage dabei über einen anderen Handler und Scope laufen kann.
    private static readonly AsyncLocal<bool> clearingExpiredSession = new();

    private readonly TokenSession session;
    private readonly ILogger<AuthTokenHandler> logger;

    // Schicken mehrere Anfragen gleichzeitig ein abgelaufenes Token, soll nur die
    // erste das Refresh-Token tauschen; die anderen nehmen dann das neue Paar.
    private readonly SemaphoreSlim refreshGate = new(1, 1);

    public AuthTokenHandler(TokenSession session, ILogger<AuthTokenHandler> logger)
    {
        this.session = session;
        this.logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Options.TryGetValue(Anonymous, out var anonymous) && anonymous)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var tokens = await session.GetAsync(cancellationToken);
        if (tokens is null)
        {
            // Ohne Tokens ist die Person abgemeldet (etwa weil der Eintrag des Web-Servers
            // abgelaufen ist); die laufende Verbindung soll das erfahren und zur Anmeldung gehen.
            session.NoteTokensMissing();
            return await base.SendAsync(request, cancellationToken);
        }

        // Eine schon gesendete Anfrage lässt sich nicht noch einmal senden. Für die
        // Wiederholung wird deshalb der Inhalt vorher gelesen und später kopiert.
        var body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        if (clearingExpiredSession.Value)
        {
            // Die Anmeldung ist schon als abgelaufen erkannt; kein zweiter Refresh.
            return response;
        }

        var renewed = await RefreshAsync(tokens, RefreshAddressOf(request), cancellationToken);
        if (renewed is null)
        {
            return response;
        }

        response.Dispose();
        var retry = Copy(request, body);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", renewed.AccessToken);
        return await base.SendAsync(retry, cancellationToken);
    }

    /// <summary>
    /// Tauscht das Refresh-Token gegen ein neues Paar. Liefert null, wenn das nicht
    /// geklappt hat. Ist die API nicht erreichbar, fliegt die Ausnahme weiter und
    /// die Tokens bleiben liegen, damit es beim nächsten Mal wieder klappen kann.
    /// </summary>
    private async Task<AuthTokens?> RefreshAsync(AuthTokens used, Uri refreshAddress, CancellationToken cancellationToken)
    {
        await refreshGate.WaitAsync(cancellationToken);
        try
        {
            var current = await session.GetAsync(cancellationToken);
            if (current is null)
            {
                return null;
            }
            if (current.AccessToken != used.AccessToken)
            {
                // Eine andere Anfrage hat in der Zwischenzeit schon getauscht.
                return current;
            }

            using var refresh = new HttpRequestMessage(HttpMethod.Post, refreshAddress)
            {
                Content = JsonContent.Create(new RefreshRequest(current.RefreshToken)),
            };
            using var response = await base.SendAsync(refresh, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var pair = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                    ?? throw new InvalidOperationException("Die API hat beim Refresh eine leere Antwort geschickt.");
                var renewed = new AuthTokens(pair.AccessToken, pair.RefreshToken);
                await session.SaveAsync(renewed, cancellationToken);
                return renewed;
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
            {
                logger.LogInformation("Refresh-Token abgelehnt ({StatusCode}); die Anmeldung wird gelöscht.", (int)response.StatusCode);
                clearingExpiredSession.Value = true;
                try
                {
                    await session.ClearAsync(cancellationToken);
                }
                finally
                {
                    clearingExpiredSession.Value = false;
                }
                return null;
            }

            // Ein Fehler der API (etwa 500) sagt nichts über die Anmeldung; die Tokens bleiben.
            logger.LogWarning("Refresh fehlgeschlagen mit {StatusCode}; die Tokens bleiben erhalten.", (int)response.StatusCode);
            return null;
        }
        finally
        {
            refreshGate.Release();
        }
    }

    // Der Server steht in den Optionen der Anfrage; fehlt er, ist es der Rechner der Anfrage selbst.
    private static Uri RefreshAddressOf(HttpRequestMessage request) =>
        new(request.Options.TryGetValue(Server, out var server) ? server : new Uri(request.RequestUri!.GetLeftPart(UriPartial.Authority) + "/"),
            "api/v1/auth/refresh");

    private static HttpRequestMessage Copy(HttpRequestMessage original, byte[]? body)
    {
        var copy = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Version = original.Version,
            VersionPolicy = original.VersionPolicy,
        };
        foreach (var header in original.Headers)
        {
            copy.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        foreach (var option in original.Options)
        {
            copy.Options.TryAdd(option.Key, option.Value);
        }
        if (body is not null)
        {
            copy.Content = new ByteArrayContent(body);
            foreach (var header in original.Content!.Headers)
            {
                // Die Länge berechnet der neue Inhalt selbst.
                if (header.Key != "Content-Length")
                {
                    copy.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
        }
        return copy;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            refreshGate.Dispose();
        }
        base.Dispose(disposing);
    }
}
