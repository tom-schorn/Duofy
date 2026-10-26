using System.Security.Claims;
using Duofy.Schemas;
using Duofy.Data;
using Duofy.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Duofy.Api;

/// <summary>
/// Der Haushalt (/api/v1/households, Spec 67): gründen, ansehen, umbenennen, Quoten, Mitglieder entfernen,
/// Austreten und Einladungen; dazu die Freigaben zwischen den Mitgliedern (Spec 68).
///
/// Die Person kommt immer aus dem Token. Ein Haushalt, in dem sie nicht Mitglied ist, sieht aus wie ein
/// unbekannter: 404 mit dem Code household_not_found. Verwalten darf nur der Gründer; Mitglieder bekommen
/// 403. Eine Rolle ändern lässt sich nicht, dafür gibt es keinen Endpunkt.
/// </summary>
internal static class HouseholdEndpoints
{
    private const string LogCategory = "Duofy.Api.Households";

    public static void MapHouseholdEndpoints(this IEndpointRouteBuilder v1)
    {
        var households = v1.MapGroup("/households").RequireAuthorization();
        households.MapPost("", FoundAsync);
        households.MapGet("/mine", GetMineAsync);
        households.MapPatch("/{id:guid}", UpdateAsync);
        households.MapDelete("/{id:guid}/members/me", LeaveAsync);
        households.MapDelete("/{id:guid}/members/{userId:guid}", RemoveAsync);
        households.MapPut("/{id:guid}/grants/{granteeId:guid}", SetGrantsAsync);
        households.MapGet("/{id:guid}/invitations", ListInvitationsAsync);
        households.MapPost("/{id:guid}/invitations", CreateInvitationAsync);
        households.MapDelete("/{id:guid}/invitations/{invitationId:guid}", RevokeInvitationAsync);
    }

    internal static Guid? UserOf(ClaimsPrincipal principal, UserManager<AppUser> users) =>
        users.GetUserId(principal) is { } id ? Guid.Parse(id) : null;

    internal static IResult NotFound(string code = ErrorCodes.HouseholdNotFound) =>
        Results.Json(new ErrorResponse(code), statusCode: StatusCodes.Status404NotFound);

    internal static IResult Conflict(string code) =>
        Results.Json(new ErrorResponse(code), statusCode: StatusCodes.Status409Conflict);

    internal static IResult Forbidden() =>
        Results.Json(new ErrorResponse(ErrorCodes.NotHouseholdFounder), statusCode: StatusCodes.Status403Forbidden);

    /// <summary>True, wenn das Speichern an einem eindeutigen Index scheiterte, hier: die Person ist schon in einem Haushalt.</summary>
    internal static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    /// <summary>Null, wenn die Person den Haushalt gegründet hat; sonst die Antwort 404 (kein Mitglied) oder 403 (Mitglied).</summary>
    private static async Task<IResult?> RequireFounderAsync(
        HouseholdStore households, Guid householdId, Guid userId, CancellationToken cancellationToken) =>
        await households.RoleOfAsync(householdId, userId, cancellationToken) switch
        {
            null => NotFound(),
            HouseholdRole.Founder => null,
            _ => Forbidden(),
        };

    private static InvitationResponse ToResponse(HouseholdInvitation invitation, PublicLinks links) =>
        new(invitation.Id, invitation.Token, invitation.ExpiresAt, links.Invitation(invitation.Token));

