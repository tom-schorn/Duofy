namespace Duofy.Shared.Services;

/// <summary>
/// Merkt sich pro Gerät, ob die Person einem optionalen Fremddienst zugestimmt hat (Issue #35).
/// Im Browser liegt die Wahl im localStorage. Es gibt drei Zustände: zugestimmt (true), abgelehnt (false)
/// und noch nicht gewählt (null). Ohne Wahl gilt "nicht einverstanden": Es wird nichts geladen.
/// </summary>
public interface IConsentStore
{
    Task<bool?> LoadAsync(ConsentCategory category);

    Task SaveAsync(ConsentCategory category, bool granted);
}
