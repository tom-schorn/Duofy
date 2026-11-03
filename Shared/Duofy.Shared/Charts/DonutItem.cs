namespace Duofy.Shared.Charts;

/// <summary>Ein Eintrag des runden Diagramms: Name, Farbton (ein Topf oder c1 bis c6), geplant und gebucht.</summary>
/// <param name="Target">Bei einem Topf: die Zielquote in ganzen Prozent; dann zeigt die Legende Ziel und Ist.</param>
/// <param name="Actual">Bei einem Topf: das Ist der Quote in Prozent.</param>
public sealed record DonutItem(string Label, string Tone, decimal Planned, decimal Booked, int? Target = null, decimal? Actual = null);
