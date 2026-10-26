using Duofy.Data;
using Duofy.Domain;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api;

/// <summary>Was die Regeln für Buchungen von einer Kategorie wissen müssen.</summary>
/// <param name="Id">Die Kennung der Kategorie.</param>
/// <param name="IsIncome">True bei einer Kategorie der Gruppe Einnahmen.</param>
public sealed record CategoryInfo(Guid Id, bool IsIncome);

/// <summary>Sucht eine Kategorie, die eine Person benutzen darf.</summary>
public interface ICategoryLookup
{
    /// <summary>
    /// Liefert die Kategorie, wenn sie eingebaut ist oder der Person gehört; sonst null.
    /// Eine fremde Kategorie und eine unbekannte Kennung sehen für den Aufrufer gleich aus.
    /// </summary>
    Task<CategoryInfo?> FindAsync(Guid ownerId, Guid categoryId, CancellationToken cancellationToken);
}

/// <summary>Liest die Kategorien aus der Datenbank.</summary>
internal sealed class DbCategoryLookup(DuofyDbContext db) : ICategoryLookup
{
    public async Task<CategoryInfo?> FindAsync(Guid ownerId, Guid categoryId, CancellationToken cancellationToken)
    {
        var category = await db.Categories
            .AsNoTracking()
            .Where(c => c.Id == categoryId && (c.OwnerId == null || c.OwnerId == ownerId))
            .Select(c => new { c.Id, c.Group })
            .FirstOrDefaultAsync(cancellationToken);
        return category is null ? null : new CategoryInfo(category.Id, category.Group == CategoryGroup.Income);
    }
}
