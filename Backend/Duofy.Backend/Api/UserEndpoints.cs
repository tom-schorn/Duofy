using System.Security.Claims;
using Duofy.Schemas;
using Duofy.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Duofy.Api;

/// <summary>
/// Das eigene Konto (/api/v1/users/me). Jeder Endpunkt arbeitet mit dem Benutzer
/// aus dem Token, nie mit einer Kennung aus der Anfrage; so kann niemand fremde
/// Konten ändern.
/// </summary>
internal static class UserEndpoints
{
    private const string LogCategory = "Duofy.Api.Users";

    public static void MapUserEndpoints(this IEndpointRouteBuilder v1)
    {
        var me = v1.MapGroup("/users/me").RequireAuthorization();
        me.MapGet("", GetAsync);
        me.MapPatch("", UpdateAsync);
        me.MapPost("/password", ChangePasswordAsync);
        me.MapPost("/email", ChangeEmailAsync).RequireRateLimiting(AuthEndpoints.RateLimitPolicy);
        me.MapDelete("", DeleteAsync);
        me.MapGet("/quotas", GetQuotasAsync);
        me.MapPut("/quotas", PutQuotasAsync);
        me.MapPost("/claim-admin", ClaimAdminAsync).RequireRateLimiting(AuthEndpoints.RateLimitPolicy);
    }

    /// <summary>
    /// Ein bestehendes Konto wird mit dem Einrichtungscode Administrator (Spec 70, R1), nach derselben Regel wie die
    /// Registrierung: nur solange es keinen Administrator gibt und nur mit dem richtigen Code. Sonst 400
    /// setup_code_invalid, ohne zu verraten, ob ein Code eingestellt ist oder es schon einen Administrator gibt. Die
    /// Anfrage geht durch dieselbe Bremse wie die Anmeldung, damit niemand den Code ungebremst errät.
    /// </summary>
    private static async Task<IResult> ClaimAdminAsync(
        ClaimAdminRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> users,
        DuofyDbContext db,
        InstanceSettings settings,
        InstanceAdmins admins,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (await users.GetUserAsync(principal) is not { } user)
        {
            return Results.Unauthorized();
        }

        var logger = loggers.CreateLogger(LogCategory);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await admins.LockAsync(cancellationToken);
        if (await admins.ExistAsync(cancellationToken) || !settings.IsSetupCode(request.SetupCode))
        {
            logger.LogWarning("Konto {UserId} wollte mit dem Einrichtungscode Administrator werden; abgelehnt.", user.Id);
            return ApiErrors.BadRequest(ErrorCodes.SetupCodeInvalid);
        }

        user.IsInstanceAdmin = true;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Konto {user.Id} nicht zum Administrator gemacht: {string.Join(", ", result.Errors.Select(e => e.Code))}");
        }
        await transaction.CommitAsync(cancellationToken);

