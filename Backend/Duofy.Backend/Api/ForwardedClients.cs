using Microsoft.AspNetCore.HttpOverrides;

namespace Duofy.Api;

/// <summary>
/// Die echte Adresse der Person hinter dem Cloudflare Tunnel (Spec 79, R4). Im Container kommt jede Anfrage vom
/// Tunnel-Container; die Adresse der Person steht im Kopf CF-Connecting-IP, das Schema (https) in X-Forwarded-Proto.
///
/// Beides gilt nur, wenn die Anfrage aus einem vertrauten Netz kommt: Duofy:TrustedProxies (Umgebungsvariable
/// Duofy__TrustedProxies), Netze in CIDR-Schreibweise, durch Komma getrennt, zum Beispiel 172.30.10.2/32. Ohne
/// Einstellung vertraut die API niemandem, wie bisher; sonst könnte sich jeder mit einem gefälschten Kopf ein neues
/// Budget der Bremse holen.
/// </summary>
internal static class ForwardedClients
{
    public const string TrustedProxiesKey = "Duofy:TrustedProxies";

    // Unter diesem Schlüssel merkt sich die Anfrage, von wo die Verbindung wirklich kam.
    private const string ConnectionAddressItem = "Duofy.ConnectionAddress";

    /// <summary>Der Kopf, in den Cloudflare die Adresse der Person schreibt.</summary>
    public const string ClientAddressHeader = "CF-Connecting-IP";

    /// <summary>Liest die vertrauten Netze erst, wenn die Pipeline gebaut wird, damit Tests sie vorher setzen können.</summary>
    public static IServiceCollection AddForwardedClients(this IServiceCollection services)
    {
        services.AddOptions<ForwardedHeadersOptions>().Configure<IConfiguration, ILoggerFactory>((options, configuration, loggers) =>
        {
            // Die Vorgabe vertraut localhost; hier gilt nur, was eingestellt ist.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            options.ForwardedForHeaderName = ClientAddressHeader;
            options.ForwardLimit = 1;

            var logger = loggers.CreateLogger(typeof(ForwardedClients).FullName!);
            foreach (var entry in (configuration[TrustedProxiesKey] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (System.Net.IPNetwork.TryParse(entry, out var network))
                {
                    options.KnownIPNetworks.Add(network);
                }
                else
                {
                    logger.LogWarning("{Setting} enthält kein Netz in CIDR-Schreibweise: {Value}; es wird übergangen.", TrustedProxiesKey, entry);
                }
            }
            options.ForwardedHeaders = options.KnownIPNetworks.Count > 0
                ? ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                : ForwardedHeaders.None;
        });
        return services;
    }

    /// <summary>
    /// Ganz vorn in der Pipeline: merkt sich die Adresse der Verbindung und ersetzt dann die Absenderadresse durch die
    /// der Person, wenn die Verbindung aus einem vertrauten Netz kommt.
    /// </summary>
    public static IApplicationBuilder UseForwardedClients(this IApplicationBuilder app)
    {
        app.Use((context, next) =>
        {
            context.Items[ConnectionAddressItem] = context.Connection.RemoteIpAddress;
            return next(context);
        });
        return app.UseForwardedHeaders();
    }

    /// <summary>
    /// True, wenn die Verbindung von einer der <paramref name="addresses"/> kam (durch Komma getrennt). Gefragt wird
    /// die Adresse der Verbindung vor dem Ersetzen durch CF-Connecting-IP: Wer durch den Tunnel eine fremde Adresse
    /// angibt, bekommt deren Ausnahme von der Bremse nicht. Ein Eintrag, der keine Adresse ist, passt auf niemanden.
    /// </summary>
    public static bool ConnectionIsFrom(HttpContext context, string? addresses)
    {
        var connection = context.Items.TryGetValue(ConnectionAddressItem, out var item)
            ? item as System.Net.IPAddress
            : context.Connection.RemoteIpAddress;
        if (connection is null || string.IsNullOrWhiteSpace(addresses))
        {
            return false;
        }
        if (connection.IsIPv4MappedToIPv6)
        {
            connection = connection.MapToIPv4();
        }
        return addresses.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(entry => System.Net.IPAddress.TryParse(entry, out var address) && address.Equals(connection));
    }
}
