using Duofy.Shared.Services;

namespace Duofy.Shared.Layout;

/// <summary>
/// Die Auswahl für Hell, Dunkel und "wie das Gerät", die das App-Layout den Seiten reicht (als CascadingValue).
/// Die Seite "Mehr" zeigt sie an, weil die obere Leiste in der App keine Auswahl mehr hat.
/// </summary>
public sealed record ModeSwitch(ThemeChoice Choice, Func<ThemeChoice, Task> Select);
