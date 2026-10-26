using Duofy.Data;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api;

/// <summary>
/// Was die Regeln für Buchungen von einem Konto wissen müssen. Die Buchungsregeln hängen nur an
/// dieser kleinen Schnittstelle und nicht an der Tabelle der Konten; in den Tests der Regeln steht
/// ein einfacher Ersatz an ihrer Stelle, im Betrieb liest <see cref="DbAccountLookup"/> die Tabelle.
/// </summary>
/// <param name="Id">Die Kennung des Kontos.</param>
/// <param name="OwnerId">Die Person, der das Konto gehört.</param>
/// <param name="Active">False bei einem aufgelösten Konto; darauf gibt es keine neuen Buchungen.</param>
/// <param name="OpeningDate">Der Starttag des Kontos; Buchungen an diesem Tag oder davor stecken schon im Startbetrag.</param>
/// <param name="IsDefault">True, wenn es das Standardkonto der Person ist.</param>
public sealed record AccountInfo(Guid Id, Guid OwnerId, bool Active, DateOnly OpeningDate, bool IsDefault);

/// <summary>Sucht ein Konto einer Person.</summary>
public interface IAccountLookup
{
    /// <summary>
    /// Liefert das Konto, wenn es existiert und der Person gehört. Ein Konto einer anderen
    /// Person und eine unbekannte Kennung sehen für den Aufrufer gleich aus: null.
    /// </summary>
    Task<AccountInfo?> FindAsync(Guid ownerId, Guid accountId, CancellationToken cancellationToken);
}

/// <summary>Liest die Konten aus der Datenbank.</summary>
internal sealed class DbAccountLookup(DuofyDbContext db) : IAccountLookup
{
    public Task<AccountInfo?> FindAsync(Guid ownerId, Guid accountId, CancellationToken cancellationToken) =>
        db.Accounts
            .AsNoTracking()
            .Where(a => a.Id == accountId && a.OwnerId == ownerId)
            .Select(a => new AccountInfo(a.Id, a.OwnerId, a.Active, a.OpeningDate, a.IsDefault))
            .FirstOrDefaultAsync(cancellationToken);
}
