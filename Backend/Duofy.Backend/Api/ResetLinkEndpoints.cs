using System.Security.Claims;
using Duofy.Schemas;
using Duofy.Data;
using Duofy.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api;

/// <summary>
/// "Passwort vergessen" auf einem selbst betriebenen Server (Spec 53, R4): Es gibt keine Mails, also legt der
/// Administrator einen Link an (POST /api/v1/admin/password-reset-links) und gibt ihn der Person, als Link, QR-Code
/// oder über Teilen. Mit dem Link setzt sie selbst ein neues Passwort (POST /api/v1/auth/password/reset-by-link).
/// Auf der gehosteten Version gibt es beide Endpunkte nicht; dort geht der Link per Mail.
///
/// Der Administrator erfährt, ob es zu einer Adresse ein Konto gibt (404 user_not_found); sonst niemand.
/// </summary>
internal static class ResetLinkEndpoints
{
    private const string LogCategory = "Duofy.Api.Admin";

    public static void MapResetLinkEndpoints(this IEndpointRouteBuilder v1)
    {
        v1.MapGroup("/admin").RequireAuthorization().AddEndpointFilter(AdminEndpoints.RequireAdminAsync)
            .MapPost("/password-reset-links", CreateAsync);
        v1.MapGroup("/auth").RequireRateLimiting(AuthEndpoints.RateLimitPolicy)
            .MapPost("/password/reset-by-link", ResetAsync);
    }

    /// <summary>
    /// Ein Link für die Person mit dieser Adresse, 24 Stunden gültig und einmal benutzbar; ihre älteren offenen Links
    /// gelten danach nicht mehr (A13). Ohne öffentliche Adresse des Servers gibt es keinen (409 public_url_missing),
    /// denn einen Link, der nicht funktioniert, soll niemand weitergeben.
    /// </summary>
    private static async Task<IResult> CreateAsync(
        CreatePasswordResetLinkRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> users,
        DuofyDbContext db,
        PublicLinks links,
        TimeProvider time,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var logger = loggers.CreateLogger(LogCategory);
        var email = AccountValidation.Clean(request.Email);
        if (AccountValidation.CheckEmail(email) is { } error)
        {
            return ApiErrors.BadRequest(error);
        }
        if (!links.IsConfigured)
        {
            logger.LogWarning("Link zum Zurücksetzen abgelehnt: {Setting} fehlt.", PublicLinks.SettingKey);
            return Results.Json(new ErrorResponse(ErrorCodes.PublicUrlMissing), statusCode: StatusCodes.Status409Conflict);
        }
        var adminId = Guid.Parse(users.GetUserId(principal)!);
        if (await users.FindByEmailAsync(email) is not { } user)
        {
            logger.LogInformation("Link zum Zurücksetzen von {UserId} für eine unbekannte Adresse abgelehnt.", adminId);
            return ApiErrors.NotFound(ErrorCodes.UserNotFound);
        }

        // Auf Mikrosekunden gekürzt, so genau speichert PostgreSQL.
        var now = time.GetUtcNow();
        now = now.AddTicks(-(now.Ticks % 10));
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        PasswordResetLink.CloseOpen(await LockOpenLinksAsync(db, user.Id, cancellationToken), now);
        var (link, token) = PasswordResetLink.Create(user.Id, adminId, now);
        db.PasswordResetLinks.Add(link);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Das Token kommt nie ins Protokoll; es ist ein Schlüssel zum Konto.
        logger.LogWarning("Link zum Zurücksetzen {LinkId} für Konto {UserId} von Administrator {AdminId} angelegt.", link.Id, user.Id, adminId);
        return Results.Created((string?)null, new PasswordResetLinkResponse(links.PasswordResetLink(token)!, link.ExpiresAt));
    }

    /// <summary>
    /// Setzt mit dem Link das neue Passwort und verbraucht ihn und alle anderen offenen Links der Person (A13). Die
    /// Person bleibt bis zum Ende gesperrt: Zwei, die zur selben Zeit einen ihrer Links benutzen, kommen nacheinander
    /// dran, und der zweite findet seinen verbraucht. Ein neues Passwort meldet die Person überall ab.
    /// </summary>
    private static async Task<IResult> ResetAsync(
        ResetPasswordByLinkRequest request,
        UserManager<AppUser> users,
        DuofyDbContext db,
        TimeProvider time,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (AccountValidation.CheckPassword(request.NewPassword) is { } error)
        {
            return ApiErrors.BadRequest(error);
        }
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return ApiErrors.BadRequest(ErrorCodes.TokenInvalid);
        }

        var logger = loggers.CreateLogger(LogCategory);
        var now = time.GetUtcNow();
        var hash = PasswordResetLink.Hash(request.Token.Trim());
        var userId = await db.PasswordResetLinks.Where(l => l.TokenHash == hash)
            .Select(l => (Guid?)l.UserId).FirstOrDefaultAsync(cancellationToken);
        if (userId is null)
        {
            logger.LogInformation("Neues Passwort mit ungültigem Link abgelehnt.");
            return ApiErrors.BadRequest(ErrorCodes.TokenInvalid);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var open = await LockOpenLinksAsync(db, userId.Value, cancellationToken);
        var link = open.FirstOrDefault(l => l.TokenHash == hash);
        if (link is null || !link.IsUsable(now) || await users.FindByIdAsync(userId.Value.ToString()) is not { } user)
        {
            logger.LogInformation("Neues Passwort mit ungültigem Link abgelehnt.");
            return ApiErrors.BadRequest(ErrorCodes.TokenInvalid);
        }

        // Der Weg über ein Token von Identity setzt das Passwort mit allen Prüfungen und einem neuen Sicherheitsstempel.
        var reset = await users.ResetPasswordAsync(user, await users.GeneratePasswordResetTokenAsync(user), request.NewPassword);
        if (!reset.Succeeded)
        {
            var codes = reset.Errors.Select(e => e.Code).ToList();
            if (codes.Contains(nameof(IdentityErrorDescriber.PasswordTooShort)))
            {
                return ApiErrors.BadRequest(ErrorCodes.PasswordTooShort);
            }
            throw new InvalidOperationException($"Passwort von Konto {user.Id} nicht zurückgesetzt: {string.Join(", ", codes)}");
        }
        await PasswordResets.AfterResetAsync(users, user, addressProven: false);

        link.Use(now);
        PasswordResetLink.CloseOpen(open, now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogWarning("Passwort von Konto {UserId} mit Link {LinkId} neu gesetzt.", user.Id, link.Id);
        return Results.NoContent();
    }

    /// <summary>
    /// Sperrt die Zeile der Person bis zum Ende der Transaktion (<c>SELECT … FOR UPDATE</c>) und liest danach ihre
    /// offenen Links. Anlegen und Benutzen sperren beide zuerst die Person: So kommen zwei gleichzeitig nacheinander
    /// dran, keiner übersieht den Link des anderen, und keiner wartet auf einen Link, den der andere schon hält.
    /// </summary>
    private static async Task<List<PasswordResetLink>> LockOpenLinksAsync(DuofyDbContext db, Guid userId, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("SELECT id FROM users WHERE id = {0} FOR UPDATE", [userId], cancellationToken);
        return await db.PasswordResetLinks.Where(l => l.UserId == userId && l.UsedAt == null).ToListAsync(cancellationToken);
    }
}
