using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duofy.ApiClient;

/// <summary>Was bei der Wahl eines Servers herauskam.</summary>
public enum ServerChoiceOutcome
{
    /// <summary>Der Server antwortet und ist jetzt gewählt.</summary>
    Taken,

    /// <summary>Die Adresse ist keine https-Adresse (oder gar keine); geprüft wurde nichts.</summary>
    NeedsHttps,

    /// <summary>Unter der Adresse antwortet kein Duofy-Server; der alte Server bleibt.</summary>
    NoServer,
}

/// <summary>Einstellungen der Serverwahl, die der Host festlegt.</summary>
public sealed class ServerSelectorOptions
{
    /// <summary>Ob auch http-Adressen erlaubt sind. Nur Debug-Builds der App setzen das.</summary>
    public bool AllowHttp { get; set; }

    /// <summary>Wie lange die Prüfung auf <c>/api/v1/health</c> warten darf.</summary>
    public TimeSpan CheckTimeout { get; set; } = TimeSpan.FromSeconds(10);
}

/// <summary>Prüft eine Serveradresse und nimmt sie, wenn dort ein Duofy-Server antwortet.</summary>
public interface IServerSelector
{
    /// <summary>
    /// Prüft die Eingabe: https (im Debug auch http), dann <c>GET /api/v1/health</c> mit
    /// Zeitgrenze. Nur bei 200 wird die Adresse genommen; dabei werden die Tokens des alten
    /// Servers gelöscht und die Anmeldewege des neuen geholt.
    /// </summary>
    Task<ServerChoiceOutcome> TryUseAsync(string? input, CancellationToken cancellationToken = default);

    /// <summary>Wählt den gehosteten Dienst. Die Adresse braucht keine Prüfung.</summary>
    Task UseHostedAsync(CancellationToken cancellationToken = default);
}

internal sealed class ServerSelector : IServerSelector
{
    private readonly IServerStore servers;
    private readonly TokenSession session;
    private readonly IHttpClientFactory clients;
    private readonly ServerSelectorOptions options;
    private readonly ILogger<ServerSelector> logger;

    public ServerSelector(
        IServerStore servers,
        TokenSession session,
        IHttpClientFactory clients,
        ServerSelectorOptions options,
        ILogger<ServerSelector> logger)
    {
        this.servers = servers;
        this.session = session;
        this.clients = clients;
        this.options = options;
        this.logger = logger;
    }

    public async Task<ServerChoiceOutcome> TryUseAsync(string? input, CancellationToken cancellationToken = default)
    {
        if (!TryParse(input, out var address))
        {
            return ServerChoiceOutcome.NeedsHttps;
        }

        if (!await AnswersAsync(address, cancellationToken))
        {
            return ServerChoiceOutcome.NoServer;
        }

        await TakeAsync(address, cancellationToken);
        return ServerChoiceOutcome.Taken;
    }

    public Task UseHostedAsync(CancellationToken cancellationToken = default) =>
        TakeAsync(servers.HostedAddress, cancellationToken);

    private bool TryParse(string? input, out Uri address)
    {
        address = null!;
        if (!Uri.TryCreate(input?.Trim(), UriKind.Absolute, out var parsed))
        {
            return false;
        }
        var allowed = parsed.Scheme == Uri.UriSchemeHttps || (options.AllowHttp && parsed.Scheme == Uri.UriSchemeHttp);
        if (!allowed || string.IsNullOrEmpty(parsed.Host))
        {
            return false;
        }
        address = ServerAddress.Normalize(parsed);
        return true;
    }

    private async Task<bool> AnswersAsync(Uri address, CancellationToken cancellationToken)
    {
        var health = new Uri(address, "api/v1/health");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.CheckTimeout);
        try
        {
            using var response = await clients.CreateClient(DuofyApiClientServiceCollectionExtensions.HttpClientName)
                .GetAsync(health, timeout.Token);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return true;
            }
            logger.LogInformation("Prüfung von {Address}: {StatusCode} statt 200.", address, (int)response.StatusCode);
            return false;
        }
        catch (HttpRequestException ex)
        {
            logger.LogInformation(ex, "Prüfung von {Address}: nicht erreichbar.", address);
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Prüfung von {Address}: keine Antwort in {Timeout}.", address, options.CheckTimeout);
            return false;
        }
    }

    private async Task TakeAsync(Uri address, CancellationToken cancellationToken)
    {
        // Tokens eines Servers bedeuten auf einem anderen nichts und sollen ihn nie erreichen:
        // Sie sind weg, bevor die neue Adresse benutzt wird.
        var changed = address != servers.CurrentAddress;
        if (changed)
        {
            await session.ClearAsync(cancellationToken);
        }

        if (address == servers.HostedAddress)
        {
            servers.Reset();
        }
        else
        {
            servers.Set(address);
        }

        if (changed)
        {
            servers.SignInMethods = await LoadSignInMethodsAsync(address, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<string>> LoadSignInMethodsAsync(Uri address, CancellationToken cancellationToken)
    {
        string[] fallback = [SignInMethodNames.Password];
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.CheckTimeout);
        try
        {
            var instance = await clients.CreateClient(DuofyApiClientServiceCollectionExtensions.HttpClientName)
                .GetFromJsonAsync<InstanceResponse>(new Uri(address, "api/v1/instance"), timeout.Token);
            return instance is { SignInMethods.Count: > 0 } ? instance.SignInMethods : fallback;
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // Ältere Server kennen den Endpunkt nicht; sie bieten nur E-Mail und Passwort an.
            logger.LogInformation(ex, "Anmeldewege von {Address} nicht lesbar; es gilt nur das Passwort.", address);
            return fallback;
        }
    }
}
