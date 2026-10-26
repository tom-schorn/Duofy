namespace Duofy.Domain;

/// <summary>
/// Die IBAN eines Kontos (ISO 13616): zwei Buchstaben für das Land, zwei Prüfziffern und
/// bis zu 30 Buchstaben oder Ziffern, zusammen höchstens 34 Zeichen.
///
/// Gespeichert wird sie ohne Leerzeichen und in Großbuchstaben, so wie sie auch auf einem
/// Kontoauszug in maschinenlesbarer Form steht. Der Import soll eigene Umbuchungen später an
/// der IBAN erkennen; ein Tippfehler soll deshalb sofort auffallen.
/// </summary>
public static class Iban
{
    /// <summary>Die größte erlaubte Länge.</summary>
    public const int MaxLength = 34;

    // Die kürzesten IBANs (Norwegen) haben 15 Zeichen.
    private const int MinLength = 15;

    /// <summary>True, wenn kein IBAN eingegeben wurde: nichts, leer oder nur Leerzeichen.</summary>
    public static bool IsEmpty(string? input) => string.IsNullOrWhiteSpace(input);

    /// <summary>
    /// Entfernt Leerzeichen, schreibt Buchstaben groß und prüft Aufbau und Prüfziffern.
    /// Liefert false, wenn die Eingabe keine gültige IBAN ist.
    ///
    /// Beispiel: "de89 3704 0044 0532 0130 00" wird zu "DE89370400440532013000".
    /// </summary>
    public static bool TryNormalize(string? input, out string iban)
    {
        iban = new string((input ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

        var valid = iban.Length is >= MinLength and <= MaxLength
            && iban.All(char.IsAsciiLetterOrDigit)
            && char.IsAsciiLetter(iban[0]) && char.IsAsciiLetter(iban[1])
            && char.IsAsciiDigit(iban[2]) && char.IsAsciiDigit(iban[3])
            && Mod97(iban) == 1;
        if (!valid)
        {
            iban = "";
        }
        return valid;
    }

    /// <summary>
    /// Das Prüfverfahren der IBAN: die ersten vier Zeichen ans Ende stellen, jeden Buchstaben
    /// durch eine Zahl ersetzen (A = 10 bis Z = 35) und die entstehende lange Zahl durch 97
    /// teilen. Bei einer gültigen IBAN bleibt der Rest 1.
    ///
    /// Die Zahl ist zu lang für jeden Zahlentyp; deshalb wird der Rest Ziffer für Ziffer
    /// weitergerechnet, wie beim schriftlichen Teilen.
    /// </summary>
    private static int Mod97(string iban)
    {
        var rearranged = iban[4..] + iban[..4];
        var remainder = 0;
        foreach (var c in rearranged)
        {
            var value = char.IsAsciiDigit(c) ? c - '0' : c - 'A' + 10;
            // Ein Buchstabe ergibt zwei Ziffern, deshalb mal 100 statt mal 10.
            remainder = (remainder * (value < 10 ? 10 : 100) + value) % 97;
        }
        return remainder;
    }
}
