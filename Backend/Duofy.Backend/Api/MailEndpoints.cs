using Duofy.Schemas;
using Duofy.Data;
using Microsoft.AspNetCore.Identity;

namespace Duofy.Api;

/// <summary>
/// Die Wege über Mails (Spec 53, R2, R3, R5), nur auf der gehosteten Version; auf einem selbst betriebenen Server gibt
/// es diese Endpunkte nicht (404): Adresse bestätigen, den Link noch einmal schicken, "Passwort vergessen", ein neues
/// Passwort mit dem Link setzen und eine geänderte Adresse übernehmen.
///
/// Was eine Mail anfordert, antwortet immer 202, ob es die Adresse gibt oder nicht und ob die Bremse eine Mail
/// zurückhält: Niemand soll so erfahren, wer ein Konto hat. Ein ungültiges, benutztes oder abgelaufenes Token ergibt
/// 400 token_invalid, auch für eine unbekannte Person.
/// </summary>
internal static class MailEndpoints
{
    private const string LogCategory = "Duofy.Api.Mail";

    /// <summary>Die Zwecke, nach denen die Bremse Mails an eine Adresse zählt.</summary>
    public static class Purposes
    {
        public const string Confirmation = "confirmation";
        public const string PasswordReset = "password-reset";
        public const string EmailChange = "email-change";
        public const string HouseholdInvitation = "household-invitation";
    }

    public static void MapMailEndpoints(this IEndpointRouteBuilder v1)
    {
        var auth = v1.MapGroup("/auth").RequireRateLimiting(AuthEndpoints.RateLimitPolicy);
        auth.MapPost("/confirm-email", ConfirmEmailAsync);
        auth.MapPost("/confirm-email/resend", ResendAsync);
        auth.MapPost("/password/forgot", ForgotAsync);
        auth.MapPost("/password/reset", ResetAsync);
        auth.MapPost("/confirm-email-change", ConfirmEmailChangeAsync);
    }

