using System.Buffers.Text;
using System.Security.Cryptography;

namespace Duofy.Domain;

/// <summary>
/// Eine Einladung in einen Haushalt als Link <c>&lt;Server&gt;/einladung/&lt;Token&gt;</c>. Wer den Link hat,
/// darf einmal beitreten, innerhalb von 14 Tagen und solange der Gründer sie nicht zurückgezogen hat, und nur, wer noch in keinem Haushalt ist.
///
/// Der Link ist mit Absicht der Nachweis: Er hängt an keiner E-Mail-Adresse, weil die Adressen noch
/// nicht bestätigt werden (#53) und sich sonst jemand mit fremder Adresse die Einladung holen könnte.
/// </summary>
public class HouseholdInvitation
{
    /// <summary>Die eindeutige Kennung; der Gründer zieht die Einladung über sie zurück.</summary>
    public Guid Id { get; set; }

    /// <summary>Der Haushalt, in den eingeladen wird.</summary>
    public Guid HouseholdId { get; set; }

    /// <summary>Wer eingeladen hat; die Vorschau nennt diese Person.</summary>
    public Guid CreatedById { get; set; }

    /// <summary>32 Zufallsbytes, URL-tauglich in Base64 (43 Zeichen); eindeutig.</summary>
    public required string Token { get; set; }

    /// <summary>Offen, angenommen oder zurückgezogen. Abgelaufen ist kein Status, sondern eine Frage der Zeit.</summary>
    public InvitationStatus Status { get; set; }

    /// <summary>Wann die Einladung angelegt wurde.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Ab diesem Zeitpunkt gilt die Einladung nicht mehr, 14 Tage nach dem Anlegen.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Wer mit der Einladung beigetreten ist; null, solange sie offen ist.</summary>
    public Guid? UsedById { get; set; }

    /// <summary>Wann sie benutzt wurde.</summary>
    public DateTimeOffset? UsedAt { get; set; }

    /// <summary>Eine neue, offene Einladung mit frischem Token.</summary>
    public static HouseholdInvitation Create(Guid householdId, Guid createdById, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        HouseholdId = householdId,
        CreatedById = createdById,
        Token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32)),
        Status = InvitationStatus.Pending,
        CreatedAt = now,
        ExpiresAt = now + HouseholdRules.InvitationLifetime,
    };

    /// <summary>
    /// Ob die Einladung zum Zeitpunkt <paramref name="now"/> noch angenommen werden kann. Benutzt oder
    /// zurückgezogen geht vor abgelaufen.
    /// </summary>
    public InvitationState StateAt(DateTimeOffset now) =>
        Status != InvitationStatus.Pending ? InvitationState.NotOpen
        : now >= ExpiresAt ? InvitationState.Expired
        : InvitationState.Open;

    /// <summary>Verbraucht die Einladung: Sie gilt danach nicht mehr.</summary>
    public void Accept(Guid userId, DateTimeOffset now)
    {
        Status = InvitationStatus.Accepted;
        UsedById = userId;
        UsedAt = now;
    }

    /// <summary>Zieht die Einladung zurück; der Link gilt danach nicht mehr.</summary>
    public void Revoke() => Status = InvitationStatus.Revoked;
}

/// <summary>Der gespeicherte Status einer Einladung.</summary>
public enum InvitationStatus
{
    Pending,
    Accepted,
    Revoked,
}

/// <summary>Ob eine Einladung gerade angenommen werden kann; siehe <see cref="HouseholdInvitation.StateAt"/>.</summary>
public enum InvitationState
{
    Open,
    NotOpen,
    Expired,
}
