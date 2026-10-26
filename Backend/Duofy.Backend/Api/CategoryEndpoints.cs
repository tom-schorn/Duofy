using System.Security.Claims;
using Duofy.Schemas;
using Duofy.Data;
using Duofy.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api;

/// <summary>
/// Die Kategorien (/api/v1/categories): die eingebauten lesen, eigene anlegen,
/// ändern und löschen. Mit <c>?owner=</c> liest ein Mitglied die Kategorien einer Person, die ihm
/// irgendeinen Bereich freigegeben hat (Spec 68); sie braucht es, um für sie zu buchen und zu planen.
/// Schreiben darf nur die Person selbst, geprüft von <see cref="IAccess"/>. Fremde Kategorien und
/// eingebaute Kategorien gelten beim Ändern und Löschen als "nicht gefunden" (404),
/// so erfährt niemand, ob es die Kennung gibt.
/// </summary>
internal static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder v1)
    {
        var categories = v1.MapGroup("/categories").RequireAuthorization();
        categories.MapGet("", ListAsync);
        categories.MapPost("", CreateAsync);
        categories.MapPut("/{id:guid}", UpdateAsync);
        categories.MapDelete("/{id:guid}", DeleteAsync);
    }

    private static CategoryResponse ToResponse(Category category) => new(
        category.Id,
        category.Key,
        category.Key is { } key ? CategoryCatalog.GermanName(key) : category.Name!,
        CategoryGroups.ToKey(category.Group),
        PotKey.ToKey(category.SuggestedPot),
        category.IsBuiltIn);

    private static async Task<IResult> ListAsync(
        Guid? owner, IAccess access, DuofyDbContext db, CancellationToken cancellationToken)
    {
        if (access.CallerId is not { } callerId)
        {
            return Results.Unauthorized();
        }
        var ownerId = owner ?? callerId;
        if ((await access.RequireAnyViewAsync(ownerId, cancellationToken)).Refusal(ApiErrors.OwnerNotFound) is { } refused)
        {
            return refused;
        }

        var visible = await db.Categories
            .AsNoTracking()
            .Where(c => c.OwnerId == null || c.OwnerId == ownerId)
            .ToListAsync(cancellationToken);

        // Gruppiert: die Gruppen in fester Reihenfolge, darin nach Namen sortiert.
        var sorted = visible
            .Select(ToResponse)
            .OrderBy(c => CategoryGroupKeys.All.ToList().IndexOf(c.Group))
            .ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return Results.Ok(sorted);
    }

    private static async Task<IResult> CreateAsync(
        SaveCategoryRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> users,
        DuofyDbContext db,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (users.GetUserId(principal) is not { } id)
        {
            return Results.Unauthorized();
        }
        var ownerId = Guid.Parse(id);

        var checkedInput = await CheckAsync(request, ownerId, existingId: null, db, cancellationToken);
        if (checkedInput.Error is { } error)
        {
            return ApiErrors.BadRequest(error);
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = checkedInput.Name,
            Group = checkedInput.Group,
            SuggestedPot = checkedInput.Pot,
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        loggers.CreateLogger("Duofy.Api.Categories")
            .LogInformation("Kategorie {CategoryId} von {UserId} angelegt.", category.Id, ownerId);
        return Results.Created($"/api/v1/categories/{category.Id}", ToResponse(category));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        SaveCategoryRequest request,
        IAccess access,
        DuofyDbContext db,
        CancellationToken cancellationToken)
    {
        if (access.CallerId is null)
        {
            return Results.Unauthorized();
        }

        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.OwnerId != null, cancellationToken);
        if (category is null || access.RequireSelf(category.OwnerId!.Value).Refusal(() => Results.NotFound()) is not null)
        {
            return Results.NotFound();
        }
        var ownerId = category.OwnerId.Value;

        var checkedInput = await CheckAsync(request, ownerId, existingId: id, db, cancellationToken);
        if (checkedInput.Error is { } error)
        {
            return ApiErrors.BadRequest(error);
        }

        category.Name = checkedInput.Name;
        category.Group = checkedInput.Group;
        category.SuggestedPot = checkedInput.Pot;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToResponse(category));
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        IAccess access,
        DuofyDbContext db,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (access.CallerId is null)
        {
            return Results.Unauthorized();
        }

        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.OwnerId != null, cancellationToken);
        if (category is null || access.RequireSelf(category.OwnerId!.Value).Refusal(() => Results.NotFound()) is not null)
        {
            return Results.NotFound();
        }
        var ownerId = category.OwnerId.Value;

        // Eine Kategorie, die eine Buchungszeile oder ein Vertrag benutzt, bleibt (R4). Die Datenbank
        // würde das Löschen auch ablehnen; so bekommt die Person einen verständlichen Grund.
        if (await db.Set<BookingLine>().AnyAsync(l => l.CategoryId == id, cancellationToken)
            || await db.Commitments.AnyAsync(c => c.CategoryId == id, cancellationToken))
        {
            return ApiErrors.BadRequest(ErrorCodes.CategoryInUse);
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);

        loggers.CreateLogger("Duofy.Api.Categories")
            .LogInformation("Kategorie {CategoryId} von {UserId} gelöscht.", id, ownerId);
        return Results.NoContent();
    }

    /// <summary>Das Ergebnis der Prüfung: entweder ein Fehlercode oder die geprüften Werte.</summary>
    private readonly record struct Checked(string? Error, string Name, CategoryGroup Group, Pot Pot);

    /// <summary>
    /// Prüft Name, Gruppe und Topf einer eigenen Kategorie (R4). Der Name darf unter den
    /// eingebauten und den eigenen Namen der Person noch nicht vorkommen, ohne Rücksicht auf
    /// Groß- und Kleinschreibung; die Kategorie selbst (<paramref name="existingId"/>) zählt dabei nicht mit.
    /// </summary>
    private static async Task<Checked> CheckAsync(
        SaveCategoryRequest request, Guid ownerId, Guid? existingId, DuofyDbContext db, CancellationToken cancellationToken)
    {
        static Checked Fail(string code) => new(code, "", default, default);

        var name = (request.Name ?? "").Trim();
        if (name.Length == 0)
        {
            return Fail(ErrorCodes.CategoryNameRequired);
        }
        if (name.Length > CategoryRules.NameMaxLength)
        {
            return Fail(ErrorCodes.CategoryNameTooLong);
        }
        if (!CategoryGroups.TryParse(request.Group, out var group))
        {
            return Fail(ErrorCodes.CategoryGroupInvalid);
        }
        if (!PotKey.TryParse(request.Pot, out var pot)
            // Der Topf Einnahmen gehört zur Gruppe Einnahmen und nur dazu.
            || (pot == Pot.Income) != (group == CategoryGroup.Income))
        {
            return Fail(ErrorCodes.CategoryPotInvalid);
        }

        var lower = name.ToLowerInvariant();
        var takenByBuiltIn = CategoryCatalog.All.Any(c => CategoryCatalog.GermanName(c.Key).ToLowerInvariant() == lower);
        var takenByOwn = await db.Categories.AnyAsync(
            c => c.OwnerId == ownerId && c.Id != existingId && c.Name!.ToLower() == lower, cancellationToken);
        if (takenByBuiltIn || takenByOwn)
        {
            return Fail(ErrorCodes.CategoryNameTaken);
        }

        return new Checked(null, name, group, pot);
    }
}
