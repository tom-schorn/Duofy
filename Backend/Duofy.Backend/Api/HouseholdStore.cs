using Duofy.Schemas;
using Duofy.Data;
using Duofy.Domain;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api;

/// <summary>
/// Liest und ändert Haushalte für die Endpunkte (Spec 67): finden, sperren, den Gründer weitergeben
/// und die Antwort bauen.
///
/// Wer Mitgliedschaften ändert, sperrt zuerst die Zeile des Haushalts (<see cref="LockAsync"/>) und liest
/// danach erst die Mitglieder (Entscheidung 68 des Originals). Ein Gründer, der sein Konto löscht, während
/// das einzige andere Mitglied gerade austritt, sähe es sonst noch und gäbe den Haushalt an jemanden weiter,
/// den es darin nicht mehr gibt.
/// </summary>
internal sealed class HouseholdStore
{
    private readonly DuofyDbContext db;

    public HouseholdStore(DuofyDbContext db)
    {
        this.db = db;
    }

    /// <summary>Die Kennung des Haushalts der Person; null, wenn sie in keinem ist.</summary>
    public Task<Guid?> IdOfAsync(Guid userId, CancellationToken cancellationToken) =>
        db.HouseholdMembers
            .Where(m => m.UserId == userId)
            .Select(m => (Guid?)m.HouseholdId)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Die Rolle der Person in diesem Haushalt; null, wenn sie nicht dazugehört.</summary>
    public Task<HouseholdRole?> RoleOfAsync(Guid householdId, Guid userId, CancellationToken cancellationToken) =>
        db.HouseholdMembers
            .Where(m => m.HouseholdId == householdId && m.UserId == userId)
            .Select(m => (HouseholdRole?)m.Role)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Der Haushalt mit seinen Mitgliedern, frisch aus der Datenbank.</summary>
    public Task<Household> LoadAsync(Guid householdId, CancellationToken cancellationToken) =>
        db.Households.Include(h => h.Members).SingleAsync(h => h.Id == householdId, cancellationToken);

