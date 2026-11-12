using Duofy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duofy.Data;

/// <summary>
/// Wessen Daten die laufende Anfrage sehen und schreiben darf (Spec 56). Darauf beruht das Netz unter der
/// zentralen Prüfung der API: die Filter in <see cref="DuofyDbContext"/> und die Prüfung beim Speichern
/// (<see cref="OwnerSaveCheck"/>). Bereiche und Stufen entscheidet weiter die zentrale Prüfung; hier zählt nur,
/// ob jemand überhaupt etwas mit der Person zu tun hat.
/// </summary>
public interface IOwnerContext
{
    /// <summary>Die angemeldete Person; null ohne Anmeldung.</summary>
    Guid? CurrentUserId { get; }

    /// <summary>
    /// Die angemeldete Person und jedes Mitglied desselben Haushalts, das ihr in irgendeinem Bereich mindestens
    /// Ansehen freigegeben hat. Ohne Anmeldung leer.
    /// </summary>
    IReadOnlyCollection<Guid> VisibleOwners { get; }

    /// <summary>Die angemeldete Person und jedes Mitglied, das ihr in irgendeinem Bereich mindestens Anlegen freigegeben hat.</summary>
    IReadOnlyCollection<Guid> WritableOwners { get; }

    /// <summary>True, solange ein Bereich aus <see cref="Bypass"/> offen ist: Filter und Prüfung beim Speichern sind dann aus.</summary>
    bool IsBypassed { get; }

    /// <summary>
    /// Schaltet Filter und Prüfung beim Speichern aus, bis der zurückgegebene Bereich endet
    /// (<c>using (owners.Bypass("…"))</c>). Nur an den genehmigten Stellen (R4); ein Test zählt jede.
    /// </summary>
    IDisposable Bypass(string reason);
}

/// <summary>
/// Die Umsetzung von <see cref="IOwnerContext"/>, einmal je Anfrage. Die API füllt sie mit
/// <see cref="LoadAsync"/>, bevor ein Endpunkt läuft; ohne Anmeldung bleibt sie leer.
/// </summary>
public sealed class OwnerContext(ILogger<OwnerContext> logger) : IOwnerContext
{
    private int bypasses;

    // Die Freigaben an die angemeldete Person, die gerade zählen: je Geber und Bereich die Stufe.
    private Dictionary<(Guid Granter, Area Area), AccessLevel> grants = [];

    public Guid? CurrentUserId { get; private set; }

    public IReadOnlyCollection<Guid> VisibleOwners { get; private set; } = [];

    public IReadOnlyCollection<Guid> WritableOwners { get; private set; } = [];

    public bool IsBypassed => bypasses > 0;

    /// <summary>
    /// Füllt die Besitzer für <paramref name="userId"/> mit einer Abfrage der Freigaben. Eine Freigabe zählt nur,
    /// solange beide Personen im selben Haushalt sind; diese Regel steht nur hier. Die zentrale Prüfung der API
    /// liest ihre Stufen mit <see cref="LevelFrom"/> aus demselben Ergebnis (Spec 56, A10).
    /// </summary>
    public async Task LoadAsync(DuofyDbContext db, Guid userId, CancellationToken cancellationToken)
    {
        var granted = await db.Grants
            .AsNoTracking()
            .Where(g => g.GranteeId == userId)
            .Where(g => db.HouseholdMembers.Any(owner => owner.UserId == g.GranterId
                && db.HouseholdMembers.Any(caller => caller.UserId == userId && caller.HouseholdId == owner.HouseholdId)))
            .Select(g => new { g.GranterId, g.Area, g.Level })
            .ToListAsync(cancellationToken);

        // Jede Zeile heißt mindestens Ansehen; "keine Zeile" ist "Nichts".
        CurrentUserId = userId;
        grants = granted.ToDictionary(g => (g.GranterId, g.Area), g => g.Level);
        VisibleOwners = granted.Select(g => g.GranterId).Append(userId).ToHashSet();
        WritableOwners = granted.Where(g => g.Level.Includes(AccessLevel.Create)).Select(g => g.GranterId).Append(userId).ToHashSet();
    }

    /// <summary>
    /// Die Stufe, die <paramref name="granterId"/> der angemeldeten Person in diesem Bereich freigegeben hat, aus dem
    /// letzten <see cref="LoadAsync"/>; ohne Zeile oder außerhalb des Haushalts <see cref="AccessLevel.None"/>.
    /// </summary>
    public AccessLevel LevelFrom(Guid granterId, Area area) => grants.GetValueOrDefault((granterId, area), AccessLevel.None);

    /// <summary>True, wenn <paramref name="granterId"/> der angemeldeten Person für diesen Bereich eine Stufe gesetzt hat (eine Zeile gibt es nur ab Ansehen).</summary>
    public bool HasRow(Guid granterId, Area area) => grants.ContainsKey((granterId, area));

    /// <summary>True, wenn <paramref name="granterId"/> der angemeldeten Person in irgendeinem anderen Bereich als <paramref name="except"/> mindestens Ansehen freigegeben hat.</summary>
    public bool ViewsAnythingExcept(Guid granterId, Area except) =>
        Enum.GetValues<Area>().Any(area => area != except && LevelFrom(granterId, area).Includes(AccessLevel.View));

    public IDisposable Bypass(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        bypasses++;
        logger.LogDebug("Besitzer-Filter aus: {Reason}", reason);
        return new Scope(this, reason);
    }

    private void Close(string reason)
    {
        bypasses--;
        logger.LogDebug("Besitzer-Filter wieder an: {Reason}", reason);
    }

    /// <summary>Ein offener Bereich ohne Filter; schließt sich genau einmal.</summary>
    private sealed class Scope(OwnerContext owners, string reason) : IDisposable
    {
        private bool closed;

        public void Dispose()
        {
            if (closed)
            {
                return;
            }
            closed = true;
            owners.Close(reason);
        }
    }
}
