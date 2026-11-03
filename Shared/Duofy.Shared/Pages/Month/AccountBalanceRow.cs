namespace Duofy.Shared.Pages.Month;

/// <summary>Ein Konto in der Karte Kontostände: Name, Art (null, wenn unbekannt), Stand, ob es als verfügbar zählt, und im Haushalt wessen.</summary>
public sealed record AccountBalanceRow(Guid Id, string Name, string? Type, decimal Balance, bool Available, string? Owner = null);
