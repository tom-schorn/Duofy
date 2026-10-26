using Duofy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Duofy.Data;

/// <summary>
/// Ein neuer, geänderter oder entfernter Datensatz gehört jemandem, für den die laufende Anfrage nicht schreiben darf
/// (Spec 56, R3). Die API antwortet darauf 404. Die Meldung nennt nur Art und Kennungen, nie Feldwerte.
/// </summary>
public sealed class OwnerViolationException(string entityType, string entityKey, Guid? ownerId, Guid? currentUserId)
    : InvalidOperationException($"{entityType} {entityKey} gehört {ownerId?.ToString() ?? "niemandem"}; {currentUserId?.ToString() ?? "niemand angemeldet"} darf dafür nicht schreiben.")
{
    /// <summary>Der Name der Klasse, zum Beispiel "Booking".</summary>
    public string EntityType { get; } = entityType;

    /// <summary>Der Schlüssel des Datensatzes als Text.</summary>
    public string EntityKey { get; } = entityKey;

    /// <summary>
    /// Der Besitzer, der im Datensatz steht, bei Posten und Buchungszeilen der ihres Plans oder ihrer Buchung; null bei
    /// einer eingebauten Kategorie und wenn sich der Plan oder die Buchung nicht finden ließ.
    /// </summary>
    public Guid? OwnerId { get; } = ownerId;

    /// <summary>Die angemeldete Person; null ohne Anmeldung.</summary>
    public Guid? CurrentUserId { get; } = currentUserId;
}

/// <summary>
/// Prüft vor jedem Speichern, dass jeder neue, geänderte oder entfernte Datensatz mit Besitzer jemandem gehört, für
/// den die Anfrage schreiben darf (<see cref="IOwnerContext.WritableOwners"/>). Sonst wird nichts gespeichert: Die
/// Ausnahme kommt, bevor EF Core einen Befehl an die Datenbank schickt.
///
/// Posten und Buchungszeilen haben keinen eigenen Besitzer; für sie zählt der Besitzer ihres Plans oder ihrer
/// Buchung (Spec 56, A7). Ist der nicht geladen, schlägt die Prüfung ihn mit einer Abfrage je Art nach. Ohne Besitzer
/// (eingebaute Kategorie) darf in einer Anfrage niemand schreiben; die legen nur Migrationen an.
/// </summary>
public sealed class OwnerSaveCheck : SaveChangesInterceptor
{
    private const string ParentKey = "Id";

    /// <summary>Die Prüfung hat keinen eigenen Zustand; alle Verbindungen teilen sich eine.</summary>
    public static OwnerSaveCheck Instance { get; } = new();

    private OwnerSaveCheck()
    {
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        CheckAsync(eventData.Context, async: false, CancellationToken.None).GetAwaiter().GetResult();
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        await CheckAsync(eventData.Context, async: true, cancellationToken);
        return result;
    }

