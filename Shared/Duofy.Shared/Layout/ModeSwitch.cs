namespace Duofy.Shared.Layout;

/// <summary>
/// Der Schalter für hell und dunkel, den das App-Layout den Seiten reicht (als CascadingValue).
/// Die Seite "Mehr" zeigt ihn an, weil die obere Leiste in der App keinen Schalter mehr hat.
/// </summary>
public sealed record ModeSwitch(bool IsDark, Func<Task> Toggle);
