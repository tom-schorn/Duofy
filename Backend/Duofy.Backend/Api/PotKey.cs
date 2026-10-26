using Duofy.Domain;

namespace Duofy.Api;

/// <summary>
/// Übersetzt einen <see cref="Pot"/> in seinen Schlüssel in der API ("needs") und zurück.
/// Es gilt genau die kleingeschriebene Schreibweise, "Needs" oder "1" sind keine Töpfe.
/// </summary>
internal static class PotKey
{
    public static string ToKey(Pot pot) => pot.ToString().ToLowerInvariant();

    public static bool TryParse(string? key, out Pot pot)
    {
        foreach (var candidate in Enum.GetValues<Pot>())
        {
            if (ToKey(candidate) == key)
            {
                pot = candidate;
                return true;
            }
        }
        pot = default;
        return false;
    }
}
