namespace Duofy.ApiClient;

/// <summary>
/// Das Ergebnis eines API-Aufrufs ohne Rückgabewert.
///
/// Die Clients werfen bei erwartbaren Fehlern keine Ausnahme, sondern liefern einen
/// Fehlercode: einen Code der API aus <see cref="Duofy.Schemas.ErrorCodes"/> oder
/// einen Code des Clients aus <see cref="ClientErrorCodes"/>. Die Oberfläche
/// übersetzt jeden Code in einen deutschen Text.
/// </summary>
public class ApiResult
{
    protected ApiResult(string? errorCode)
    {
        ErrorCode = errorCode;
    }

    /// <summary>Der Fehlercode oder null, wenn der Aufruf geklappt hat.</summary>
    public string? ErrorCode { get; }

    /// <summary>True, wenn der Aufruf geklappt hat.</summary>
    public bool Succeeded => ErrorCode is null;

    public static ApiResult Success() => new(null);

    public static ApiResult Failure(string errorCode) => new(errorCode);
}

/// <summary>Das Ergebnis eines API-Aufrufs mit Rückgabewert.</summary>
public sealed class ApiResult<T> : ApiResult
{
    private ApiResult(T? value, string? errorCode) : base(errorCode)
    {
        Value = value;
    }

    /// <summary>Der Rückgabewert; nur gesetzt, wenn <see cref="ApiResult.Succeeded"/> true ist.</summary>
    public T? Value { get; }

    public static ApiResult<T> Success(T value) => new(value, null);

    public static new ApiResult<T> Failure(string errorCode) => new(default, errorCode);
}

/// <summary>
/// Fehlercodes, die nicht von der API kommen, sondern vom Client selbst.
/// </summary>
public static class ClientErrorCodes
{
    /// <summary>Niemand ist angemeldet, oder die Anmeldung ist abgelaufen.</summary>
    public const string NotSignedIn = "not_signed_in";

    /// <summary>Die API war nicht erreichbar, zum Beispiel ohne Netz.</summary>
    public const string ServerUnreachable = "server_unreachable";

    /// <summary>
    /// Die Web-Variante kann die Anmeldung nicht ablegen, weil der Browser das Cookie
    /// der Sitzung nicht annimmt.
    /// </summary>
    public const string CookiesRequired = "cookies_required";

    /// <summary>Zu viele Anfragen in kurzer Zeit; später noch einmal versuchen.</summary>
    public const string TooManyRequests = "too_many_requests";

    /// <summary>Die API kennt die Kennung nicht, oder sie gehört einer anderen Person (Antwort 404).</summary>
    public const string NotFound = "not_found";

    /// <summary>Eine Antwort, mit der der Client nicht gerechnet hat.</summary>
    public const string Unexpected = "unexpected_error";
}
