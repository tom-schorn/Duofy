using Duofy.Schemas;

namespace Duofy.Api;

/// <summary>Fehlerantworten mit Code, zum Beispiel <c>400 { "code": "email_taken" }</c>.</summary>
internal static class ApiErrors
{
    public static IResult BadRequest(string code) =>
        Results.Json(new ErrorResponse(code), statusCode: StatusCodes.Status400BadRequest);

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
