using Duofy.Schemas;
using Duofy.Data;
using Duofy.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api;

/// <summary>
/// Die zentrale Prüfung der Freigaben (Spec 68, R5): "Darf die angemeldete Person mit den Daten dieser Person
/// in diesem Bereich auf dieser Stufe arbeiten?" Jeder Endpunkt der Konten, Buchungen, Verträge, Pläne und
/// Kategorien fragt hier; kein Endpunkt vergleicht Besitzer selbst. Er bestimmt nur den Besitzer (aus
/// <c>owner</c> in der Anfrage oder aus dem Datensatz) und fragt dann.
/// </summary>
public interface IAccess
{
    /// <summary>Die angemeldete Person aus dem Token; null ohne gültige Anmeldung.</summary>
    Guid? CallerId { get; }

    /// <summary>
    /// Was die angemeldete Person mit den Daten von <paramref name="ownerId"/> in diesem Bereich darf. Die
    /// eigenen Daten: <see cref="AccessLevel.Delete"/>. Eine andere Person: die gespeicherte Stufe, aber nur,
    /// solange beide im selben Haushalt sind; sonst <see cref="AccessLevel.None"/>.
    /// </summary>
    Task<AccessLevel> LevelAsync(Guid ownerId, Area area, CancellationToken cancellationToken);

    /// <summary>
    /// Prüft, ob die Stufe für <paramref name="needed"/> reicht. Unter Ansehen ist der Datensatz unsichtbar
    /// (<see cref="AccessOutcome.Hidden"/>, der Endpunkt antwortet 404 wie bisher); mit Ansehen, aber ohne
    /// den nötigen Schritt, ist die Antwort 403 mit dem Code dieses Schritts.
    /// </summary>
    Task<AccessCheck> RequireAsync(Guid ownerId, Area area, AccessLevel needed, CancellationToken cancellationToken);

    /// <summary>
    /// Für das, was zu keinem Bereich gehört, wie die eigenen Kategorien: Ansehen, wenn die Person in
    /// irgendeinem Bereich mindestens Ansehen freigegeben hat, sonst unsichtbar.
    /// </summary>
    Task<AccessCheck> RequireAnyViewAsync(Guid ownerId, CancellationToken cancellationToken);

    /// <summary>
    /// Was nur die Person selbst ändern darf, gleich was sie freigegeben hat (die eigenen Kategorien): für
    /// jemand anderen unsichtbar.
    /// </summary>
    AccessCheck RequireSelf(Guid ownerId);
}

/// <summary>Das Ergebnis der Prüfung.</summary>
public enum AccessOutcome
{
    /// <summary>Die Stufe reicht.</summary>
    Allowed,

    /// <summary>Nicht einmal Ansehen: Für die angemeldete Person gibt es die Daten nicht.</summary>
    Hidden,

    /// <summary>Ansehen ja, der nötige Schritt nicht.</summary>
    Refused,
}

/// <summary>Das Ergebnis der Prüfung, bei <see cref="AccessOutcome.Refused"/> mit dem Code des fehlenden Schritts.</summary>
public readonly record struct AccessCheck(AccessOutcome Outcome, string? Code = null)
{
    public static AccessCheck Allowed { get; } = new(AccessOutcome.Allowed);

    public static AccessCheck Hidden { get; } = new(AccessOutcome.Hidden);

    /// <summary>Null, wenn die Stufe reicht; sonst die Antwort: <paramref name="notFound"/> oder 403 mit dem Code.</summary>
    public IResult? Refusal(Func<IResult> notFound) => Outcome switch
    {
        AccessOutcome.Allowed => null,
        AccessOutcome.Hidden => notFound(),
        _ => Results.Json(new ErrorResponse(Code!), statusCode: StatusCodes.Status403Forbidden),
    };

    /// <summary>Der Code, wenn dieser Schritt fehlt.</summary>
    public static string CodeFor(AccessLevel needed) => needed switch
    {
        AccessLevel.Create => ErrorCodes.NoCreateGranted,
        AccessLevel.Edit => ErrorCodes.NoEditGranted,
        _ => ErrorCodes.NoDeleteGranted,
    };
}

/// <summary>
/// Liest die Stufen aus den Freigaben, die <see cref="OwnerContext"/> einmal je Anfrage geladen hat (Spec 56, A10):
/// Der Besitzer-Filter der Datenbank (<see cref="OwnerFilter"/>) und diese Prüfung nutzen dasselbe Ergebnis, und die
/// Regel "nur im selben Haushalt" steht nur dort. Der Filter kennt keine Bereiche und Stufen, nur die Besitzer, die
/// mindestens Ansehen oder Anlegen freigegeben haben, und fängt ab, was ein Endpunkt hier zu fragen vergisst.
/// </summary>
internal sealed class DbAccess(DuofyDbContext db, OwnerContext owners, IHttpContextAccessor http, UserManager<AppUser> users) : IAccess
{

    public Guid? CallerId =>
        http.HttpContext is { } context && users.GetUserId(context.User) is { } id ? Guid.Parse(id) : null;

    public async Task<AccessLevel> LevelAsync(Guid ownerId, Area area, CancellationToken cancellationToken)
    {
        if (CallerId is not { } callerId)
        {
            return AccessLevel.None;
        }
        if (ownerId == callerId)
        {
            return AccessLevel.Delete;
        }
        // Gewöhnlich hat der Besitzer-Filter die Freigaben am Anfang der Anfrage schon geladen; sonst hier, einmal.
        if (owners.CurrentUserId != callerId)
        {
            await owners.LoadAsync(db, callerId, cancellationToken);
        }
        return owners.LevelFrom(ownerId, area);
    }

    public async Task<AccessCheck> RequireAsync(Guid ownerId, Area area, AccessLevel needed, CancellationToken cancellationToken)
    {
        var level = await LevelAsync(ownerId, area, cancellationToken);
        if (level.Includes(needed))
        {
            return AccessCheck.Allowed;
        }
        return level.Includes(AccessLevel.View) ? new AccessCheck(AccessOutcome.Refused, AccessCheck.CodeFor(needed)) : AccessCheck.Hidden;
    }

    public async Task<AccessCheck> RequireAnyViewAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        foreach (var area in Enum.GetValues<Area>())
        {
            if ((await LevelAsync(ownerId, area, cancellationToken)).Includes(AccessLevel.View))
            {
                return AccessCheck.Allowed;
            }
        }
        return AccessCheck.Hidden;
    }

    public AccessCheck RequireSelf(Guid ownerId) => CallerId == ownerId ? AccessCheck.Allowed : AccessCheck.Hidden;
}

/// <summary>Übersetzt Bereiche und Stufen in ihre Schlüssel in der API und zurück.</summary>
internal static class GrantKeys
{
    public static string ToKey(Area area) => area.ToString().ToLowerInvariant();

    public static string ToKey(AccessLevel level) => level.ToString().ToLowerInvariant();

    /// <summary>Genau die kleingeschriebenen Schlüssel aus <see cref="AccessLevelKeys"/>.</summary>
    public static bool TryParse(string? key, out AccessLevel level)
    {
        foreach (var candidate in Enum.GetValues<AccessLevel>())
        {
            if (ToKey(candidate) == key)
            {
                level = candidate;
                return true;
            }
        }
        level = default;
        return false;
    }

    public static GrantLevels ToResponse(IReadOnlyDictionary<Area, AccessLevel> levels) => new(
        ToKey(levels[Area.Plan]), ToKey(levels[Area.Book]), ToKey(levels[Area.Accounts]), ToKey(levels[Area.Commitments]), ToKey(levels[Area.Savings]));
}