    /// <summary>
    /// Gründet einen Haushalt mit der Person als Gründer (R1). Ohne Namen, oder mit einem leeren, heißt er
    /// "Haushalt von Vorname"; ein Name hat höchstens 100 Zeichen. Wer schon in einem Haushalt ist, bekommt 409.
    /// </summary>
    private static async Task<IResult> FoundAsync(
        CreateHouseholdRequest? request,
        ClaimsPrincipal principal,
        UserManager<AppUser> users,
        HouseholdStore households,
        DuofyDbContext db,
        TimeProvider time,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (await users.GetUserAsync(principal) is not { } user)
        {
            return Results.Unauthorized();
        }
        var name = request?.Name?.Trim();
        if (name is { Length: > HouseholdRules.NameMaxLength })
        {
            return ApiErrors.BadRequest(ErrorCodes.HouseholdNameTooLong);
        }
        if (await households.IdOfAsync(user.Id, cancellationToken) is not null)
        {
            return Conflict(ErrorCodes.AlreadyInHousehold);
        }

        var household = Household.Found(user.Id, user.FirstName, string.IsNullOrEmpty(name) ? null : name, time.GetUtcNow());
        db.Households.Add(household);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Zur selben Zeit gegründet oder beigetreten: Der eindeutige Index auf der Person hält es auf.
            loggers.CreateLogger(LogCategory).LogInformation(ex, "{UserId} ist beim Gründen schon in einem Haushalt.", user.Id);
            return Conflict(ErrorCodes.AlreadyInHousehold);
        }

