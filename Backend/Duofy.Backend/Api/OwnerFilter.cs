using Duofy.Data;

namespace Duofy.Api;

/// <summary>
/// Hängt den Besitzer-Filter der Datenbank (Spec 56) an jede Anfrage: Am Anfang lädt er mit einer Abfrage der
/// Freigaben, wessen Daten die angemeldete Person sehen und schreiben darf; ohne Anmeldung bleibt alles leer.
/// Scheitert ein Speichern an einem fremden Besitzer, antwortet die API 404 wie für jede Person außerhalb des
/// Haushalts. Das passiert nur, wenn ein Endpunkt seine Prüfung vergessen hat; deshalb steht es als Fehler im
/// Protokoll, mit Art und Kennungen, nie mit Feldwerten.
/// </summary>
internal static class OwnerFilter
{
    private const string LogCategory = "Duofy.Api.OwnerFilter";

    public static IApplicationBuilder UseOwnerFilter(this IApplicationBuilder app) => app.Use(RunAsync);

    internal static async Task RunAsync(HttpContext context, RequestDelegate next)
    {
        var services = context.RequestServices;
        if (services.GetRequiredService<IAccess>().CallerId is { } callerId)
        {
            await services.GetRequiredService<OwnerContext>()
                .LoadAsync(services.GetRequiredService<DuofyDbContext>(), callerId, context.RequestAborted);
        }

        try
        {
            await next(context);
        }
        catch (OwnerViolationException ex) when (!context.Response.HasStarted)
        {
            services.GetRequiredService<ILoggerFactory>().CreateLogger(LogCategory).LogError(
                ex,
                "Speichern abgelehnt: {EntityType} {EntityKey} gehört {OwnerId}, angemeldet ist {UserId}.",
                ex.EntityType, ex.EntityKey, ex.OwnerId, ex.CurrentUserId);
            await ApiErrors.OwnerNotFound().ExecuteAsync(context);
        }
    }
}