        logger.LogWarning("Konto {UserId} ist mit dem Einrichtungscode Administrator dieses Servers geworden.", user.Id);
        return Results.Ok(ToResponse(user));
    }

    /// <summary>Die Zielquoten der Person (Spec 66, R8); ohne eigene Einstellung 50/30/20.</summary>
    private static async Task<IResult> GetQuotasAsync(ClaimsPrincipal principal, UserManager<AppUser> users) =>
        await users.GetUserAsync(principal) is { } user
            ? Results.Ok(new QuotaSettings(user.QuotaNeeds, user.QuotaWants, user.QuotaSavings))
            : Results.Unauthorized();

    /// <summary>
    /// Ändert die Zielquoten: ganze Prozent von 0 bis 100, zusammen 100. Neue Monate übernehmen
    /// sie; Monate, die es schon gibt, behalten ihre.
    /// </summary>
    private static async Task<IResult> PutQuotasAsync(
        QuotaSettings request, ClaimsPrincipal principal, UserManager<AppUser> users)
    {
        if (await users.GetUserAsync(principal) is not { } user)
        {
            return Results.Unauthorized();
        }
        if (!new Domain.Quotas(request.Needs, request.Wants, request.Savings).IsValid)
        {
            return ApiErrors.BadRequest(ErrorCodes.QuotaInvalid);
        }

        user.QuotaNeeds = request.Needs;
        user.QuotaWants = request.Wants;
        user.QuotaSavings = request.Savings;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Quoten von Konto {user.Id} nicht gespeichert: {string.Join(", ", result.Errors.Select(e => e.Code))}");
        }
        return Results.Ok(new QuotaSettings(user.QuotaNeeds, user.QuotaWants, user.QuotaSavings));
    }

    /// <summary>
    /// Prüft das Passwort wie die Anmeldung: Fehlversuche zählen zur Sperre, und ein
    /// gesperrtes Konto gilt als falsches Passwort. Sonst könnte jemand mit einem
    /// gestohlenen Access-Token das Passwort hier ungebremst erraten.
    /// </summary>
    private static async Task<bool> IsCurrentPasswordAsync(SignInManager<AppUser> signIn, AppUser user, string? password)
    {
        var result = await signIn.CheckPasswordSignInAsync(user, password ?? "", lockoutOnFailure: true);
        return result.Succeeded;
    }

    private static MeResponse ToResponse(AppUser user) =>
        new(user.Id, user.Email ?? "", user.FirstName, user.LastName, user.IsInstanceAdmin);

    private static async Task<IResult> GetAsync(ClaimsPrincipal principal, UserManager<AppUser> users) =>
        await users.GetUserAsync(principal) is { } user
            ? Results.Ok(ToResponse(user))
            : Results.Unauthorized();

    private static async Task<IResult> UpdateAsync(
        UpdateMeRequest request, ClaimsPrincipal principal, UserManager<AppUser> users)
    {
        if (await users.GetUserAsync(principal) is not { } user)
        {
            return Results.Unauthorized();
        }

        var firstName = AccountValidation.Clean(request.FirstName);
        var lastName = AccountValidation.Clean(request.LastName);
        if (AccountValidation.CheckNames(firstName, lastName) is { } error)
        {
            return ApiErrors.BadRequest(error);
        }

        user.FirstName = firstName;
        user.LastName = lastName;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Namen von Konto {user.Id} nicht gespeichert: {string.Join(", ", result.Errors.Select(e => e.Code))}");
        }
        return Results.Ok(ToResponse(user));
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> users,
        SignInManager<AppUser> signIn,
        ILoggerFactory loggers)
    {
        if (await users.GetUserAsync(principal) is not { } user)
        {
            return Results.Unauthorized();
        }
        if (AccountValidation.CheckPassword(request.NewPassword) is { } error)
        {
            return ApiErrors.BadRequest(error);
        }
        if (!await IsCurrentPasswordAsync(signIn, user, request.CurrentPassword))
        {
            return ApiErrors.BadRequest(ErrorCodes.PasswordWrong);
        }

        // ChangePasswordAsync setzt einen neuen Sicherheitsstempel: Refresh-Tokens
        // von vorher gelten nicht mehr.
        var result = await users.ChangePasswordAsync(user, request.CurrentPassword!, request.NewPassword);
        if (!result.Succeeded)
        {
            var codes = result.Errors.Select(e => e.Code).ToList();
            if (codes.Contains(nameof(IdentityErrorDescriber.PasswordTooShort)))
            {
                return ApiErrors.BadRequest(ErrorCodes.PasswordTooShort);
            }
            throw new InvalidOperationException(
                $"Passwort von Konto {user.Id} nicht geändert: {string.Join(", ", codes)}");
        }

        loggers.CreateLogger(LogCategory).LogInformation("Passwort von Konto {UserId} geändert.", user.Id);
        return Results.NoContent();
    }

    /// <summary>
    /// Eine neue Adresse mit dem aktuellen Passwort (Spec 53, R5). Auf der gehosteten Version gilt sie erst, wenn die
    /// Person den Link aus der Mail an die neue Adresse öffnet (202); bis dahin bleibt die alte. Auf einem selbst
    /// betriebenen Server gilt sie sofort (200). Beides setzt einen neuen Sicherheitsstempel, sobald die Adresse
    /// wechselt: Die Person meldet sich überall neu an, auf diesem Gerät tut das der Client für sie.
    ///
    /// Ob eine Adresse vergeben ist (409 email_taken), erfährt nur, wer das richtige Passwort kennt.
    /// </summary>
    private static async Task<IResult> ChangeEmailAsync(
        ChangeEmailRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> users,
        SignInManager<AppUser> signIn,
        HostedSettings hosted,
        PublicLinks links,
        Mailer mailer,
        MailThrottle throttle,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (await users.GetUserAsync(principal) is not { } user)
        {
            return Results.Unauthorized();
        }
        var newEmail = AccountValidation.Clean(request.NewEmail);
        if (AccountValidation.CheckEmail(newEmail) is { } error)
        {
            return ApiErrors.BadRequest(error);
        }
        if (!await IsCurrentPasswordAsync(signIn, user, request.Password))
        {
            return ApiErrors.BadRequest(ErrorCodes.PasswordWrong);
        }
        if (newEmail == user.Email)
        {
            return Results.Ok(ToResponse(user));
        }
        if (await users.FindByEmailAsync(newEmail) is { } holder && holder.Id != user.Id)
        {
            return Results.Json(new ErrorResponse(ErrorCodes.EmailTaken), statusCode: StatusCodes.Status409Conflict);
        }

        var logger = loggers.CreateLogger(LogCategory);
        if (hosted.Hosted)
        {
            if (throttle.TryTake(MailEndpoints.Purposes.EmailChange, newEmail))
            {
                var token = await users.GenerateChangeEmailTokenAsync(user, newEmail);
                var link = links.ConfirmEmailChange(user.Id, newEmail, token)
                    ?? throw new InvalidOperationException($"{PublicLinks.SettingKey} fehlt; kein Link zum Wechsel für Konto {user.Id}.");
                mailer.Queue(MailTexts.ConfirmEmailChange(newEmail, link), MailEndpoints.Purposes.EmailChange);
            }
            logger.LogInformation("Wechsel der Adresse von Konto {UserId} angefragt; wartet auf Bestätigung.", user.Id);
            return Results.Accepted();
        }

        var result = await users.SetEmailAsync(user, newEmail);
        if (!result.Succeeded)
        {
            return EmailChanges.Refused(result, user.Id);
        }
        if (await EmailChanges.SetUserNameAsync(users, user, newEmail) is { } refused)
        {
            return refused;
        }
        logger.LogInformation("Adresse von Konto {UserId} gewechselt.", user.Id);
        return Results.Ok(ToResponse(user));
    }

    private static async Task<IResult> DeleteAsync(
        [FromBody] DeleteMeRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> users,
        SignInManager<AppUser> signIn,
        DuofyDbContext db,
        HouseholdStore households,
        InstanceAdmins admins,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (await users.GetUserAsync(principal) is not { } user)
        {
            return Results.Unauthorized();
        }
        if (!await IsCurrentPasswordAsync(signIn, user, request.Password))
        {
            return ApiErrors.BadRequest(ErrorCodes.PasswordWrong);
        }

        // Der Haushalt der anderen bleibt (Spec 67, R9): erst den Gründer weitergeben oder den leeren
        // Haushalt löschen, dann den Benutzer, beides zusammen oder gar nicht. Die Datenbank löscht alles
        // mit, was dem Benutzer gehört (Fremdschlüssel mit Cascade: Verträge, Konten, Mitgliedschaft …). Dabei
        // steht kein Besitzer-Filter im Weg (Spec 56, R4).
        using var everything = db.Owners.Bypass("Konto löschen");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Der letzte Administrator bleibt (Spec 70, R5): Sonst könnte niemand mehr den Server verwalten. Die Sperre
        // hält eine gleichzeitige Änderung der Administratoren auf, bis hier entschieden ist.
        if (user.IsInstanceAdmin)
        {
            await admins.LockAsync(cancellationToken);
            if (!await admins.OthersExistAsync(user.Id, cancellationToken))
            {
                loggers.CreateLogger(LogCategory).LogInformation("Konto {UserId} ist der letzte Administrator; nicht gelöscht.", user.Id);
                return Results.Json(new ErrorResponse(ErrorCodes.LastAdmin), statusCode: StatusCodes.Status409Conflict);
            }
        }

        await households.HandOverAsync(user.Id, cancellationToken);
        var result = await users.DeleteAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Konto {user.Id} nicht gelöscht: {string.Join(", ", result.Errors.Select(e => e.Code))}");
        }

        await transaction.CommitAsync(cancellationToken);

        loggers.CreateLogger(LogCategory).LogInformation("Konto {UserId} gelöscht.", user.Id);
        return Results.NoContent();
    }
}
