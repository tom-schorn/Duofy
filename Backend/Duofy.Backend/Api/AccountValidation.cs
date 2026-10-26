using System.Net.Mail;
using Duofy.Schemas;

namespace Duofy.Api;

/// <summary>
/// Prüft Namen, E-Mail und Passwort, bevor sie an Identity gehen. Jede Methode
/// liefert den ersten passenden Fehlercode oder null, wenn alles stimmt.
/// </summary>
internal static class AccountValidation
{
    // So lang darf eine Adresse in der Tabelle der Benutzer höchstens sein.
    private const int EmailMaxLength = 256;

    public static string? CheckNames(string firstName, string lastName)
    {
        if (firstName.Length == 0)
        {
            return ErrorCodes.FirstNameRequired;
        }
        if (firstName.Length > AccountRules.NameMaxLength)
        {
            return ErrorCodes.FirstNameTooLong;
        }
        if (lastName.Length == 0)
        {
            return ErrorCodes.LastNameRequired;
        }
        if (lastName.Length > AccountRules.NameMaxLength)
        {
            return ErrorCodes.LastNameTooLong;
        }
        return null;
    }

    public static string? CheckEmail(string email)
    {
        // MailAddress versteht auch "Anna <anna@example.org>"; erlaubt ist hier
        // aber nur die nackte Adresse.
        var valid = email.Length <= EmailMaxLength
            && MailAddress.TryCreate(email, out var parsed)
            && parsed.Address == email;
        return valid ? null : ErrorCodes.EmailInvalid;
    }

    public static string? CheckPassword(string? password) =>
        password is null || password.Length < AccountRules.PasswordMinLength ? ErrorCodes.PasswordTooShort : null;

    /// <summary>Kürzt Leerzeichen am Anfang und Ende; fehlt der Wert ganz, wird er leer.</summary>
    public static string Clean(string? value) => value?.Trim() ?? "";
}
