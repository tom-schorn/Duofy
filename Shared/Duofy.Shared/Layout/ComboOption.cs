namespace Duofy.Shared.Layout;

/// <summary>
/// Eine Option der Auswahl mit Suchfeld (<see cref="ComboBox"/>).
/// </summary>
/// <param name="Value">Der Wert, der gewählt wird; "" ist die leere Wahl.</param>
/// <param name="Label">Der Name, der im Feld steht und nach dem gesucht wird.</param>
/// <param name="Group">Die Überschrift, unter der die Option steht; null für keine Gruppe.</param>
/// <param name="Detail">Eine zweite Angabe neben dem Namen (zum Beispiel Fälligkeit und Betrag eines Postens); sie wird auch durchsucht.</param>
/// <param name="Icon">Der Name eines Kontosymbols (<c>AccountIcons</c>); null für keins.</param>
public sealed record ComboOption(string Value, string Label, string? Group = null, string? Detail = null, string? Icon = null);
