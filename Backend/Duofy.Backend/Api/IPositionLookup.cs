using Duofy.Data;
using Duofy.Domain;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api;

/// <summary>Was die Regeln für Buchungen von einem Posten wissen müssen.</summary>
/// <param name="Id">Die Kennung des Postens.</param>
/// <param name="PlanYear">Das Jahr seines Plans.</param>
/// <param name="PlanMonth">Der Monat seines Plans.</param>
/// <param name="Pot">Sein Topf; er entscheidet, welche Zeilen ihn füllen dürfen.</param>
/// <param name="CounterAccountId">Sein Zielkonto; mit einem Zielkonto darf auch eine Umbuchung ihn füllen.</param>
public sealed record PositionInfo(Guid Id, int PlanYear, int PlanMonth, Pot Pot, Guid? CounterAccountId);

/// <summary>Sucht einen Posten in den Plänen einer Person.</summary>
public interface IPositionLookup
{
    /// <summary>
    /// Liefert den Posten, wenn er in einem Plan der Person steht. Ein Posten einer anderen Person
    /// und eine unbekannte Kennung sehen für den Aufrufer gleich aus: null.
    /// </summary>
    Task<PositionInfo?> FindAsync(Guid ownerId, Guid positionId, CancellationToken cancellationToken);
}

/// <summary>Für Prüfungen ohne Pläne: Es gibt keinen Posten.</summary>
internal sealed class NoPositions : IPositionLookup
{
    public static NoPositions Instance { get; } = new();

    public Task<PositionInfo?> FindAsync(Guid ownerId, Guid positionId, CancellationToken cancellationToken) =>
        Task.FromResult<PositionInfo?>(null);
}

/// <summary>Liest die Posten aus der Datenbank, über ihren Plan auf die Person eingeschränkt.</summary>
internal sealed class DbPositionLookup(DuofyDbContext db) : IPositionLookup
{
    public Task<PositionInfo?> FindAsync(Guid ownerId, Guid positionId, CancellationToken cancellationToken) =>
        db.Plans
            .AsNoTracking()
            .Where(p => p.OwnerId == ownerId)
            .SelectMany(p => p.Positions.Select(position => new { Plan = p, Position = position }))
            .Where(x => x.Position.Id == positionId)
            .Select(x => new PositionInfo(x.Position.Id, x.Plan.Year, x.Plan.Month, x.Position.Pot, x.Position.CounterAccountId))
            .FirstOrDefaultAsync(cancellationToken);
}
