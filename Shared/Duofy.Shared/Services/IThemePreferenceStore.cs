namespace Duofy.Shared.Services;

/// <summary>
/// Merkt sich pro Gerät, ob die Person Hell, Dunkel oder "wie das Gerät" gewählt hat.
/// Im Browser liegt die Wahl im localStorage, in der App in den Preferences.
/// Hat die Person noch nie gewählt, gibt es <see cref="ThemeChoice.Device"/>.
/// Früher gespeicherte Wahlen (hell oder dunkel, als Wahrheitswert) bleiben gültig.
/// </summary>
public interface IThemePreferenceStore
{
    Task<ThemeChoice> LoadAsync();

    Task SaveAsync(ThemeChoice choice);
}
