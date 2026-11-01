namespace Duofy.Shared.Services;

/// <summary>Was vor dem ersten Zeichnen schon feststeht: die Wahl der Person und ob das Gerät dunkel eingestellt ist.</summary>
public sealed record ThemeStart(ThemeChoice Choice, bool SystemIsDark);

/// <summary>
/// Merkt sich pro Gerät, ob die Person Hell, Dunkel oder "wie das Gerät" gewählt hat.
/// Im Browser liegt die Wahl im localStorage, in der App in den Preferences.
/// Hat die Person noch nie gewählt, gibt es <see cref="ThemeChoice.Device"/>.
/// Früher gespeicherte Wahlen (hell oder dunkel, als Wahrheitswert) bleiben gültig.
/// </summary>
public interface IThemePreferenceStore
{
    Task<ThemeChoice> LoadAsync();

    /// <summary>
    /// Die Wahl und die Einstellung des Geräts, wenn sie sofort und ohne Warten bekannt sind (in der App: Preferences und
    /// Systemdarstellung). Das Layout wendet sie noch vor dem ersten Zeichnen an (Issue 5). Im Browser sind sie nicht sofort
    /// bekannt (der localStorage braucht JavaScript, Vorrendern auf dem Server hat keinen Browser); dort setzt ein kleines
    /// Skript im Kopf der Seite (<see cref="Duofy.Shared.Theme.ThemeBoot"/>) die Farbe vor dem ersten Zeichnen, und es gibt null.
    /// </summary>
    ThemeStart? LoadImmediately() => null;

    Task SaveAsync(ThemeChoice choice);
}
