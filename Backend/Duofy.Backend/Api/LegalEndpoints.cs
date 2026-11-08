using Duofy.Schemas;

namespace Duofy.Api;

/// <summary>
/// Die Rechtstexte des Servers (/api/v1/legal, Spec 70, R7): die Liste der vorhandenen Texte und je Name der
/// Text. Beides ohne Anmeldung, weil Impressum und Datenschutzerklärung jeder vor dem Anmelden lesen können
/// muss. Der Name in der Adresse ist nur ein Schlüssel; einen Pfad nimmt der Server nie aus der Anfrage.
/// </summary>
internal static class LegalEndpoints
{
    public static void MapLegalEndpoints(this IEndpointRouteBuilder v1)
    {
        var legal = v1.MapGroup("/legal").AllowAnonymous();
        legal.MapGet("", ListAsync);
        legal.MapGet("/{name}", GetAsync);
    }

    private static async Task<IResult> ListAsync(LegalDocuments documents, CancellationToken cancellationToken) =>
        Results.Ok(new LegalListResponse(await documents.AvailableAsync(cancellationToken), documents.Urls()));

    private static async Task<IResult> GetAsync(string name, LegalDocuments documents, CancellationToken cancellationToken) =>
        await documents.ReadAsync(name, cancellationToken) is { } text
            ? Results.Ok(new LegalTextResponse(text))
            : ApiErrors.NotFound(LegalErrorCodes.NotFound);
}
