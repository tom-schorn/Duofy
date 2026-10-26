using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>Der Client für /api/v1/auth, siehe <see cref="IAuthApi"/>.</summary>
internal sealed class AuthApi : IAuthApi
{
    private readonly ApiConnection api;
    private readonly TokenSession session;

    public AuthApi(ApiConnection api, TokenSession session)
    {
        this.api = api;
        this.session = session;
    }

    public Task<ApiResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default) =>
        api.SendAsync(HttpMethod.Post, "api/v1/auth/register", request, anonymous: true, cancellationToken);

    public async Task<ApiResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var result = await api.SendAsync<TokenResponse>(
            HttpMethod.Post, "api/v1/auth/login", request, anonymous: true, cancellationToken);
        if (!result.Succeeded)
        {
            return result;
        }

        var tokens = result.Value!;
        try
        {
            await session.SaveAsync(new AuthTokens(tokens.AccessToken, tokens.RefreshToken), cancellationToken);
        }
        catch (TokenStoreUnavailableException ex)
        {
            // Die Ablage hat schon protokolliert, warum; die Oberfläche erklärt den Code.
            return ApiResult.Failure(ex.ErrorCode);
        }
        return ApiResult.Success();
    }

    public Task<ApiResult> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken cancellationToken = default) =>
        Anonymous("api/v1/auth/confirm-email", request, cancellationToken);

    public Task<ApiResult> ResendConfirmationAsync(ResendConfirmationRequest request, CancellationToken cancellationToken = default) =>
        Anonymous("api/v1/auth/confirm-email/resend", request, cancellationToken);

    public Task<ApiResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default) =>
        Anonymous("api/v1/auth/password/forgot", request, cancellationToken);

    public Task<ApiResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default) =>
        Anonymous("api/v1/auth/password/reset", request, cancellationToken);

    public Task<ApiResult> ResetPasswordByLinkAsync(ResetPasswordByLinkRequest request, CancellationToken cancellationToken = default) =>
        Anonymous("api/v1/auth/password/reset-by-link", request, cancellationToken);

    public Task<ApiResult> ConfirmEmailChangeAsync(ConfirmEmailChangeRequest request, CancellationToken cancellationToken = default) =>
        Anonymous("api/v1/auth/confirm-email-change", request, cancellationToken);

    // Die Wege aus einer Mail oder einem Link brauchen keine Anmeldung; wer sie öffnet, ist oft nicht angemeldet.
    private Task<ApiResult> Anonymous(string path, object request, CancellationToken cancellationToken) =>
        api.SendAsync(HttpMethod.Post, path, request, anonymous: true, cancellationToken);

    public Task LogoutAsync(CancellationToken cancellationToken = default) => session.ClearAsync(cancellationToken);

    public async Task<ApiResult> LogoutEverywhereAsync(CancellationToken cancellationToken = default)
    {
        var result = await api.SendAsync(
            HttpMethod.Post, "api/v1/auth/logout-everywhere", body: null, anonymous: false, cancellationToken);
        if (result.Succeeded)
        {
            await session.ClearAsync(cancellationToken);
        }
        return result;
    }
}
