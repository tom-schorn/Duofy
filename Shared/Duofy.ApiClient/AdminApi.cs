using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>Der Client für /api/v1/admin, siehe <see cref="IAdminApi"/>.</summary>
internal sealed class AdminApi(ApiConnection api) : IAdminApi
{
    private const string Invitations = "api/v1/admin/invitations";

    public async Task<ApiResult<IReadOnlyList<RegistrationInvitationResponse>>> ListInvitationsAsync(CancellationToken cancellationToken = default)
    {
        var result = await api.SendAsync<List<RegistrationInvitationResponse>>(HttpMethod.Get, Invitations, body: null, anonymous: false, cancellationToken);
        return result.Succeeded
            ? ApiResult<IReadOnlyList<RegistrationInvitationResponse>>.Success(result.Value!)
            : ApiResult<IReadOnlyList<RegistrationInvitationResponse>>.Failure(result.ErrorCode!);
    }

    public Task<ApiResult<RegistrationInvitationResponse>> CreateInvitationAsync(string? email, CancellationToken cancellationToken = default) =>
        api.SendAsync<RegistrationInvitationResponse>(
            HttpMethod.Post, Invitations, new CreateRegistrationInvitationRequest(email), anonymous: false, cancellationToken);

    public async Task<ApiResult<PasswordResetLinkResponse>> CreatePasswordResetLinkAsync(string email, CancellationToken cancellationToken = default)
    {
        var result = await api.SendAsync<PasswordResetLinkResponse>(
            HttpMethod.Post, "api/v1/admin/password-reset-links", new CreatePasswordResetLinkRequest(email), anonymous: false, cancellationToken);
        // 404 heißt hier: Zu der Adresse gibt es kein Konto; das darf nur der Administrator erfahren.
        return result.ErrorCode == ClientErrorCodes.NotFound
            ? ApiResult<PasswordResetLinkResponse>.Failure(ErrorCodes.UserNotFound)
            : result;
    }

    public Task<ApiResult> RevokeInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default) =>
        api.SendAsync(HttpMethod.Delete, $"{Invitations}/{invitationId}", body: null, anonymous: false, cancellationToken);
}
