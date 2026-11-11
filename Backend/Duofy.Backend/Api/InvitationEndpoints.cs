using System.Security.Claims;
using Duofy.Schemas;
using Duofy.Data;
using Duofy.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api;

/// <summary>
/// Ein Einladungslink (/api/v1/invitations/{token}, Spec 67, R5 und R6): die Vorschau ohne Anmeldung
/// und das Beitreten.
///
/// Wer den Link hat, darf beitreten; das ist Absicht. Die Vorschau zeigt deshalb nur, was jemand vor dem
/// Beitreten wissen muss (Haushalt, wer einlädt, bis wann), nie die Mitglieder. Sie geht durch dieselbe
/// Bremse wie die Anmeldung, damit niemand ungebremst Tokens durchprobiert.
/// </summary>
internal static class InvitationEndpoints
{
    private const string LogCategory = "Duofy.Api.Invitations";

    public static void MapInvitationEndpoints(this IEndpointRouteBuilder v1)
    {
        var invitations = v1.MapGroup("/invitations/{token}");
        invitations.MapGet("", PreviewAsync).RequireRateLimiting(AuthEndpoints.RateLimitPolicy);
        invitations.MapPost("/accept", AcceptAsync).RequireAuthorization();
    }

    /// <summary>Null, wenn die Einladung offen ist; sonst die Antwort 404 oder 409 mit dem passenden Code.</summary>
    private static IResult? Refusal(HouseholdInvitation? invitation, DateTimeOffset now) => invitation?.StateAt(now) switch
    {
        null => HouseholdEndpoints.NotFound(ErrorCodes.InvitationNotFound),
        InvitationState.Open => null,
        InvitationState.Expired => HouseholdEndpoints.Conflict(ErrorCodes.InvitationExpired),
        _ => HouseholdEndpoints.Conflict(ErrorCodes.InvitationNotOpen),
    };

    private static async Task<IResult> PreviewAsync(
        string token, DuofyDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        var invitation = await db.HouseholdInvitations.AsNoTracking().FirstOrDefaultAsync(i => i.Token == token, cancellationToken);
        if (Refusal(invitation, time.GetUtcNow()) is { } refused)
        {
            return refused;
        }

        var householdName = await db.Households
            .Where(h => h.Id == invitation!.HouseholdId)
            .Select(h => h.Name)
            .SingleAsync(cancellationToken);
        var inviter = await db.Users
            .Where(u => u.Id == invitation!.CreatedById)
            .Select(u => new { u.FirstName, u.LastName })
            .SingleAsync(cancellationToken);
        return Results.Ok(new InvitationPreview(householdName, $"{inviter.FirstName} {inviter.LastName}", invitation!.ExpiresAt));
    }

    /// <summary>
    /// Tritt bei (R6): Das geht nur, solange die Person in keinem Haushalt ist; sie wird Mitglied, die
    /// Einladung ist verbraucht. Konten, Verträge, Pläne und Buchungen bleiben, wo sie sind.
    /// </summary>
    private static async Task<IResult> AcceptAsync(
        string token,
        ClaimsPrincipal principal,
        UserManager<AppUser> users,
        HouseholdStore households,
        DuofyDbContext db,
        TimeProvider time,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (HouseholdEndpoints.UserOf(principal, users) is not { } userId)
        {
            return Results.Unauthorized();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Erst den Haushalt sperren, dann die Einladung, in derselben Reihenfolge wie das Auflösen, das mit
        // dem Haushalt auch seine Einladungen löscht; sonst warteten beide aufeinander (Entscheidung 68).
        // Ist der Haushalt inzwischen aufgelöst, gibt es danach auch die Einladung nicht mehr.
        var householdId = await db.HouseholdInvitations
            .Where(i => i.Token == token)
            .Select(i => (Guid?)i.HouseholdId)
            .FirstOrDefaultAsync(cancellationToken);
        if (householdId is { } id)
        {
            await households.LockAsync(id, cancellationToken);
        }

        // Die Zeile der Einladung bleibt bis zum Ende gesperrt: Zwei, die denselben Link zur selben Zeit
        // annehmen, kommen nacheinander dran, und der zweite findet sie verbraucht.
        var invitation = await db.HouseholdInvitations
            .FromSqlInterpolated($"SELECT * FROM household_invitations WHERE token = {token} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);
        var now = time.GetUtcNow();
        if (Refusal(invitation, now) is { } refused)
        {
            return refused;
        }

        if (await households.IdOfAsync(userId, cancellationToken) is not null)
        {
            return HouseholdEndpoints.Conflict(ErrorCodes.AlreadyInHousehold);
        }

        db.HouseholdMembers.Add(new HouseholdMember
        {
            HouseholdId = invitation!.HouseholdId,
            UserId = userId,
            Role = HouseholdRole.Member,
            JoinedAt = now,
        });
        invitation.Accept(userId, now);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (HouseholdEndpoints.IsUniqueViolation(ex))
        {
            // Zur selben Zeit gegründet oder anderswo beigetreten: Der eindeutige Index auf der Person hält
            // es auf, und mit der Transaktion bleibt auch die Einladung offen.
            loggers.CreateLogger(LogCategory).LogInformation(ex, "{UserId} ist beim Beitreten schon in einem Haushalt.", userId);
            return HouseholdEndpoints.Conflict(ErrorCodes.AlreadyInHousehold);
        }
        await transaction.CommitAsync(cancellationToken);

        loggers.CreateLogger(LogCategory).LogInformation(
            "{UserId} ist mit Einladung {InvitationId} Haushalt {HouseholdId} beigetreten.", userId, invitation.Id, invitation.HouseholdId);
        var joined = await households.LoadAsync(invitation.HouseholdId, cancellationToken);
        return Results.Ok(await households.ResponseAsync(joined, userId, cancellationToken));
    }
}
