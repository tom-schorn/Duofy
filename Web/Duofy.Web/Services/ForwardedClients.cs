using Microsoft.AspNetCore.HttpOverrides;

namespace Duofy.Web.Services;

/// <summary>
/// Die echte Adresse der Person und das Schema hinter dem Cloudflare Tunnel (Spec 79, R4), wie in der API
/// (Duofy.Api.ForwardedClients): die Adresse aus CF-Connecting-IP, das Schema aus X-Forwarded-Proto. Ohne das Schema
/// hielte sich die Web-Variante für eine http-Seite, obwohl der Browser https spricht.
///
/// Beides gilt nur, wenn die Verbindung aus einem vertrauten Netz kommt: Duofy:TrustedProxies (Umgebungsvariable
/// Duofy__TrustedProxies), Netze in CIDR-Schreibweise, durch Komma getrennt. Ohne Einstellung vertraut die Web-Variante
/// niemandem.
/// </summary>
public static class ForwardedClients
{
    public const string TrustedProxiesKey = "Duofy:TrustedProxies";

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
}
