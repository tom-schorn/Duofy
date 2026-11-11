using Duofy.Data;
using Duofy.Domain;
using Duofy.Schemas;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api;

/// <summary>
/// Das Protokoll der Haushaltsereignisse und die Abendzusammenfassung daraus (Spec 118, R8). Ereignisse werden dort
/// vermerkt, wo sie geschehen (Abhaken, Buchen, Einladen, Beitreten, Freigaben); die Zusammenfassung liest sie später und
/// zeigt jeder Person nur, was sie nach den bestehenden Freigaben sehen darf.
/// </summary>
internal static class HouseholdSummary
{
    /// <summary>Älter als so viele Tage wird ein Ereignis beim nächsten Vermerken gelöscht; die Zusammenfassung kommt jeden Abend.</summary>
    private static readonly TimeSpan Retention = TimeSpan.FromDays(14);

    /// <summary>Vermerkt ein Ereignis; gespeichert wird mit dem nächsten SaveChanges des Aufrufers (gleiche Transaktion wie die Tat).</summary>
    public static void Record(
        DuofyDbContext db, Guid householdId, HouseholdEventKind kind, Guid actorId, DateTimeOffset now,
        Guid? ownerId = null, Guid? subjectId = null, string? label = null, decimal? amount = null)
    {
        db.HouseholdEvents.Add(new HouseholdEvent
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId,
            Kind = kind,
            ActorId = actorId,
            OwnerId = ownerId,
            SubjectId = subjectId,
            Label = label is { Length: > 200 } ? label[..200] : label,
            Amount = amount,
            OccurredAt = now,
        });
    }

    /// <summary>
    /// Vermerkt das Ereignis in dem Haushalt, in dem <paramref name="personId"/> Mitglied ist (nichts, wenn die Person in keinem
    /// ist: Dann gibt es niemanden, der es lesen soll). Gespeichert wird mit dem nächsten SaveChanges des Aufrufers.
    /// </summary>
    public static async Task RecordForAsync(
        DuofyDbContext db, Guid personId, HouseholdEventKind kind, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken,
        Guid? ownerId = null, Guid? subjectId = null, string? label = null, decimal? amount = null)
    {
        foreach (var householdId in await HouseholdsOfAsync(db, personId, cancellationToken))
        {
            Record(db, householdId, kind, actorId, now, ownerId, subjectId, label, amount);
        }
    }

    /// <summary>Die Haushalte, in denen die Person Mitglied ist.</summary>
    public static Task<List<Guid>> HouseholdsOfAsync(DuofyDbContext db, Guid userId, CancellationToken cancellationToken) =>
        db.HouseholdMembers.Where(m => m.UserId == userId).Select(m => m.HouseholdId).ToListAsync(cancellationToken);

    /// <summary>
    /// Die Ereignisse im Zeitraum (von ausschließlich bis einschließlich), die diese Person sehen darf: nicht ihre eigenen,
    /// Abhaken mit dem Recht auf den Plan des Besitzers, Buchungen mit dem Recht auf das Buch, Einladung und Beitritt
    /// für jedes Mitglied, eine geänderte Freigabe nur für die Person, die sie betrifft. Der Aufrufer hat den Besitzerfilter
    /// ausgeschaltet.
    /// </summary>
    public static async Task<List<HouseholdEventLine>> VisibleAsync(
        DuofyDbContext db, Guid recipientId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        var households = await HouseholdsOfAsync(db, recipientId, cancellationToken);
        if (households.Count == 0)
        {
            return [];
        }
        var events = await db.HouseholdEvents.AsNoTracking()
            .Where(e => households.Contains(e.HouseholdId) && e.OccurredAt > from && e.OccurredAt <= until && e.ActorId != recipientId)
            .OrderBy(e => e.OccurredAt)
            .ToListAsync(cancellationToken);
        if (events.Count == 0)
        {
            return [];
        }

        var viewed = (await db.Grants.AsNoTracking()
                .Where(g => g.GranteeId == recipientId && g.Level >= AccessLevel.View)
                .Select(g => new { g.GranterId, g.Area })
                .ToListAsync(cancellationToken))
            .Select(g => (g.GranterId, g.Area)).ToHashSet();
        bool May(Guid? owner, Area area) => owner is { } id && (id == recipientId || viewed.Contains((id, area)));

        var visible = events.Where(e => e.Kind switch
        {
            HouseholdEventKind.Ticked => May(e.OwnerId, Area.Plan),
            HouseholdEventKind.Booked => May(e.OwnerId, Area.Book),
            HouseholdEventKind.Invited or HouseholdEventKind.Joined => true,
            HouseholdEventKind.GrantChanged => e.SubjectId == recipientId,
            _ => false,
        }).ToList();
        if (visible.Count == 0)
        {
            return [];
        }

        var actorIds = visible.Select(e => e.ActorId).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => actorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FirstName, cancellationToken);
        return visible.Select(e => new HouseholdEventLine(
            e.Kind.ToString().ToLowerInvariant(), names.GetValueOrDefault(e.ActorId, ""), e.Label, e.Amount)).ToList();
    }

    /// <summary>Löscht alte Ereignisse (Aufräumen im Job, nicht beim Vermerken, damit eine Tat nie daran scheitert).</summary>
    public static async Task PruneAsync(DuofyDbContext db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Einzeln gelöscht statt mit einem Massenbefehl: Massenlöschungen stehen auf der freigegebenen Liste (OwnerBulkListTests).
        var limit = now - Retention;
        var old = await db.HouseholdEvents.Where(e => e.OccurredAt < limit).ToListAsync(cancellationToken);
        if (old.Count > 0)
        {
            db.HouseholdEvents.RemoveRange(old);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