    /// <summary>Schickt einem neuen, unbestätigten Konto den Link zum Bestätigen (bei der Registrierung und auf Wunsch).</summary>
    public static async Task SendConfirmationAsync(
        AppUser user, UserManager<AppUser> users, PublicLinks links, Mailer mailer, CancellationToken cancellationToken)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        if (links.ConfirmEmail(user.Id, token) is not { } link)
        {
            // Kann nur passieren, wenn die öffentliche Adresse nach dem Start verschwindet.
            throw new InvalidOperationException($"{PublicLinks.SettingKey} fehlt; kein Link zum Bestätigen für Konto {user.Id}.");
        }
        mailer.Queue(MailTexts.ConfirmEmail(user.Email!, link), Purposes.Confirmation);
    }

    private static IResult TokenInvalid() => ApiErrors.BadRequest(ErrorCodes.TokenInvalid);

    /// <summary>Bestätigt die Adresse (R2); danach klappt die Anmeldung.</summary>
    private static async Task<IResult> ConfirmEmailAsync(
        ConfirmEmailRequest request, UserManager<AppUser> users, ILoggerFactory loggers)
    {
        if (await users.FindByIdAsync(request.UserId.ToString()) is not { } user || string.IsNullOrEmpty(request.Token))
        {
            return TokenInvalid();
        }
        if (user.EmailConfirmed)
        {
            // Ein zweiter Klick auf denselben Link ist kein Fehler der Person; der Link gilt aber nicht mehr.
            return TokenInvalid();
        }
        var result = await users.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded)
        {
            loggers.CreateLogger(LogCategory).LogInformation("Bestätigung von Konto {UserId} abgelehnt: Token ungültig.", user.Id);
            return TokenInvalid();
        }
        loggers.CreateLogger(LogCategory).LogInformation("Adresse von Konto {UserId} bestätigt.", user.Id);
        return Results.NoContent();
    }

    /// <summary>Schickt den Link noch einmal (R2), höchstens einmal alle fünf Minuten je Adresse; antwortet immer 202.</summary>
    private static async Task<IResult> ResendAsync(
        ResendConfirmationRequest request,
        UserManager<AppUser> users,
        PublicLinks links,
        Mailer mailer,
        MailThrottle throttle,
        CancellationToken cancellationToken)
    {
        var email = AccountValidation.Clean(request.Email);
        if (email.Length > 0
            && await users.FindByEmailAsync(email) is { EmailConfirmed: false } user
            && throttle.TryTake(Purposes.Confirmation, email))
        {
            await SendConfirmationAsync(user, users, links, mailer, cancellationToken);
        }
        return Results.Accepted();
    }

    /// <summary>"Passwort vergessen" (R3): Ein Konto mit dieser Adresse bekommt einen Link, eine Stunde gültig; antwortet immer 202.</summary>
    private static async Task<IResult> ForgotAsync(
        ForgotPasswordRequest request,
        UserManager<AppUser> users,
        PublicLinks links,
        Mailer mailer,
        MailThrottle throttle,
        CancellationToken cancellationToken)
    {
        var email = AccountValidation.Clean(request.Email);
        if (email.Length > 0
            && await users.FindByEmailAsync(email) is { } user
            && throttle.TryTake(Purposes.PasswordReset, email))
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var link = links.PasswordReset(user.Email!, token)
                ?? throw new InvalidOperationException($"{PublicLinks.SettingKey} fehlt; kein Link zum Zurücksetzen für Konto {user.Id}.");
            mailer.Queue(MailTexts.PasswordReset(user.Email!, link), Purposes.PasswordReset);
        }
        return Results.Accepted();
    }

    /// <summary>
    /// Setzt mit dem Link das neue Passwort (R3). Identity setzt dabei einen neuen Sicherheitsstempel: Jedes
    /// Refresh-Token von vorher gilt nicht mehr, die Person ist überall abgemeldet. Wer den Link aus der Mail benutzt,
    /// hat die Adresse damit auch bestätigt.
    /// </summary>
    private static async Task<IResult> ResetAsync(
        ResetPasswordRequest request, UserManager<AppUser> users, ILoggerFactory loggers)
    {
        if (AccountValidation.CheckPassword(request.NewPassword) is { } error)
        {
            return ApiErrors.BadRequest(error);
        }
        var email = AccountValidation.Clean(request.Email);
        if (email.Length == 0 || string.IsNullOrEmpty(request.Token) || await users.FindByEmailAsync(email) is not { } user)
        {
            return TokenInvalid();
        }

        var logger = loggers.CreateLogger(LogCategory);
        var result = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            var codes = result.Errors.Select(e => e.Code).ToList();
            if (codes.Contains(nameof(IdentityErrorDescriber.InvalidToken)))
            {
                logger.LogInformation("Neues Passwort für Konto {UserId} abgelehnt: Token ungültig.", user.Id);
                return TokenInvalid();
            }
            if (codes.Contains(nameof(IdentityErrorDescriber.PasswordTooShort)))
            {
                return ApiErrors.BadRequest(ErrorCodes.PasswordTooShort);
            }
            throw new InvalidOperationException($"Passwort von Konto {user.Id} nicht zurückgesetzt: {string.Join(", ", codes)}");
        }

        await PasswordResets.AfterResetAsync(users, user, addressProven: true);
        logger.LogInformation("Passwort von Konto {UserId} über den Link aus der Mail neu gesetzt.", user.Id);
        return Results.NoContent();
    }

    /// <summary>
    /// Übernimmt die neue Adresse mit dem Link, der an sie ging (R5), und sagt der alten Adresse Bescheid. Ist die
    /// neue Adresse inzwischen vergeben, antwortet die API 409 email_taken.
    /// </summary>
    private static async Task<IResult> ConfirmEmailChangeAsync(
        ConfirmEmailChangeRequest request,
        UserManager<AppUser> users,
        Mailer mailer,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var newEmail = AccountValidation.Clean(request.NewEmail);
        if (newEmail.Length == 0 || string.IsNullOrEmpty(request.Token)
            || await users.FindByIdAsync(request.UserId.ToString()) is not { } user)
        {
            return TokenInvalid();
        }
        var logger = loggers.CreateLogger(LogCategory);
        if (await users.FindByEmailAsync(newEmail) is { } holder && holder.Id != user.Id)
        {
            logger.LogInformation("Wechsel der Adresse von Konto {UserId} abgelehnt: Adresse inzwischen vergeben.", user.Id);
            return Results.Json(new ErrorResponse(ErrorCodes.EmailTaken), statusCode: StatusCodes.Status409Conflict);
        }

        var oldEmail = user.Email!;
        if (string.Equals(oldEmail, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            // Schon übernommen: Der Link gilt nicht noch einmal.
            return TokenInvalid();
        }
        var result = await users.ChangeEmailAsync(user, newEmail, request.Token);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken)))
            {
                logger.LogInformation("Wechsel der Adresse von Konto {UserId} abgelehnt: Token ungültig.", user.Id);
                return TokenInvalid();
            }
            return EmailChanges.Refused(result, user.Id);
        }
        if (await EmailChanges.SetUserNameAsync(users, user, newEmail) is { } refused)
        {
            return refused;
        }

        logger.LogInformation("Adresse von Konto {UserId} nach Bestätigung gewechselt.", user.Id);
        mailer.Queue(MailTexts.EmailChanged(oldEmail, newEmail), "email-changed");
        return Results.NoContent();
    }
}

