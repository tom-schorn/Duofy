using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Duofy.Domain;

/// <summary>
/// Ein Link, mit dem eine Person auf einem selbst betriebenen Server ein neues Passwort setzt (Spec 53, R4), als
/// <c>&lt;Server&gt;/passwort-neu?link=&lt;Token&gt;</c>. Nur der Administrator legt ihn an und gibt ihn weiter; er
/// selbst setzt kein Passwort.
///
/// Der Link gilt 24 Stunden und einmal; je Person ist höchstens einer offen. Gespeichert ist nur der SHA-256-Wert des Tokens: Wer die Datenbank liest,
/// kann damit kein Passwort setzen. Wer ihn angelegt und wann jemand ihn benutzt hat, bleibt stehen.
/// </summary>
public class PasswordResetLink
{
    /// <summary>So lange gilt ein Link; eine feste Regel, keine Einstellung.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    public Guid Id { get; set; }

    /// <summary>Die Person, deren Passwort der Link neu setzt.</summary>
    public Guid UserId { get; set; }

    /// <summary>Der SHA-256-Wert des Tokens als Hex in Kleinbuchstaben (64 Zeichen); eindeutig.</summary>
    public required string TokenHash { get; set; }

    /// <summary>Der Administrator, der den Link angelegt hat.</summary>
    public Guid CreatedById { get; set; }

    /// <summary>Ab diesem Zeitpunkt gilt der Link nicht mehr, 24 Stunden nach dem Anlegen.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Wann die Person damit ein Passwort gesetzt hat; null, solange er offen ist.</summary>
    public DateTimeOffset? UsedAt { get; set; }

    /// <summary>Ein neuer Link mit frischem Token (32 Zufallsbytes, URL-tauglich). Das Token gibt es nur hier, nie wieder.</summary>
    public static (PasswordResetLink Link, string Token) Create(Guid userId, Guid createdById, DateTimeOffset now)
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var link = new PasswordResetLink
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Hash(token),
            CreatedById = createdById,
            ExpiresAt = now + Lifetime,
        };
        return (link, token);
    }

    /// <summary>Der gespeicherte Wert zu einem Token.</summary>
    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Ob der Link zum Zeitpunkt <paramref name="now"/> noch gilt: nicht benutzt und nicht abgelaufen.</summary>
    public bool IsUsable(DateTimeOffset now) => UsedAt is null && now < ExpiresAt;

    /// <summary>Verbraucht den Link: Er gilt danach nicht mehr.</summary>
    public void Use(DateTimeOffset now) => UsedAt = now;

    /// <summary>
    /// Verbraucht jeden Link der Liste, der noch gilt (A13): Wer ein Passwort setzt oder einen neuen Link bekommt,
    /// soll keinen älteren Link mehr in Umlauf haben. Benutzte und abgelaufene Links bleiben, wie sie sind.
    /// </summary>
    public static void CloseOpen(IEnumerable<PasswordResetLink> links, DateTimeOffset now)
    {
        foreach (var link in links.Where(l => l.IsUsable(now)))
        {
            link.Use(now);
        }
    }
}
