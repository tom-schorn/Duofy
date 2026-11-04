using Duofy.ApiClient;
using Duofy.Schemas;
using Duofy.Shared.Services;

namespace Duofy.Shared.Tests;

/// <summary>
/// Eine Haushalts-API im Speicher: Sie hält den Haushalt der angemeldeten Person, den der Test vorgibt
/// (null heißt: in keinem Haushalt), und merkt sich jeden Aufruf. Änderungen wirken auf den gespeicherten Haushalt so, wie die echte API
/// es täte, damit die Seite nach dem Neuladen den neuen Stand zeigt.
/// </summary>
public sealed partial class FakeHouseholdApi : IHouseholdApi
{
    public static readonly Guid AnnaId = Guid.NewGuid();
    public static readonly Guid BenId = Guid.NewGuid();

    public static MemberResponse Anna(string role = HouseholdRoles.Founder, bool isMe = true) =>
        new(AnnaId, "Anna", "Muster", "anna@example.org", role, isMe, GrantLevels.None, GrantLevels.None);

    public static MemberResponse Ben(string role = HouseholdRoles.Member, bool isMe = false) =>
        new(BenId, "Ben", "Beispiel", "ben@example.org", role, isMe, GrantLevels.None, GrantLevels.None);

    /// <summary>Die öffentliche Adresse, mit der die API die Links baut, wie Duofy:PublicUrl auf dem Server.</summary>
    public const string PublicUrl = "https://duofy.example.de";

    /// <summary>Hält die Antwort auf GetMineAsync zurück, wie ein echter Server beim ersten Laden der Seite.</summary>
    public Task? MineGate { get; set; }

    public HouseholdResponse? Household { get; set; } = new(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20, [Anna()]);

    /// <summary>Der Haushalt, den das Beitreten liefert und danach als eigener gilt.</summary>
    public HouseholdResponse? Joined { get; set; }

