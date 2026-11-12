using Duofy.Schemas;

namespace Duofy.Api;

/// <summary>Fehlerantworten mit Code, zum Beispiel <c>400 { "code": "email_taken" }</c>.</summary>
internal static class ApiErrors
{
    /// <summary>Deutsche Erklärungen für Fehler, die man nicht am Code allein versteht (Issue 137).</summary>
    private static readonly Dictionary<string, string> Messages = new()
    {
        [ErrorCodes.CommitmentPotInvalid] =
            "Ein gewöhnlicher Vertrag liegt im Topf Grundbedarf oder Wünsche. Sparziele und Schulden liegen fest im Topf Sparen, Einnahmen im Topf Einnahmen; dafür die passende Vertragsart wählen.",
        [ErrorCodes.CommitmentDebtInvalid] =
            "Eine Schuld lässt sich nur mit einem Vertrag der Art Schuld oder einem gewöhnlichen Vertrag verknüpfen, der kein Zielkonto und kein Sparziel hat. Besser: den Ratenplan an der Schuld selbst anlegen.",
        [ErrorCodes.CommitmentIsDebtPlan] =
            "Dieser Vertrag ist der Ratenplan einer Schuld und lässt sich nur dort ändern oder beenden.",
    };

    /// <summary>Der deutsche Erklärsatz zu einem Fehlercode, falls es einen gibt.</summary>
    public static string? MessageFor(string code) => Messages.GetValueOrDefault(code);

    public static IResult BadRequest(string code) =>
        Results.Json(new ErrorResponse(code, MessageFor(code)), statusCode: StatusCodes.Status400BadRequest);

    public static IResult Unauthorized(string code) =>
        Results.Json(new ErrorResponse(code), statusCode: StatusCodes.Status401Unauthorized);

    public static IResult NotFound(string code) =>
        Results.Json(new ErrorResponse(code), statusCode: StatusCodes.Status404NotFound);

    /// <summary>
    /// Die Person aus <c>owner</c> teilt diesen Bereich nicht mit der angemeldeten Person (Spec 68): 404, so
    /// wie jede Person außerhalb des Haushalts aussieht.
    /// </summary>
    public static IResult OwnerNotFound() => NotFound(ErrorCodes.OwnerNotFound);
}