        loggers.CreateLogger(LogCategory).LogInformation("{UserId} hat Haushalt {HouseholdId} gegründet.", user.Id, household.Id);
        return Results.Created("/api/v1/households/mine", await households.ResponseAsync(household, user.Id, cancellationToken));
    }

    /// <summary>Der Haushalt der Person (R3); ohne Haushalt 404 mit dem Code no_household.</summary>
    private static async Task<IResult> GetMineAsync(
        ClaimsPrincipal principal, UserManager<AppUser> users, HouseholdStore households, CancellationToken cancellationToken)
    {
        if (UserOf(principal, users) is not { } userId)
        {
            return Results.Unauthorized();
        }
        if (await households.IdOfAsync(userId, cancellationToken) is not { } householdId)
        {
            return NotFound(ErrorCodes.NoHousehold);
        }

        var household = await households.LoadAsync(householdId, cancellationToken);
        return Results.Ok(await households.ResponseAsync(household, userId, cancellationToken));
    }

    /// <summary>
    /// Benennt um und ändert die Quoten des Haushalts; nur der Gründer (R7). Der Name hat 1 bis 100 Zeichen; die
    /// Quoten kommen alle drei oder gar nicht, als ganze Prozent mit der Summe 100. Jede Person behält
    /// daneben ihre eigenen Quoten.
    /// </summary>
    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateHouseholdRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> users,
        HouseholdStore households,
        DuofyDbContext db,
        CancellationToken cancellationToken)
    {
        if (UserOf(principal, users) is not { } userId)
        {
            return Results.Unauthorized();
        }
        if (await RequireFounderAsync(households, id, userId, cancellationToken) is { } refused)
        {
            return refused;
        }

        var name = request.Name?.Trim();
        if (name is not null && name.Length == 0)
        {
            return ApiErrors.BadRequest(ErrorCodes.HouseholdNameRequired);
        }
        if (name is not null && name.Length > HouseholdRules.NameMaxLength)
        {
            return ApiErrors.BadRequest(ErrorCodes.HouseholdNameTooLong);
        }
        Quotas? quotas = null;
        if (request.QuotaNeeds is not null || request.QuotaWants is not null || request.QuotaSavings is not null)
        {
            if (request is not { QuotaNeeds: { } needs, QuotaWants: { } wants, QuotaSavings: { } savings }
                || !new Quotas(needs, wants, savings).IsValid)
            {
                return ApiErrors.BadRequest(ErrorCodes.QuotaInvalid);
            }
            quotas = new Quotas(needs, wants, savings);
        }

        var household = await households.LoadAsync(id, cancellationToken);
        household.Name = name ?? household.Name;
        household.Quotas = quotas ?? household.Quotas;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await households.ResponseAsync(household, userId, cancellationToken));
    }

    /// <summary>
    /// Tritt aus (R8): Ein Mitglied ist danach in keinem Haushalt und behält alle Daten; alle Freigaben von und
    /// an die Person verschwinden in derselben Transaktion (Spec 68, R3). Der Gründer kann nur
    /// austreten, wenn sonst niemand im Haushalt ist; dann löst sich der Haushalt mit seinen Einladungen auf.
    /// </summary>
    private static async Task<IResult> LeaveAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<AppUser> users,
        HouseholdStore households,
        DuofyDbContext db,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (UserOf(principal, users) is not { } userId)
        {
            return Results.Unauthorized();
        }
        if (await households.RoleOfAsync(id, userId, cancellationToken) is null)
        {
            return NotFound();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await households.LockAsync(id, cancellationToken);
        var household = await households.LoadAsync(id, cancellationToken);
        if (household.MemberOf(userId) is not { } member)
        {
            return NotFound();
        }
        var dissolved = false;
        if (member.Role == HouseholdRole.Founder)
        {
            if (household.Members.Count > 1)
            {
                return Conflict(ErrorCodes.FounderCannotLeave);
            }
            db.Households.Remove(household);
            dissolved = true;
        }
        else
        {
            db.HouseholdMembers.Remove(member);
        }
        await households.DropGrantsAsync(userId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var logger = loggers.CreateLogger(LogCategory);
        if (dissolved)
        {
            logger.LogInformation("{UserId} hat Haushalt {HouseholdId} aufgelöst.", userId, id);
        }
        else
        {
            logger.LogInformation("{UserId} ist aus Haushalt {HouseholdId} ausgetreten.", userId, id);
        }
        return Results.NoContent();
    }

    /// <summary>
    /// Entfernt ein anderes Mitglied; nur der Gründer (R7, R8). Für die entfernte Person ist es wie Austreten:
    /// Sie ist danach in keinem Haushalt und behält alle Daten, ihre Freigaben verschwinden (Spec 68, R3). Sich
    /// selbst entfernt man nicht.
    /// </summary>
    private static async Task<IResult> RemoveAsync(
        Guid id,
        Guid userId,
        ClaimsPrincipal principal,
        UserManager<AppUser> users,
        HouseholdStore households,
        DuofyDbContext db,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (UserOf(principal, users) is not { } callerId)
        {
            return Results.Unauthorized();
        }
        if (await RequireFounderAsync(households, id, callerId, cancellationToken) is { } refused)
        {
            return refused;
        }
        if (userId == callerId)
        {
            return ApiErrors.BadRequest(ErrorCodes.CannotRemoveSelf);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await households.LockAsync(id, cancellationToken);
        var household = await households.LoadAsync(id, cancellationToken);
        if (household.MemberOf(userId) is not { } member)
        {
            return NotFound(ErrorCodes.MemberNotFound);
        }

        db.HouseholdMembers.Remove(member);
        await households.DropGrantsAsync(userId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        loggers.CreateLogger(LogCategory).LogInformation(
            "{MemberId} wurde von {UserId} aus Haushalt {HouseholdId} entfernt.", userId, callerId, id);
        return Results.NoContent();
    }

    /// <summary>
    /// Setzt, was die angemeldete Person einem anderen Mitglied mit ihren Daten erlaubt (Spec 68, R1, R2). Der
    /// Geber kommt nur aus dem Token; niemand setzt Freigaben für die Daten eines anderen, auch der Gründer nicht.
    /// Nur die geschickten Bereiche ändern sich. Die Sperre auf dem Haushalt reiht das Setzen hinter ein
    /// gleichzeitiges Austreten ein, so bleibt keine Freigabe an jemanden übrig, der nicht mehr dazugehört.
    /// </summary>
    private static async Task<IResult> SetGrantsAsync(
        Guid id,
        Guid granteeId,
        UpdateGrantsRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> users,
        HouseholdStore households,
        DuofyDbContext db,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (UserOf(principal, users) is not { } userId)
        {
            return Results.Unauthorized();
        }
        if (await households.RoleOfAsync(id, userId, cancellationToken) is null)
        {
            return NotFound();
        }
        if (granteeId == userId)
        {
            return ApiErrors.BadRequest(ErrorCodes.CannotGrantSelf);
        }
        var levels = new Dictionary<Area, AccessLevel>();
        foreach (var (area, key) in new[]
        {
            (Area.Plan, request.Plan), (Area.Book, request.Book), (Area.Accounts, request.Accounts), (Area.Commitments, request.Commitments),
        })
        {
            if (key is null)
            {
                continue;
            }
            if (!GrantKeys.TryParse(key, out var level))
            {
                return ApiErrors.BadRequest(ErrorCodes.GrantLevelInvalid);
            }
            levels[area] = level;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await households.LockAsync(id, cancellationToken);
        var household = await households.LoadAsync(id, cancellationToken);
        if (household.MemberOf(userId) is null)
        {
            return NotFound();
        }
        if (household.MemberOf(granteeId) is not { } grantee)
        {
            return NotFound(ErrorCodes.MemberNotFound);
        }

        await households.SetGrantsAsync(userId, granteeId, levels, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        loggers.CreateLogger(LogCategory).LogInformation(
            "{UserId} hat {GranteeId} in Haushalt {HouseholdId} Freigaben gesetzt: {Levels}.",
            userId, granteeId, id, string.Join(", ", levels.Select(l => $"{GrantKeys.ToKey(l.Key)}={GrantKeys.ToKey(l.Value)}")));
        return Results.Ok(await households.MemberResponseAsync(grantee, userId, cancellationToken));
    }

    /// <summary>
    /// Die offenen, nicht abgelaufenen Einladungen, die neueste zuerst, jede mit ihrem Link; nur für den
    /// Gründer (R4).
    /// </summary>
    private static async Task<IResult> ListInvitationsAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<AppUser> users,
        HouseholdStore households,
        DuofyDbContext db,
        PublicLinks links,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (UserOf(principal, users) is not { } userId)
        {
            return Results.Unauthorized();
        }
        if (await RequireFounderAsync(households, id, userId, cancellationToken) is { } refused)
        {
            return refused;
        }

        var now = time.GetUtcNow();
        var open = await db.HouseholdInvitations
            .AsNoTracking()
            .Where(i => i.HouseholdId == id && i.Status == InvitationStatus.Pending && i.ExpiresAt > now)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
        return Results.Ok(open.Select(i => ToResponse(i, links)).ToList());
    }

    /// <summary>
    /// Eine neue Einladung, 14 Tage gültig und einmal benutzbar; höchstens 10 sind zugleich offen (R4).
    /// Die Antwort bringt den ganzen Link mit der öffentlichen Adresse des Servers mit; ohne diese Adresse
    /// entsteht keine Einladung (409 public_url_missing), denn einen Link, der nicht funktioniert, soll
    /// niemand weitergeben. Die Sperre auf dem Haushalt sorgt dafür, dass zwei gleichzeitige Anfragen die
    /// Grenze nicht überspringen.
    ///
    /// Auf der gehosteten Version darf eine Adresse dabei sein (Spec 53, R6): Gehört sie zu einem bestätigten Konto, geht
    /// der Link per Mail dorthin. Die Antwort ist dieselbe, ob eine Mail hinausging oder nicht; so verrät die Einladung
    /// nicht, wer ein Konto hat. Ein selbst betriebener Server übergeht die Adresse.
    /// </summary>
    private static async Task<IResult> CreateInvitationAsync(
        Guid id,
        CreateHouseholdInvitationRequest? request,
        ClaimsPrincipal principal,
        UserManager<AppUser> users,
        HouseholdStore households,
        DuofyDbContext db,
        PublicLinks links,
        Mailer mailer,
        MailThrottle throttle,
        TimeProvider time,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (UserOf(principal, users) is not { } userId)
        {
            return Results.Unauthorized();
        }
        if (await RequireFounderAsync(households, id, userId, cancellationToken) is { } refused)
        {
            return refused;
        }
        var email = mailer.IsOn ? AccountValidation.Clean(request?.Email) : "";
        if (email.Length > 0 && AccountValidation.CheckEmail(email) is { } emailError)
        {
            return ApiErrors.BadRequest(emailError);
        }
        if (!links.IsConfigured)
        {
            loggers.CreateLogger(LogCategory).LogWarning(
                "Einladung in Haushalt {HouseholdId} abgelehnt: {Setting} fehlt.", id, PublicLinks.SettingKey);
            return Conflict(ErrorCodes.PublicUrlMissing);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await households.LockAsync(id, cancellationToken);
        // Auf Mikrosekunden gekürzt, so genau speichert PostgreSQL: Die Antwort nennt dann dieselbe
        // Ablaufzeit wie später die Liste und die Vorschau.
        var now = time.GetUtcNow();
        now = now.AddTicks(-(now.Ticks % 10));
        var open = await db.HouseholdInvitations
            .CountAsync(i => i.HouseholdId == id && i.Status == InvitationStatus.Pending && i.ExpiresAt > now, cancellationToken);
        if (open >= HouseholdRules.MaxOpenInvitations)
        {
            return Conflict(ErrorCodes.TooManyInvitations);
        }

        var invitation = HouseholdInvitation.Create(id, userId, now);
        db.HouseholdInvitations.Add(invitation);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Das Token selbst kommt nie ins Protokoll; es ist der Schlüssel zum Haushalt.
        loggers.CreateLogger(LogCategory).LogInformation(
            "Einladung {InvitationId} in Haushalt {HouseholdId} von {UserId} angelegt.", invitation.Id, id, userId);
        if (email.Length > 0)
        {
            await MailInvitationAsync(invitation, email, userId, users, db, links, mailer, throttle, cancellationToken);
        }
        return Results.Created($"/api/v1/households/{id}/invitations/{invitation.Id}", ToResponse(invitation, links));
    }

    /// <summary>Schickt den Link der Einladung an die Adresse, wenn sie zu einem bestätigten Konto gehört (Spec 53, R6).</summary>
    private static async Task MailInvitationAsync(
        HouseholdInvitation invitation,
        string email,
        Guid inviterId,
        UserManager<AppUser> users,
        DuofyDbContext db,
        PublicLinks links,
        Mailer mailer,
        MailThrottle throttle,
        CancellationToken cancellationToken)
    {
        if (await users.FindByEmailAsync(email) is not { EmailConfirmed: true } invitee
            || !throttle.TryTake(MailEndpoints.Purposes.HouseholdInvitation, email))
        {
            return;
        }
        var household = await db.Households.Where(h => h.Id == invitation.HouseholdId).Select(h => h.Name).SingleAsync(cancellationToken);
        var inviter = await db.Users.Where(u => u.Id == inviterId).Select(u => u.FirstName + " " + u.LastName).SingleAsync(cancellationToken);
        mailer.Queue(
            MailTexts.HouseholdInvitation(invitee.Email!, inviter, household, links.Invitation(invitation.Token)!),
            MailEndpoints.Purposes.HouseholdInvitation);
    }

    /// <summary>Zieht eine Einladung zurück; der Link gilt danach nicht mehr (R4).</summary>
    private static async Task<IResult> RevokeInvitationAsync(
        Guid id,
        Guid invitationId,
        ClaimsPrincipal principal,
        UserManager<AppUser> users,
        HouseholdStore households,
        DuofyDbContext db,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (UserOf(principal, users) is not { } userId)
        {
            return Results.Unauthorized();
        }
        if (await RequireFounderAsync(households, id, userId, cancellationToken) is { } refused)
        {
            return refused;
        }

        // Erst den Haushalt sperren wie beim Anlegen und Beitreten, dann die Einladung lesen (A22): Nimmt Ben sie
        // gerade an, wartet das Zurückziehen, bis er fertig ist, und findet sie danach angenommen. Ohne die Sperre
        // läse es sie noch offen und überschriebe danach "angenommen" mit "zurückgezogen".
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await households.LockAsync(id, cancellationToken);
        var invitation = await db.HouseholdInvitations
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.HouseholdId == id, cancellationToken);
        if (invitation is null)
        {
            return NotFound(ErrorCodes.InvitationNotFound);
        }

        // Eine schon benutzte Einladung bleibt "angenommen"; zurückziehen ändert an ihr nichts mehr.
        if (invitation.Status == InvitationStatus.Pending)
        {
            invitation.Revoke();
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            loggers.CreateLogger(LogCategory).LogInformation(
                "Einladung {InvitationId} in Haushalt {HouseholdId} von {UserId} zurückgezogen.", invitation.Id, id, userId);
        }
        return Results.NoContent();
    }
}
