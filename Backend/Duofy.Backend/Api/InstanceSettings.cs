using System.Security.Cryptography;
using System.Text;
using Duofy.Schemas;
using Duofy.Data;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api;

/// <summary>
/// Wer auf diesen Server darf (Spec 70, R1 bis R3): der Einrichtungscode aus Duofy:SetupCode (Umgebungsvariable
/// Duofy__SetupCode) und der Registrierungsmodus aus Duofy:RegistrationMode (Duofy__RegistrationMode).
///
/// Beides wird bei jeder Frage frisch gelesen, wie die öffentliche Adresse in <see cref="PublicLinks"/>. Ein leerer
/// Code heißt: Es gibt keinen; ein Code unter 16 Zeichen gilt genauso (A17), denn er ließe sich erraten. Ein fehlender oder unbekannter Modus gilt als "invite", denn ein Familienserver soll
/// nicht aus Versehen für alle offen stehen.
/// </summary>
internal sealed class InstanceSettings(IConfiguration configuration, ILoggerFactory loggers)
{
    public const string LogCategory = "Duofy.Api.Instance";

    public const string SetupCodeKey = "Duofy:SetupCode";

    public const string RegistrationModeKey = "Duofy:RegistrationMode";

    /// <summary>So viele Zeichen braucht ein Einrichtungscode mindestens; ein kürzerer gilt als nicht gesetzt.</summary>
    public const int SetupCodeMinLength = 16;

    private readonly ILogger logger = loggers.CreateLogger(LogCategory);

    // Ein unbekannter Modus wird einmal gemeldet, nicht bei jeder Anfrage.
    private int unknownModeLogged;

    /// <summary>Der Registrierungsmodus, einer aus <see cref="RegistrationModes"/>.</summary>
    public string RegistrationMode
    {
        get
        {
            var value = configuration[RegistrationModeKey]?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(value))
            {
                return RegistrationModes.Invite;
            }
            if (RegistrationModes.All.Contains(value))
            {
                return value;
            }
            if (Interlocked.Exchange(ref unknownModeLogged, 1) == 0)
            {
                logger.LogWarning("{Setting} ist weder open noch invite noch closed; es gilt invite.", RegistrationModeKey);
            }
            return RegistrationModes.Invite;
        }
    }

    /// <summary>True, wenn ein Einrichtungscode eingestellt ist.</summary>
    public bool HasSetupCode => SetupCode() is not null;

    /// <summary>
    /// True, wenn <paramref name="candidate"/> der eingestellte Code ist. Der Vergleich dauert immer gleich lange,
    /// auch bei unterschiedlicher Länge: Verglichen werden die SHA-256-Werte beider Texte. Ohne eingestellten Code
    /// passt kein Code.
    /// </summary>
    public bool IsSetupCode(string? candidate)
    {
        var code = SetupCode();
        var given = candidate?.Trim();
        if (code is null || string.IsNullOrEmpty(given))
        {
            return false;
        }
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(code)),
            SHA256.HashData(Encoding.UTF8.GetBytes(given)));
    }

    /// <summary>
    /// Beim Start (R2): Ohne Code und ohne Administrator kann niemand Administrator werden. Das steht dann einmal im
    /// Log, damit ein fehlender Administrator nicht unbemerkt bleibt. Ein zu kurzer Code steht immer einmal im Log,
    /// in einer eigenen Zeile statt der ohne Code: Wer ihn gesetzt hat, soll sehen, warum er nicht wirkt (A17).
    /// </summary>
    public void WarnIfNoAdministratorCanArise(DuofyDbContext db)
    {
        if (IsTooShort(ConfiguredSetupCode()))
        {
            logger.LogWarning("Einrichtungscode kürzer als {MinLength} Zeichen: Er gilt als nicht gesetzt, mit ihm kann kein Admin entstehen.",
                SetupCodeMinLength);
            return;
        }
        if (!HasSetupCode && !db.Users.Any(u => u.IsInstanceAdmin))
        {
            logger.LogWarning("Kein Einrichtungscode gesetzt: Es kann kein Admin entstehen.");
        }
    }

    // Der Code selbst kommt nie ins Log.
    private string? SetupCode()
    {
        var value = ConfiguredSetupCode();
        return string.IsNullOrEmpty(value) || IsTooShort(value) ? null : value;
    }

    private string? ConfiguredSetupCode() => configuration[SetupCodeKey]?.Trim();

    private static bool IsTooShort(string? value) => !string.IsNullOrEmpty(value) && value.Length < SetupCodeMinLength;
}

/// <summary>
/// Die Administratoren des Servers in der Datenbank. Wer den ersten Administrator macht oder den letzten löscht,
/// sperrt vorher mit <see cref="LockAsync"/>: Zwei Registrierungen mit dem Code zur selben Zeit kommen so
/// nacheinander dran, und die zweite sieht den ersten Administrator schon.
/// </summary>
internal sealed class InstanceAdmins(DuofyDbContext db)
{
    // Der Schlüssel der Sperre in PostgreSQL ("DuofyAdm" als Zahl); er gilt bis zum Ende der Transaktion.
    private const long LockKey = 0x4475_6F66_7941_646D;

    /// <summary>Sperrt bis zum Ende der laufenden Transaktion alle anderen, die Administratoren ändern wollen.</summary>
    public Task LockAsync(CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", [LockKey], cancellationToken);

    /// <summary>True, sobald es einen Administrator gibt.</summary>
    public Task<bool> ExistAsync(CancellationToken cancellationToken) =>
        db.Users.AnyAsync(u => u.IsInstanceAdmin, cancellationToken);

    /// <summary>True, wenn die Person Administrator ist.</summary>
    public Task<bool> IsAdminAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(u => u.Id == userId && u.IsInstanceAdmin, cancellationToken);

    /// <summary>True, wenn es außer der Person noch einen Administrator gibt.</summary>
    public Task<bool> OthersExistAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(u => u.Id != userId && u.IsInstanceAdmin, cancellationToken);
}
