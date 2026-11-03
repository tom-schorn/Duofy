namespace Duofy.Shared.Pages.Month;

/// <summary>Eine Zeile des Druckbilds: ein Posten mit Fälligkeitstag, geplant, gebucht, ob abgehakt und, im Haushalt, wessen.</summary>
public sealed record PrintRow(string Label, int DueDay, decimal Planned, decimal Booked, bool Done, string? Owner = null);

/// <summary>Ein Topf im Druckbild: was geplant ist, was die Quote erlaubt (ohne bei Einnahmen) und seine Posten.</summary>
public sealed record PrintPot(string Pot, decimal Planned, decimal? Limit, IReadOnlyList<PrintRow> Rows);