    public List<InvitationResponse> Invitations { get; } = [];
    public InvitationPreview? Preview { get; set; } = new("Haushalt von Anna", "Anna Muster", new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));

    public int MineCalls { get; private set; }
    public int ListInvitationsCalls { get; private set; }
    public List<string?> Founded { get; } = [];
    public List<UpdateHouseholdRequest> Updates { get; } = [];
    public List<Guid> Removed { get; } = [];
    public int Leaves { get; private set; }
    public int CreatedInvitations { get; private set; }
    public List<Guid> Revoked { get; } = [];
    public List<string> Previewed { get; } = [];
    public List<string> Accepted { get; } = [];

    /// <summary>Wenn gesetzt, scheitert jeder ändernde Aufruf mit diesem Code.</summary>
    public string? Error { get; set; }

    /// <summary>Wenn gesetzt, scheitert die Vorschau mit diesem Code.</summary>
    public string? PreviewError { get; set; }

    public Task<ApiResult<HouseholdResponse>> CreateAsync(string? name, CancellationToken cancellationToken = default)
    {
        Founded.Add(name);
        if (Error is not null)
        {
            return Task.FromResult(ApiResult<HouseholdResponse>.Failure(Error));
        }
        Household = new HouseholdResponse(Guid.NewGuid(), name ?? "Haushalt von Anna", 50, 30, 20, [Anna()]);
        return Task.FromResult(ApiResult<HouseholdResponse>.Success(Household));
    }

    // Ohne Haushalt antwortet die API 404 no_household, der Client meldet das als NotFound.
    public async Task<ApiResult<HouseholdResponse>> GetMineAsync(CancellationToken cancellationToken = default)
    {
        MineCalls++;
        if (MineGate is { } gate)
        {
            await gate;
        }
        return Household is null
            ? ApiResult<HouseholdResponse>.Failure(ClientErrorCodes.NotFound)
            : ApiResult<HouseholdResponse>.Success(Household);
    }

    public Task<ApiResult<HouseholdResponse>> UpdateAsync(Guid householdId, UpdateHouseholdRequest request, CancellationToken cancellationToken = default)
    {
        Updates.Add(request);
        if (Error is not null)
        {
            return Task.FromResult(ApiResult<HouseholdResponse>.Failure(Error));
        }
        Household = Household! with
        {
            Name = request.Name ?? Household.Name,
            QuotaNeeds = request.QuotaNeeds ?? Household.QuotaNeeds,
            QuotaWants = request.QuotaWants ?? Household.QuotaWants,
            QuotaSavings = request.QuotaSavings ?? Household.QuotaSavings,
        };
        return Task.FromResult(ApiResult<HouseholdResponse>.Success(Household));
    }

    public Task<ApiResult> RemoveMemberAsync(Guid householdId, Guid userId, CancellationToken cancellationToken = default)
    {
        Removed.Add(userId);
        if (Error is not null)
        {
            return Task.FromResult(ApiResult.Failure(Error));
        }
        Household = Household! with { Members = Household.Members.Where(m => m.UserId != userId).ToList() };
        return Task.FromResult(ApiResult.Success());
    }

    public Task<ApiResult> LeaveAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        Leaves++;
        if (Error is not null)
        {
            return Task.FromResult(ApiResult.Failure(Error));
        }
        Household = null;
        return Task.FromResult(ApiResult.Success());
    }

    /// <summary>Jede gesetzte Freigabe: für wen und was geschickt wurde.</summary>
    public List<(Guid GranteeId, UpdateGrantsRequest Request)> GrantsSet { get; } = [];

    /// <summary>Wenn gesetzt, hält das Setzen der Freigaben an, bis der Test die Aufgabe beendet.</summary>
    public TaskCompletionSource? GrantsGate { get; set; }

    /// <summary>
    /// Setzt die Stufen wie die echte API: nur die geschickten Bereiche ändern sich, und die Antwort ist das
    /// Mitglied mit seinen neuen Stufen.
    /// </summary>
    public async Task<ApiResult<MemberResponse>> SetGrantsAsync(
        Guid householdId, Guid granteeId, UpdateGrantsRequest request, CancellationToken cancellationToken = default)
    {
        GrantsSet.Add((granteeId, request));
        if (GrantsGate is { } gate)
        {
            await gate.Task;
        }
        if (Error is not null)
        {
            return ApiResult<MemberResponse>.Failure(Error);
        }
        var member = Household!.Members.Single(m => m.UserId == granteeId);
        var levels = member.MyGrants with
        {
            Plan = request.Plan ?? member.MyGrants.Plan,
            Book = request.Book ?? member.MyGrants.Book,
            Accounts = request.Accounts ?? member.MyGrants.Accounts,
            Commitments = request.Commitments ?? member.MyGrants.Commitments,
            Savings = request.Savings ?? member.MyGrants.Savings,
            Reminders = request.Reminders ?? member.MyGrants.Reminders,
        };
        var changed = member with { MyGrants = levels };
        Household = Household with { Members = Household.Members.Select(m => m.UserId == granteeId ? changed : m).ToList() };
        return ApiResult<MemberResponse>.Success(changed);
    }

    public Task<ApiResult<IReadOnlyList<InvitationResponse>>> ListInvitationsAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        ListInvitationsCalls++;
        return Task.FromResult(ApiResult<IReadOnlyList<InvitationResponse>>.Success(Invitations.ToList()));
    }

    /// <summary>Die Adressen, die mit den Einladungen mitgingen (Spec 53), null ohne Adresse.</summary>
    public List<string?> InvitationEmails { get; } = [];

    public Task<ApiResult<InvitationResponse>> CreateInvitationAsync(Guid householdId, string? email = null, CancellationToken cancellationToken = default)
    {
        CreatedInvitations++;
        InvitationEmails.Add(email);
        if (Error is not null)
        {
            return Task.FromResult(ApiResult<InvitationResponse>.Failure(Error));
        }
        var token = $"token{CreatedInvitations}abcdefghijklmnopqrstuvwxyz0123456789_-";
        var invitation = new InvitationResponse(Guid.NewGuid(), token, new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero), $"{PublicUrl}/einladung/{token}");
        Invitations.Add(invitation);
        return Task.FromResult(ApiResult<InvitationResponse>.Success(invitation));
    }

    public Task<ApiResult> RevokeInvitationAsync(Guid householdId, Guid invitationId, CancellationToken cancellationToken = default)
    {
        Revoked.Add(invitationId);
        Invitations.RemoveAll(i => i.Id == invitationId);
        return Task.FromResult(ApiResult.Success());
    }

    public Task<ApiResult<InvitationPreview>> PreviewInvitationAsync(string token, CancellationToken cancellationToken = default)
    {
        Previewed.Add(token);
        return Task.FromResult(PreviewError is null && Preview is not null
            ? ApiResult<InvitationPreview>.Success(Preview)
            : ApiResult<InvitationPreview>.Failure(PreviewError ?? ClientErrorCodes.NotFound));
    }

    public Task<ApiResult<HouseholdResponse>> AcceptInvitationAsync(string token, CancellationToken cancellationToken = default)
    {
        Accepted.Add(token);
        if (Error is not null)
        {
            return Task.FromResult(ApiResult<HouseholdResponse>.Failure(Error));
        }
        Household = Joined ?? Household;
        return Task.FromResult(ApiResult<HouseholdResponse>.Success(Household));
    }
}

/// <summary>Merkt sich, was geteilt wurde, statt den Teilen-Dialog oder die Zwischenablage zu öffnen.</summary>
public sealed class FakeShareService : IShareService
{
    public List<(string Title, string Link)> Shared { get; } = [];

    public Task ShareAsync(string title, string link)
    {
        Shared.Add((title, link));
        return Task.CompletedTask;
    }
}