    /// <summary>
    /// Sperrt die Zeile des Haushalts bis zum Ende der Transaktion (<c>SELECT … FOR UPDATE</c>). Eine
    /// zweite Änderung an denselben Mitgliedschaften wartet, bis die erste fertig ist, und sieht sie dann.
    /// Muss in einer Transaktion laufen.
    /// </summary>
    public Task LockAsync(Guid householdId, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlRawAsync("SELECT id FROM households WHERE id = {0} FOR UPDATE", [householdId], cancellationToken);

    /// <summary>
    /// Vor dem Löschen eines Kontos (R9): Ist die Person Gründer, wird das Mitglied Gründer, das am frühesten
    /// dazukam; ist sonst niemand im Haushalt, verschwindet er mit seinen Einladungen. Die Mitgliedschaft selbst
    /// löscht die Datenbank mit dem Benutzer. Muss in derselben Transaktion laufen wie das Löschen.
    /// </summary>
    public async Task HandOverAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Erst sperren, dann lesen (Entscheidung 68). Hat sich die Mitgliedschaft dazwischen geändert, zum
        // Beispiel durch ein gleichzeitiges Beitreten, wird der neue Haushalt gesperrt und neu gelesen.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (await IdOfAsync(userId, cancellationToken) is not { } householdId)
            {
                return;
            }
            await LockAsync(householdId, cancellationToken);
            var household = await LoadAsync(householdId, cancellationToken);
            if (household.MemberOf(userId) is not { } member)
            {
                continue;
            }

            if (household.NextFounder(userId) is not { } next)
            {
                db.Households.Remove(household);
            }
            else if (member.Role == HouseholdRole.Founder)
            {
                next.Role = HouseholdRole.Founder;
            }
            await db.SaveChangesAsync(cancellationToken);
            return;
        }
        throw new InvalidOperationException($"Haushalt von Konto {userId} hat sich beim Löschen dreimal geändert.");
    }

    /// <summary>
    /// Die Antwort mit den Mitgliedern in der Reihenfolge, in der sie dazukamen; "du" ist <paramref name="viewerId"/>.
    /// Jedes andere Mitglied trägt, was es "dir" erlaubt und was du ihm erlaubst (Spec 68); Freigaben zwischen
    /// zwei anderen Mitgliedern sieht niemand.
    /// </summary>
    public async Task<HouseholdResponse> ResponseAsync(Household household, Guid viewerId, CancellationToken cancellationToken)
    {
        var ids = household.Members.Select(m => m.UserId).ToList();
        var users = await db.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);
        var grants = await db.Grants
            .AsNoTracking()
            .Where(g => (g.GranterId == viewerId && ids.Contains(g.GranteeId)) || (g.GranteeId == viewerId && ids.Contains(g.GranterId)))
            .ToListAsync(cancellationToken);
        var members = household.Members
            .Where(m => users.ContainsKey(m.UserId))
            .OrderBy(m => m.JoinedAt)
            .ThenBy(m => m.UserId)
            .Select(m => ToResponse(m, users[m.UserId], viewerId, grants))
            .ToList();
        return new HouseholdResponse(household.Id, household.Name, household.QuotaNeeds, household.QuotaWants, household.QuotaSavings, members);
    }

    /// <summary>Ein Mitglied, so wie <paramref name="viewerId"/> es sieht, frisch aus der Datenbank.</summary>
    public async Task<MemberResponse> MemberResponseAsync(HouseholdMember member, Guid viewerId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == member.UserId, cancellationToken);
        var grants = await db.Grants
            .AsNoTracking()
            .Where(g => (g.GranterId == viewerId && g.GranteeId == member.UserId) || (g.GranteeId == viewerId && g.GranterId == member.UserId))
            .ToListAsync(cancellationToken);
        return ToResponse(member, user, viewerId, grants);
    }

    /// <summary>
    /// Setzt, was <paramref name="granterId"/> <paramref name="granteeId"/> erlaubt, nur in den angegebenen
    /// Bereichen (R1, R2). Nichts löscht die Zeile, damit "keine Zeile" und "Nichts" nie zweierlei heißen.
    /// Speichert noch nicht.
    /// </summary>
    public async Task SetGrantsAsync(Guid granterId, Guid granteeId, IReadOnlyDictionary<Area, AccessLevel> levels, CancellationToken cancellationToken)
    {
        var existing = await db.Grants
            .Where(g => g.GranterId == granterId && g.GranteeId == granteeId)
            .ToDictionaryAsync(g => g.Area, cancellationToken);
        foreach (var (area, level) in levels)
        {
            existing.TryGetValue(area, out var row);
            if (level == AccessLevel.None)
            {
                if (row is not null)
                {
                    db.Grants.Remove(row);
                }
            }
            else if (row is null)
            {
                db.Grants.Add(new Grant { GranterId = granterId, GranteeId = granteeId, Area = area, Level = level });
            }
            else
            {
                row.Level = level;
            }
        }
    }

    /// <summary>
    /// Löscht jede Freigabe von und an die Person (R3), beim Austreten und Entfernen. Eine Freigabe ist Vertrauen
    /// in einem Haushalt; wer später wieder beitritt, fängt bei Nichts an. Muss in derselben Transaktion laufen
    /// wie das Ändern der Mitgliedschaft.
    /// </summary>
    public Task DropGrantsAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Grants
            .Where(g => g.GranterId == userId || g.GranteeId == userId)
            .ExecuteDeleteAsync(cancellationToken);

    private static MemberResponse ToResponse(HouseholdMember member, AppUser user, Guid viewerId, IReadOnlyCollection<Grant> grants)
    {
        var isMe = member.UserId == viewerId;
        IEnumerable<Grant> toMe = isMe ? [] : grants.Where(g => g.GranterId == member.UserId && g.GranteeId == viewerId);
        IEnumerable<Grant> mine = isMe ? [] : grants.Where(g => g.GranterId == viewerId && g.GranteeId == member.UserId);
        return new(member.UserId, user.FirstName, user.LastName, user.Email ?? "", RoleKey(member.Role), isMe,
            GrantKeys.ToResponse(Grant.LevelsOf(toMe)), GrantKeys.ToResponse(Grant.LevelsOf(mine)));
    }

    public static string RoleKey(HouseholdRole role) =>
        role == HouseholdRole.Founder ? HouseholdRoles.Founder : HouseholdRoles.Member;
}
