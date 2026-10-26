using System.Security.Claims;
using Duofy.Schemas;
using Duofy.Data;
using Duofy.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api;

/// <summary>
/// Die Verwaltung des Servers (/api/v1/admin, Spec 70, R4 und R6): Einladungen zur Registrierung anlegen, auflisten
/// und zurückziehen.
///
/// Jeder Endpunkt hier verlangt einen Administrator (sonst 403 admin_required). Ein Administrator betreibt den
/// Server, er schaut nicht hinein: Nichts hier gibt Pläne, Buchungen, Konten oder Verträge anderer heraus, und die
/// Antworten nennen außer der eingeladenen Adresse niemanden.
/// </summary>
internal static class AdminEndpoints
{
    private const string LogCategory = "Duofy.Api.Admin";

    public static void MapAdminEndpoints(this IEndpointRouteBuilder v1)
    {
        var admin = v1.MapGroup("/admin").RequireAuthorization().AddEndpointFilter(RequireAdminAsync);
        admin.MapGet("/invitations", ListInvitationsAsync);
        admin.MapPost("/invitations", CreateInvitationAsync);
        admin.MapDelete("/invitations/{id:guid}", RevokeInvitationAsync);
    }

    /// <summary>Lässt nur Administratoren durch; alle anderen bekommen 403 admin_required.</summary>
    internal static async ValueTask<object?> RequireAdminAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var users = services.GetRequiredService<UserManager<AppUser>>();
        var admins = services.GetRequiredService<InstanceAdmins>();
        if (users.GetUserId(context.HttpContext.User) is not { } id
            || !await admins.IsAdminAsync(Guid.Parse(id), context.HttpContext.RequestAborted))
        {
            services.GetRequiredService<ILoggerFactory>().CreateLogger(LogCategory)
                .LogInformation("{Method} {Path} ohne Administrator abgelehnt.", context.HttpContext.Request.Method, context.HttpContext.Request.Path);
            return Results.Json(new ErrorResponse(ErrorCodes.AdminRequired), statusCode: StatusCodes.Status403Forbidden);
        }
        return await next(context);
    }

    private static RegistrationInvitationResponse ToResponse(RegistrationInvitation invitation, PublicLinks links) =>
        new(invitation.Id, invitation.Token, invitation.Email, invitation.ExpiresAt, links.RegistrationInvitation(invitation.Token));

    /// <summary>Die offenen Einladungen: nicht benutzt, nicht abgelaufen; die neueste zuerst.</summary>
    private static async Task<IResult> ListInvitationsAsync(
        DuofyDbContext db, PublicLinks links, TimeProvider time, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var open = await db.RegistrationInvitations
            .AsNoTracking()
            .Where(i => i.UsedAt == null && i.ExpiresAt > now)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
        return Results.Ok(open.Select(i => ToResponse(i, links)).ToList());
    }

    /// <summary>
    /// Eine neue Einladung, sieben Tage gültig und einmal benutzbar, auf Wunsch nur für eine Adresse. Die Antwort
    /// bringt den ganzen Link mit der öffentlichen Adresse des Servers mit; ohne diese Adresse entsteht keine
    /// Einladung (409 public_url_missing), denn einen Link, der nicht funktioniert, soll niemand weitergeben.
    ///
    /// Auf der gehosteten Version geht der Link einer Einladung mit Adresse auch per Mail dorthin (Spec 53, R6), an jede
    /// Adresse, denn die Person hat noch kein Konto; höchstens 20 am Tag je Administrator. Die Antwort ist dieselbe, ob
    /// eine Mail hinausging oder nicht.
    /// </summary>
    private static async Task<IResult> CreateInvitationAsync(
        CreateRegistrationInvitationRequest? request,
        ClaimsPrincipal principal,
        UserManager<AppUser> users,
        DuofyDbContext db,
        PublicLinks links,
        Mailer mailer,
        MailThrottle throttle,
        TimeProvider time,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var logger = loggers.CreateLogger(LogCategory);
        var email = AccountValidation.Clean(request?.Email);
        if (email.Length > 0 && AccountValidation.CheckEmail(email) is { } error)
        {
            return ApiErrors.BadRequest(error);
        }
        if (!links.IsConfigured)
        {
            logger.LogWarning("Einladung zur Registrierung abgelehnt: {Setting} fehlt.", PublicLinks.SettingKey);
            return Results.Json(new ErrorResponse(ErrorCodes.PublicUrlMissing), statusCode: StatusCodes.Status409Conflict);
        }

        // Auf Mikrosekunden gekürzt, so genau speichert PostgreSQL: Die Antwort nennt dann dieselbe Ablaufzeit wie
        // später die Liste.
        var now = time.GetUtcNow();
        now = now.AddTicks(-(now.Ticks % 10));
        var adminId = Guid.Parse(users.GetUserId(principal)!);
        var invitation = RegistrationInvitation.Create(adminId, email, now);
        db.RegistrationInvitations.Add(invitation);
        await db.SaveChangesAsync(cancellationToken);

        // Weder das Token noch die Adresse kommen ins Protokoll.
        logger.LogInformation("Einladung zur Registrierung {InvitationId} von {UserId} angelegt.", invitation.Id, adminId);
        if (mailer.IsOn && invitation.Email is { } address)
        {
            if (throttle.TryTakeRegistrationInvitation(adminId))
            {
                var inviter = await db.Users.Where(u => u.Id == adminId).Select(u => u.FirstName + " " + u.LastName).SingleAsync(cancellationToken);
                mailer.Queue(
                    MailTexts.RegistrationInvitation(address, inviter, links.RegistrationInvitation(invitation.Token)!),
                    "registration-invitation");
            }
            else
            {
                logger.LogWarning("Einladung zur Registrierung {InvitationId}: Tagesgrenze der Mails von {UserId} erreicht; keine Mail.", invitation.Id, adminId);
            }
        }
        return Results.Created($"/api/v1/admin/invitations/{invitation.Id}", ToResponse(invitation, links));
    }

    /// <summary>Zieht eine Einladung zurück: Die Zeile wird gelöscht, der Link gilt sofort nicht mehr.</summary>
    private static async Task<IResult> RevokeInvitationAsync(
        Guid id, DuofyDbContext db, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        var deleted = await db.RegistrationInvitations.Where(i => i.Id == id).ExecuteDeleteAsync(cancellationToken);
        if (deleted == 0)
        {
            return ApiErrors.NotFound(ErrorCodes.InvitationNotFound);
        }

        loggers.CreateLogger(LogCategory).LogInformation("Einladung zur Registrierung {InvitationId} zurückgezogen.", id);
        return Results.NoContent();
    }
}
