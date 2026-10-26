using System.Security.Claims;
using Duofy.Schemas;
using Duofy.Data;
using Duofy.Domain;
using Microsoft.AspNetCore.Identity;

namespace Duofy.Api;

/// <summary>
/// Der Haushaltsplan (/api/v1/households/{id}/plans, Spec 69): ein Monat und die Liste der Monate, nur zum Lesen.
/// Es gibt keinen Weg zum Schreiben; wer etwas an den Posten eines anderen Mitglieds ändern will, geht über dessen
/// eigenen Plan und braucht dessen Freigaben (Spec 68). Wer nicht im Haushalt ist, bekommt 404 household_not_found,
/// wie bei jedem anderen Weg des Haushalts.
/// </summary>
internal static class HouseholdPlanEndpoints
{
    public static void MapHouseholdPlanEndpoints(this IEndpointRouteBuilder v1)
    {
        var plans = v1.MapGroup("/households/{id:guid}/plans").RequireAuthorization();
        plans.MapGet("", ListMonthsAsync);
        plans.MapGet("/{year:int}/{month:int}", GetAsync);
    }

    private static bool IsValidMonth(int year, int month) =>
        month is >= 1 and <= 12 && year is >= PlanRules.FirstYear and <= PlanRules.LastYear;

    /// <summary>Der Haushalt, wenn die Person Mitglied ist; sonst die Antwort 401 oder 404.</summary>
    private static async Task<(Household? Household, Guid UserId, IResult? Denied)> MembershipAsync(
        Guid id, ClaimsPrincipal principal, UserManager<AppUser> users, HouseholdStore households, CancellationToken cancellationToken)
    {
        if (HouseholdEndpoints.UserOf(principal, users) is not { } userId)
        {
            return (null, Guid.Empty, Results.Unauthorized());
        }
        if (await households.RoleOfAsync(id, userId, cancellationToken) is null)
        {
            return (null, userId, HouseholdEndpoints.NotFound());
        }
        return (await households.LoadAsync(id, cancellationToken), userId, null);
    }

    /// <summary>Der Haushaltsplan des Monats (R1 bis R4, R8); legt die fehlenden Monate der gezählten Mitglieder an (R2).</summary>
    private static async Task<IResult> GetAsync(
        Guid id,
        int year,
        int month,
        ClaimsPrincipal principal,
        UserManager<AppUser> users,
        HouseholdStore households,
        HouseholdPlanStore composer,
        CancellationToken cancellationToken)
    {
        var (household, userId, denied) = await MembershipAsync(id, principal, users, households, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }
        if (!IsValidMonth(year, month))
        {
            return ApiErrors.BadRequest(ErrorCodes.MonthInvalid);
        }
        return Results.Ok(await composer.ComposeAsync(household!, userId, year, month, cancellationToken));
    }

    /// <summary>Die Monate, für die es einen Haushaltsplan gibt, der neueste zuerst (R7).</summary>
    private static async Task<IResult> ListMonthsAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<AppUser> users,
        HouseholdStore households,
        HouseholdPlanStore composer,
        CancellationToken cancellationToken)
    {
        var (household, _, denied) = await MembershipAsync(id, principal, users, households, cancellationToken);
        return denied ?? Results.Ok(await composer.MonthsAsync(household!, cancellationToken));
    }
}
