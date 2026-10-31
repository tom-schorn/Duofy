using Microsoft.AspNetCore.Components;

namespace Duofy.Shared.Blocks;

/// <summary>
/// Eine Zeile der Linientabelle. Bezeichnung und Betrag genügen; <paramref name="Detail"/> steht klein unter der
/// Bezeichnung, <paramref name="Leading"/> davor (zum Beispiel ein Haken) und <paramref name="Trailing"/> dahinter
/// (zum Beispiel Knöpfe). Ohne Betrag bleibt die Betragsspalte der Zeile leer.
/// </summary>
public sealed record LineTableRow(
    string Label,
    decimal? Amount,
    string? Detail = null,
    RenderFragment? Leading = null,
    RenderFragment? Trailing = null,
    string? TestId = null);
