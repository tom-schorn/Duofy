using System.Buffers.Text;
using System.Security.Cryptography;

namespace Duofy.Domain;

/// <summary>
/// Eine Einladung, sich auf diesem Server zu registrieren (Spec 70, R4), als Link
/// <c>&lt;Server&gt;/registrieren?einladung=&lt;Token&gt;</c>. Nicht zu verwechseln mit der Einladung in einen
/// Haushalt (<see cref="HouseholdInvitation"/>).
///
/// Nur Administratoren legen sie an; sie zählt nur im Registrierungsmodus "invite". Sie gilt sieben Tage und
/// einmal. Mit einer E-Mail-Adresse gilt sie nur für diese Adresse. Zurückziehen löscht die Zeile: Eine
/// fehlende Zeile kann niemand für gültig halten.
/// </summary>
public class RegistrationInvitation
{
    /// <summary>So lange gilt eine Einladung; eine feste Regel, keine Einstellung.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    /// <summary>Die größte Länge der E-Mail-Adresse, wie bei Identity.</summary>
    public const int EmailMaxLength = 256;

    /// <summary>Die eindeutige Kennung; über sie zieht der Administrator die Einladung zurück.</summary>
    public Guid Id { get; set; }

    /// <summary>32 Zufallsbytes, URL-tauglich in Base64 (43 Zeichen); eindeutig. Das Token ist der Nachweis im Link.</summary>
    public required string Token { get; set; }

    /// <summary>Der Administrator, der sie angelegt hat.</summary>
    public Guid CreatedById { get; set; }

    /// <summary>Wenn gesetzt, kann sich nur diese Adresse damit registrieren; immer in Kleinbuchstaben.</summary>
    public string? Email { get; set; }

    /// <summary>Wann die Einladung angelegt wurde.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Ab diesem Zeitpunkt gilt sie nicht mehr, sieben Tage nach dem Anlegen.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Wann sich jemand damit registriert hat; null, solange sie offen ist.</summary>
    public DateTimeOffset? UsedAt { get; set; }

    /// <summary>Wer sich damit registriert hat; null, solange sie offen ist oder nachdem das Konto gelöscht wurde.</summary>
    public Guid? UsedById { get; set; }

    /// <summary>Eine neue, offene Einladung mit frischem Token. Eine leere Adresse heißt: für jede Adresse.</summary>
    public static RegistrationInvitation Create(Guid createdById, string? email, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        Token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32)),
        CreatedById = createdById,
        Email = Normalize(email),
        CreatedAt = now,
        ExpiresAt = now + Lifetime,
    };

    /// <summary>
    /// Ob sich <paramref name="email"/> zum Zeitpunkt <paramref name="now"/> damit registrieren kann: nicht benutzt,
    /// nicht abgelaufen und, falls die Einladung eine Adresse hat, genau diese Adresse.
    /// </summary>
    public bool Admits(string email, DateTimeOffset now) =>
        UsedAt is null && now < ExpiresAt && (Email is null || Email == Normalize(email));

    /// <summary>Verbraucht die Einladung: Sie gilt danach für niemanden mehr.</summary>
    public void Use(Guid userId, DateTimeOffset now)
    {
        UsedById = userId;
        UsedAt = now;
    }

    private static string? Normalize(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
}
