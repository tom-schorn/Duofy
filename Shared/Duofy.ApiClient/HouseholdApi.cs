using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>Der Client für /api/v1/households und /api/v1/invitations, siehe <see cref="IHouseholdApi"/>.</summary>
internal sealed class HouseholdApi(ApiConnection api) : IHouseholdApi
{
    private static string Household(Guid id) => $"api/v1/households/{id}";

    private static string Invitation(string token) => $"api/v1/invitations/{Uri.EscapeDataString(token)}";

    public Task<ApiResult<HouseholdResponse>> CreateAsync(string? name, CancellationToken cancellationToken = default) =>
        api.SendAsync<HouseholdResponse>(HttpMethod.Post, "api/v1/households", new CreateHouseholdRequest(name), anonymous: false, cancellationToken);

    public Task<ApiResult<HouseholdResponse>> GetMineAsync(CancellationToken cancellationToken = default) =>
        api.SendAsync<HouseholdResponse>(HttpMethod.Get, "api/v1/households/mine", body: null, anonymous: false, cancellationToken);

    public Task<ApiResult<HouseholdResponse>> UpdateAsync(Guid householdId, UpdateHouseholdRequest request, CancellationToken cancellationToken = default) =>
        api.SendAsync<HouseholdResponse>(HttpMethod.Patch, Household(householdId), request, anonymous: false, cancellationToken);

    public Task<ApiResult> RemoveMemberAsync(Guid householdId, Guid userId, CancellationToken cancellationToken = default) =>
        api.SendAsync(HttpMethod.Delete, $"{Household(householdId)}/members/{userId}", body: null, anonymous: false, cancellationToken);

    public Task<ApiResult> LeaveAsync(Guid householdId, CancellationToken cancellationToken = default) =>
        api.SendAsync(HttpMethod.Delete, $"{Household(householdId)}/members/me", body: null, anonymous: false, cancellationToken);

    public Task<ApiResult<MemberResponse>> SetGrantsAsync(
        Guid householdId, Guid granteeId, UpdateGrantsRequest request, CancellationToken cancellationToken = default) =>
        api.SendAsync<MemberResponse>(HttpMethod.Put, $"{Household(householdId)}/grants/{granteeId}", request, anonymous: false, cancellationToken);

    public async Task<ApiResult<IReadOnlyList<InvitationResponse>>> ListInvitationsAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        var result = await api.SendAsync<List<InvitationResponse>>(
            HttpMethod.Get, $"{Household(householdId)}/invitations", body: null, anonymous: false, cancellationToken);
        return result.Succeeded
            ? ApiResult<IReadOnlyList<InvitationResponse>>.Success(result.Value!)
            : ApiResult<IReadOnlyList<InvitationResponse>>.Failure(result.ErrorCode!);
    }

    public Task<ApiResult<InvitationResponse>> CreateInvitationAsync(Guid householdId, string? email = null, CancellationToken cancellationToken = default) =>
        api.SendAsync<InvitationResponse>(HttpMethod.Post, $"{Household(householdId)}/invitations",
            string.IsNullOrWhiteSpace(email) ? null : new CreateHouseholdInvitationRequest(email.Trim()), anonymous: false, cancellationToken);

    public Task<ApiResult> RevokeInvitationAsync(Guid householdId, Guid invitationId, CancellationToken cancellationToken = default) =>
        api.SendAsync(HttpMethod.Delete, $"{Household(householdId)}/invitations/{invitationId}", body: null, anonymous: false, cancellationToken);

    public Task<ApiResult<HouseholdPlanResponse>> GetPlanAsync(Guid householdId, int year, int month, CancellationToken cancellationToken = default) =>
        api.SendAsync<HouseholdPlanResponse>(HttpMethod.Get, $"{Household(householdId)}/plans/{year}/{month}", body: null, anonymous: false, cancellationToken);

    public async Task<ApiResult<IReadOnlyList<HouseholdPlanMonth>>> ListPlansAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        var result = await api.SendAsync<List<HouseholdPlanMonth>>(
            HttpMethod.Get, $"{Household(householdId)}/plans", body: null, anonymous: false, cancellationToken);
        return result.Succeeded
            ? ApiResult<IReadOnlyList<HouseholdPlanMonth>>.Success(result.Value!)
            : ApiResult<IReadOnlyList<HouseholdPlanMonth>>.Failure(result.ErrorCode!);
    }

    // Ohne Anmeldung: Wer den Link öffnet, sieht die Vorschau schon vor dem Anmelden (R5).
    public Task<ApiResult<InvitationPreview>> PreviewInvitationAsync(string token, CancellationToken cancellationToken = default) =>
        api.SendAsync<InvitationPreview>(HttpMethod.Get, Invitation(token), body: null, anonymous: true, cancellationToken);

    public Task<ApiResult<HouseholdResponse>> AcceptInvitationAsync(string token, CancellationToken cancellationToken = default) =>
        api.SendAsync<HouseholdResponse>(HttpMethod.Post, $"{Invitation(token)}/accept", body: null, anonymous: false, cancellationToken);
}
