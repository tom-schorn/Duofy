namespace Duofy.Api;

/// <summary>
/// "Heute" ist für Duofy der Tag in Berlin, nicht der Tag auf der Uhr des Servers (die läuft
/// meist in UTC). Kurz nach Mitternacht ist es in Berlin schon morgen, in UTC noch heute.
/// </summary>
internal static class Berlin
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    /// <summary>Der heutige Tag in Berlin nach der übergebenen Uhr; Tests geben eine feste Uhr vor.</summary>
    public static DateOnly Today(TimeProvider time) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), Zone).DateTime);

    /// <summary>Der Tag in Berlin, an dem dieser Zeitpunkt liegt.</summary>
    public static DateOnly DateOf(DateTimeOffset moment) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, Zone).DateTime);
}