    // Mit async: false läuft alles ohne Warten durch; so teilen sich SaveChanges und SaveChangesAsync eine Prüfung.
    private static async Task CheckAsync(DbContext? context, bool async, CancellationToken cancellationToken)
    {
        if (context is not DuofyDbContext db || db.Owners.IsBypassed)
        {
            return;
        }
        var entries = db.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries.Where(e => HasOwner(e.Entity)))
        {
            // Bei einer Änderung zählt auch der Besitzer von vorher: Sonst könnte jemand, der einen Datensatz nur
            // sieht, ihn sich durch einen neuen Besitzer selbst zuschreiben. Beim Entfernen zählt nur er.
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                RequireWritable(db, entry, Owner(entry, original: true));
            }
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                RequireWritable(db, entry, Owner(entry, original: false));
            }
        }

        var positions = entries.Where(e => e.Entity is PlanPosition).ToList();
        var lines = entries.Where(e => e.Entity is BookingLine).ToList();
        if (positions.Count == 0 && lines.Count == 0)
        {
            return;
        }
        var planOwners = await ParentOwnersAsync(db, db.Plans, positions, async, cancellationToken);
        var bookingOwners = await ParentOwnersAsync(db, db.Bookings, lines, async, cancellationToken);
        foreach (var (children, owners) in new[] { (positions, planOwners), (lines, bookingOwners) })
        {
            foreach (var entry in children)
            {
                foreach (var parentId in ParentIds(entry))
                {
                    RequireWritable(db, entry, owners.TryGetValue(parentId, out var owner) ? owner : null);
                }
            }
        }
    }

    private static void RequireWritable(DuofyDbContext db, EntityEntry entry, Guid? ownerId)
    {
        if (ownerId is not { } owner || !db.Owners.WritableOwners.Contains(owner))
        {
            throw new OwnerViolationException(entry.Metadata.ClrType.Name, KeyOf(entry), ownerId, db.Owners.CurrentUserId);
        }
    }

    /// <summary>True für die Datensätze mit eigenem Besitzer, auch für eingebaute Kategorien (Besitzer null).</summary>
    private static bool HasOwner(object entity) => entity is Account or Booking or Commitment or Plan or Category;

    /// <summary>Der Besitzer, wie er geladen wurde oder wie er gespeichert würde; null bei eingebauten Kategorien.</summary>
    private static Guid? Owner(EntityEntry entry, bool original)
    {
        var property = entry.Property(nameof(Account.OwnerId));
        return (Guid?)(original ? property.OriginalValue : property.CurrentValue);
    }

    /// <summary>
    /// Die Eltern, die für einen Posten oder eine Zeile zählen: beim Anlegen der neue, beim Entfernen der alte, beim
    /// Ändern beide, damit niemand einen Posten oder eine Zeile in einen fremden Plan oder eine fremde Buchung umhängt.
    /// </summary>
    private static IEnumerable<Guid> ParentIds(EntityEntry entry)
    {
        var key = entry.Property(entry.Entity is PlanPosition ? DuofyDbContext.PositionPlan : "BookingId");
        var ids = new List<Guid>();
        if (entry.State is EntityState.Modified or EntityState.Deleted && key.OriginalValue is Guid before)
        {
            ids.Add(before);
        }
        if (entry.State is EntityState.Added or EntityState.Modified && key.CurrentValue is Guid after)
        {
            ids.Add(after);
        }
        return ids.Distinct();
    }

    /// <summary>
    /// Die Besitzer der Eltern: geladene aus dem ChangeTracker (wie sie geladen wurden), die übrigen mit einer
    /// Abfrage an jedem Filter vorbei, damit auch der Besitzer eines unsichtbaren Elternteils in der Meldung steht.
    /// </summary>
    private static async Task<Dictionary<Guid, Guid>> ParentOwnersAsync<TParent>(
        DuofyDbContext db, DbSet<TParent> set, List<EntityEntry> children, bool async, CancellationToken cancellationToken)
        where TParent : class
    {
        var owners = new Dictionary<Guid, Guid>();
        var tracked = db.ChangeTracker.Entries<TParent>()
            .ToDictionary(p => (Guid)p.Property(ParentKey).CurrentValue!);
        var missing = new List<Guid>();
        foreach (var id in children.SelectMany(ParentIds).Distinct())
        {
            if (tracked.TryGetValue(id, out var parent) && Owner(parent, original: true) is { } owner)
            {
                owners[id] = owner;
            }
            else
            {
                missing.Add(id);
            }
        }
        if (missing.Count == 0)
        {
            return owners;
        }
        var query = set.IgnoreQueryFilters().AsNoTracking()
            .Where(p => missing.Contains(EF.Property<Guid>(p, ParentKey)))
            .Select(p => new { Id = EF.Property<Guid>(p, ParentKey), OwnerId = EF.Property<Guid>(p, nameof(Plan.OwnerId)) });
        var found = async ? await query.ToListAsync(cancellationToken) : query.ToList();
        foreach (var parent in found)
        {
            owners[parent.Id] = parent.OwnerId;
        }
        return owners;
    }

    private static string KeyOf(EntityEntry entry) =>
        string.Join(",", entry.Metadata.FindPrimaryKey()!.Properties.Select(p => entry.Property(p.Name).CurrentValue));
}
