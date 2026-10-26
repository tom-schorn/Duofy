using Duofy.Schemas;

namespace Duofy.Api;

/// <summary>
/// Sagt, welche Anmeldewege dieser Server anbietet (/api/v1/instance). Die App fragt
/// das nach der Wahl eines Servers. Der Endpunkt ist offen und verrät nur diese Liste, den Registrierungsmodus, ob
/// der Einrichtungscode gerade gilt (Spec 70), ob er die gehostete Version ist (Spec 53) und die Namen der Rechtstexte, die der Server anbietet. Ob ein Code
/// eingestellt ist, solange es schon einen Administrator gibt, verrät er nicht.
/// </summary>
internal static class InstanceEndpoints
{
    public static void MapInstanceEndpoints(this IEndpointRouteBuilder v1)
    {
        v1.MapGet("/instance", Get).AllowAnonymous();
    }

    // Google kommt mit Ticket 62 dazu, dann aus der Konfiguration.
    private static async Task<IResult> Get(
        LegalDocuments legal, InstanceSettings settings, InstanceAdmins admins, HostedSettings hosted, CancellationToken cancellationToken)
    {
        var setupPending = settings.HasSetupCode && !await admins.ExistAsync(cancellationToken);
        return Results.Ok(new InstanceResponse(
            [SignInMethodNames.Password],
            settings.RegistrationMode,
            setupPending,
            await legal.AvailableAsync(cancellationToken),
            hosted.Hosted));
    }
}