/// <summary>Was nach jedem neuen Passwort ohne das alte gilt, ob über die Mail oder den Link des Administrators.</summary>
internal static class PasswordResets
{
    /// <summary>
    /// Hebt eine Sperre nach falschen Passwörtern auf, damit die Person mit dem neuen Passwort sofort hineinkommt. Kam
    /// der Link per Mail (<paramref name="addressProven"/>), gilt die Adresse danach auch als bestätigt.
    /// </summary>
    public static async Task AfterResetAsync(UserManager<AppUser> users, AppUser user, bool addressProven)
    {
        Check(await users.ResetAccessFailedCountAsync(user), user.Id);
        if (user.LockoutEnd is not null)
        {
            Check(await users.SetLockoutEndDateAsync(user, null), user.Id);
        }
        if (addressProven && !user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            Check(await users.UpdateAsync(user), user.Id);
        }
    }

    private static void Check(IdentityResult result, Guid userId)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Konto {userId} nach neuem Passwort nicht entsperrt: {string.Join(", ", result.Errors.Select(e => e.Code))}");
        }
    }
}

/// <summary>Gemeinsames für den Wechsel der Adresse: Der Benutzername ist bei Duofy immer die Adresse.</summary>
internal static class EmailChanges
{
    /// <summary>Setzt den Benutzernamen auf die neue Adresse; null bei Erfolg, sonst die Antwort.</summary>
    public static async Task<IResult?> SetUserNameAsync(UserManager<AppUser> users, AppUser user, string email)
    {
        var result = await users.SetUserNameAsync(user, email);
        return result.Succeeded ? null : Refused(result, user.Id);
    }

    /// <summary>Übersetzt eine Ablehnung von Identity beim Wechsel; Unerwartetes ist ein Fehler.</summary>
    public static IResult Refused(IdentityResult result, Guid userId)
    {
        var codes = result.Errors.Select(e => e.Code).ToList();
        if (codes.Contains(nameof(IdentityErrorDescriber.DuplicateEmail)) || codes.Contains(nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return Results.Json(new ErrorResponse(ErrorCodes.EmailTaken), statusCode: StatusCodes.Status409Conflict);
        }
        if (codes.Contains(nameof(IdentityErrorDescriber.InvalidEmail)) || codes.Contains(nameof(IdentityErrorDescriber.InvalidUserName)))
        {
            return ApiErrors.BadRequest(ErrorCodes.EmailInvalid);
        }
        throw new InvalidOperationException($"Adresse von Konto {userId} nicht gewechselt: {string.Join(", ", codes)}");
    }
}
