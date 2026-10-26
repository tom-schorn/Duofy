using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Duofy.Schemas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duofy.ApiClient;

/// <summary>
/// Die Verbindung zur API für einen Scope: in der App für die ganze Laufzeit, im
/// Web für eine Sitzung des Browsers.
///
/// Der <see cref="AuthTokenHandler"/> braucht die Token-Ablage desselben Scopes.
/// Hängt man ihn wie üblich über IHttpClientFactory ein, bekommt er einen eigenen
/// Scope und damit die falsche Ablage. Deshalb wird er hier von Hand vor den
/// Handler der Factory gesetzt; die Factory verwaltet weiter die Verbindungen.
/// </summary>
internal sealed class ApiConnection : IDisposable
{
    private readonly HttpClient http;
    private readonly ILogger<ApiConnection> logger;
    private readonly IServerStore servers;

    public ApiConnection(
        IHttpMessageHandlerFactory handlerFactory,
        TokenSession session,
        IServerStore servers,
        ILoggerFactory loggerFactory)
    {
        this.servers = servers;
        logger = loggerFactory.CreateLogger<ApiConnection>();
        var handler = new AuthTokenHandler(session, loggerFactory.CreateLogger<AuthTokenHandler>())
        {
            InnerHandler = handlerFactory.CreateHandler(DuofyApiClientServiceCollectionExtensions.HttpClientName),
        };
        // Ohne feste Adresse: Jede Anfrage bekommt die aktuelle Adresse des Servers mit.
        http = new HttpClient(handler);
    }

    /// <summary>Schickt eine Anfrage, deren Antwort keinen Inhalt hat.</summary>
    public async Task<ApiResult> SendAsync(
        HttpMethod method, string path, object? body, bool anonymous, CancellationToken cancellationToken)
    {
        var (response, error) = await SendCoreAsync(method, path, body, anonymous, cancellationToken);
        response?.Dispose();
        return error is null ? ApiResult.Success() : ApiResult.Failure(error);
    }

    /// <summary>Schickt eine Anfrage und liest die Antwort als <typeparamref name="T"/>.</summary>
    public async Task<ApiResult<T>> SendAsync<T>(
        HttpMethod method, string path, object? body, bool anonymous, CancellationToken cancellationToken)
    {
        var (response, error) = await SendCoreAsync(method, path, body, anonymous, cancellationToken);
        if (error is not null)
        {
            return ApiResult<T>.Failure(error);
        }

        using (response)
        {
            try
            {
                var value = await response!.Content.ReadFromJsonAsync<T>(cancellationToken);
                if (value is not null)
                {
                    return ApiResult<T>.Success(value);
                }
                logger.LogError("Leere Antwort der API auf {Method} {Path}.", method, path);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Unlesbare Antwort der API auf {Method} {Path}.", method, path);
            }
            return ApiResult<T>.Failure(ClientErrorCodes.Unexpected);
        }
    }

    /// <summary>
    /// Wie <see cref="SendAsync{T}"/>, nur darf die Antwort leer sein (202 oder 204 ohne Inhalt): Dann ist der Wert
    /// null.
    /// </summary>
    public async Task<ApiResult<T?>> SendOptionalAsync<T>(
        HttpMethod method, string path, object? body, bool anonymous, CancellationToken cancellationToken) where T : class
    {
        var (response, error) = await SendCoreAsync(method, path, body, anonymous, cancellationToken);
        if (error is not null)
        {
            return ApiResult<T?>.Failure(error);
        }
        using (response)
        {
            if (response!.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.NoContent
                && response.Content.Headers.ContentLength is null or 0)
            {
                return ApiResult<T?>.Success(null);
            }
            try
            {
                if (await response.Content.ReadFromJsonAsync<T>(cancellationToken) is { } value)
                {
                    return ApiResult<T?>.Success(value);
                }
                logger.LogError("Leere Antwort der API auf {Method} {Path}.", method, path);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Unlesbare Antwort der API auf {Method} {Path}.", method, path);
            }
            return ApiResult<T?>.Failure(ClientErrorCodes.Unexpected);
        }
    }

    private async Task<(HttpResponseMessage? Response, string? Error)> SendCoreAsync(
        HttpMethod method, string path, object? body, bool anonymous, CancellationToken cancellationToken)
    {
        // Die Adresse wird einmal gelesen und mit der Anfrage mitgegeben, damit auch der Tausch
        // des Refresh-Tokens zu demselben Server geht, falls sich die Wahl währenddessen ändert.
        var server = servers.CurrentAddress;
        using var request = new HttpRequestMessage(method, new Uri(server, path));
        request.Options.Set(AuthTokenHandler.Server, server);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }
        if (anonymous)
        {
            request.Options.Set(AuthTokenHandler.Anonymous, true);
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Die API ist nicht erreichbar ({Method} {Path}).", method, path);
            return (null, ClientErrorCodes.ServerUnreachable);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Ohne Abbruch durch den Aufrufer heißt das: Zeitüberschreitung.
            logger.LogWarning(ex, "Die API hat nicht rechtzeitig geantwortet ({Method} {Path}).", method, path);
            return (null, ClientErrorCodes.ServerUnreachable);
        }

        if (response.IsSuccessStatusCode)
        {
            return (response, null);
        }

        using (response)
        {
            var error = await ReadErrorAsync(response, method, path, cancellationToken);
            return (null, error);
        }
    }

    private async Task<string> ReadErrorAsync(
        HttpResponseMessage response, HttpMethod method, string path, CancellationToken cancellationToken)
    {
        var code = await ReadCodeAsync(response, cancellationToken);
        switch (response.StatusCode)
        {
            case HttpStatusCode.TooManyRequests:
                return ClientErrorCodes.TooManyRequests;
            case HttpStatusCode.Unauthorized:
                return code ?? ClientErrorCodes.NotSignedIn;
            case HttpStatusCode.NotFound:
                return ClientErrorCodes.NotFound;
            // 403 mit Code: Ein Schritt fehlt, zum Beispiel no_edit_granted (Spec 68).
            case HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.Forbidden when code is not null:
                return code;
            default:
                logger.LogWarning("Unerwartete Antwort {StatusCode} der API auf {Method} {Path}.",
                    (int)response.StatusCode, method, path);
                return ClientErrorCodes.Unexpected;
        }
    }

    /// <summary>Liest den Fehlercode aus einer Antwort wie <c>{ "code": "email_taken" }</c>, falls es einen gibt.</summary>
    private async Task<string?> ReadCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentType?.MediaType is not { } mediaType || !mediaType.Contains("json"))
        {
            return null;
        }
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken);
            return string.IsNullOrEmpty(error?.Code) ? null : error.Code;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Fehlerantwort {StatusCode} der API ohne lesbaren Code.", (int)response.StatusCode);
            return null;
        }
    }

    public void Dispose() => http.Dispose();
}
