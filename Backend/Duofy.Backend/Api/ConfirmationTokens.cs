using Duofy.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Duofy.Api;

/// <summary>
/// Die Tokens zum Bestätigen einer Adresse (Spec 53): bei der Registrierung und beim Wechsel der Adresse. Es ist der
/// Baustein von Identity (<see cref="DataProtectorTokenProvider{TUser}"/>), nur mit eigener Laufzeit: 24 Stunden statt
/// der einen Stunde, die für "Passwort vergessen" gilt (<see cref="ResetLifetime"/>).
/// </summary>
internal sealed class ConfirmationTokenProvider(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<ConfirmationTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<AppUser>> logger)
    : DataProtectorTokenProvider<AppUser>(dataProtectionProvider, options, logger)
{
    /// <summary>Der Name, unter dem Identity den Anbieter kennt.</summary>
    public const string ProviderName = "DuofyConfirmation";

    /// <summary>So lange gilt ein Link zum Bestätigen einer Adresse.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    /// <summary>So lange gilt ein Link aus "Passwort vergessen" (der Standard-Anbieter von Identity).</summary>
    public static readonly TimeSpan ResetLifetime = TimeSpan.FromHours(1);
}

/// <summary>Die Einstellungen von <see cref="ConfirmationTokenProvider"/>; ein eigener Typ, damit sie die des Standard-Anbieters nicht berühren.</summary>
internal sealed class ConfirmationTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public ConfirmationTokenProviderOptions()
    {
        Name = ConfirmationTokenProvider.ProviderName;
        TokenLifespan = ConfirmationTokenProvider.Lifetime;
    }
}
