namespace Duofy.ApiClient;

/// <summary>
/// Das Token-Paar einer Anmeldung: das kurzlebige Access-Token (15 Minuten), das
/// jede Anfrage mitschickt, und das langlebige Refresh-Token (30 Tage), mit dem
/// ohne Passwort ein neues Paar geholt wird.
/// </summary>
public sealed record AuthTokens(string AccessToken, string RefreshToken);

/// <summary>
/// Der Ort, an dem ein Host die Tokens aufbewahrt.
///
/// Jeder Host füllt diese Schnittstelle selbst aus: Die App legt die Tokens in den
/// SecureStorage des Geräts, die Web-Variante in die Sitzung auf dem Web-Server,
/// damit kein Skript im Browser sie lesen kann.
///
/// Die Clients dieses Projekts lesen und schreiben die Ablage selbst. Seiten müssen
/// sie nicht anfassen; angemeldet ist, wer hier Tokens liegen hat.
/// </summary>
public interface ITokenStore
{
    /// <summary>Liefert die gespeicherten Tokens oder null, wenn niemand angemeldet ist.</summary>
    Task<AuthTokens?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Speichert ein neues Token-Paar und ersetzt das alte. Kann die Ablage gerade
    /// nichts speichern, wirft sie <see cref="TokenStoreUnavailableException"/>.
    /// </summary>
    Task SaveAsync(AuthTokens tokens, CancellationToken cancellationToken = default);

    /// <summary>Löscht die Tokens. Danach ist auf diesem Gerät niemand mehr angemeldet.</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Die Ablage kann gerade keine Tokens speichern, zum Beispiel weil der Browser kein
/// Cookie annimmt. <see cref="ErrorCode"/> ist ein Code aus <see cref="ClientErrorCodes"/>;
/// die Clients geben ihn als Ergebnis zurück, damit die Oberfläche ihn erklären kann.
/// </summary>
public sealed class TokenStoreUnavailableException : Exception
{
    public TokenStoreUnavailableException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
