namespace Duofy.Shared.Services;

/// <summary>
/// Merkt sich pro Gerät, ob die Person den dunklen oder den hellen Modus gewählt hat.
/// Im Browser liegt die Wahl im localStorage, in der App in den Preferences.
/// Hat die Person noch nie gewählt, gibt es <c>null</c> und das System entscheidet.
/// </summary>
public interface IThemePreferenceStore
{
    Task<bool?> LoadIsDarkAsync();

    Task SaveIsDarkAsync(bool isDark);
}
