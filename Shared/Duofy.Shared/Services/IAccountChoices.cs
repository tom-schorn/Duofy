using Duofy.ApiClient;

namespace Duofy.Shared.Services;

/// <summary>Ein Konto, so wie die Buchungsseite es zur Auswahl braucht.</summary>
/// <param name="Id">Die Kennung des Kontos.</param>
/// <param name="Name">Der Name, unter dem das Konto angezeigt wird.</param>
/// <param name="IsDefault">True beim Standardkonto: Es ist beim Buchen vorgewählt.</param>
/// <param name="Active">False bei einem aufgelösten Konto: Es erscheint im Filter, aber nicht bei neuen Buchungen.</param>
public sealed record AccountChoice(Guid Id, string Name, bool IsDefault, bool Active);

/// <summary>
/// Liefert der Buchungsseite die Konten der angemeldeten Person.
///
/// Die Buchungsseite braucht von einem Konto nur Kennung, Name, Standard und offen; diese kleine
/// Schnittstelle hält sie frei von allem anderen, was ein Konto hat. Im Betrieb steht
/// <see cref="ApiAccountChoices"/> dahinter, in den Seitentests ein Ersatz.
/// </summary>
public interface IAccountChoices
{
    /// <summary>
    /// Alle Konten der Person, aktive und aufgelöste. Scheitert der Aufruf, steht der Fehlercode
    /// im Ergebnis, damit die Seite den richtigen Hinweis zeigt statt "kein Konto". Mit <paramref name="owner"/>
    /// die Konten dieses Mitglieds (Spec 68).
    /// </summary>
    Task<ApiResult<IReadOnlyList<AccountChoice>>> ListAsync(Guid? owner = null, CancellationToken cancellationToken = default);
}

/// <summary>Die Konten aus dem Konten-Client der API.</summary>
public sealed class ApiAccountChoices(IAccountApi accounts) : IAccountChoices
{
    public async Task<ApiResult<IReadOnlyList<AccountChoice>>> ListAsync(Guid? owner = null, CancellationToken cancellationToken = default)
    {
        var result = await accounts.ListAsync(activeOnly: false, owner, cancellationToken);
        if (!result.Succeeded)
        {
            return ApiResult<IReadOnlyList<AccountChoice>>.Failure(result.ErrorCode!);
        }
        return ApiResult<IReadOnlyList<AccountChoice>>.Success(
            result.Value!.Select(a => new AccountChoice(a.Id, a.Name, a.IsDefault, a.Active)).ToList());
    }
}
