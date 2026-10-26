using Duofy.Schemas;
using Microsoft.Extensions.Logging;

namespace Duofy.ApiClient;

/// <summary>Der Client für /api/v1/users/me, siehe <see cref="IUserApi"/>.</summary>
internal sealed class UserApi : IUserApi
{
    private const string Me = "api/v1/users/me";

    private readonly ApiConnection api;
    private readonly TokenSession session;
    private readonly ILogger<UserApi> logger;

    public UserApi(ApiConnection api, TokenSession session, ILogger<UserApi> logger)
    {
        this.api = api;
        this.session = session;
        this.logger = logger;
    }

    public Task<ApiResult<MeResponse>> GetMeAsync(CancellationToken cancellationToken = default) =>
        api.SendAsync<MeResponse>(HttpMethod.Get, Me, body: null, anonymous: false, cancellationToken);

    public Task<ApiResult<MeResponse>> UpdateMeAsync(UpdateMeRequest request, CancellationToken cancellationToken = default) =>
        api.SendAsync<MeResponse>(HttpMethod.Patch, Me, request, anonymous: false, cancellationToken);

    public async Task<ApiResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var result = await api.SendAsync(HttpMethod.Post, $"{Me}/password", request, anonymous: false, cancellationToken);
        if (result.Succeeded)
        {
            await SignInAgainAsync(request.NewPassword, cancellationToken);
        }
        return result;
    }

    /// <summary>
    /// Ein neues Passwort macht alle alten Refresh-Tokens ungültig, auch das dieses
    /// Geräts. Ohne neue Anmeldung wäre die Person hier nach spätestens 15 Minuten
    /// abgemeldet. Klappt die neue Anmeldung nicht, bleibt die Änderung trotzdem
    /// gültig; die Person meldet sich dann später mit dem neuen Passwort an.
    /// </summary>
    private async Task SignInAgainAsync(string newPassword, CancellationToken cancellationToken)
    {
        var me = await GetMeAsync(cancellationToken);
        if (!me.Succeeded)
        {
            logger.LogWarning("Nach der Passwortänderung kein Konto gelesen ({ErrorCode}).", me.ErrorCode);
            return;
        }
        await SignInAgainAsync(me.Value!.Email, newPassword, "Passwortänderung", cancellationToken);
    }

    // Meldet dieses Gerät mit Adresse und Passwort neu an und legt die Tokens ab.
    private async Task SignInAgainAsync(string email, string password, string reason, CancellationToken cancellationToken)
    {
        var login = await api.SendAsync<TokenResponse>(
            HttpMethod.Post, "api/v1/auth/login", new LoginRequest(email, password),
            anonymous: true, cancellationToken);
        if (!login.Succeeded)
        {
            logger.LogWarning("Neue Anmeldung nach der {Reason} fehlgeschlagen ({ErrorCode}).", reason, login.ErrorCode);
            return;
        }

        await session.SaveAsync(new AuthTokens(login.Value!.AccessToken, login.Value.RefreshToken), cancellationToken);
    }

    /// <summary>
    /// Wie beim Passwort: Eine sofort geänderte Adresse macht die alten Refresh-Tokens ungültig, also meldet sich
    /// dieses Gerät gleich mit der neuen Adresse neu an. Wartet die Änderung auf die Bestätigung (202 ohne Inhalt),
    /// bleibt alles, wie es ist.
    /// </summary>
    public async Task<ApiResult<MeResponse?>> ChangeEmailAsync(ChangeEmailRequest request, CancellationToken cancellationToken = default)
    {
        var result = await api.SendOptionalAsync<MeResponse>(HttpMethod.Post, $"{Me}/email", request, anonymous: false, cancellationToken);
        if (result.Value is { } me)
        {
            await SignInAgainAsync(me.Email, request.Password, "Änderung der Adresse", cancellationToken);
        }
        return result;
    }

    public Task<ApiResult<QuotaSettings>> GetQuotasAsync(CancellationToken cancellationToken = default) =>
        api.SendAsync<QuotaSettings>(HttpMethod.Get, $"{Me}/quotas", body: null, anonymous: false, cancellationToken);

    public Task<ApiResult<QuotaSettings>> UpdateQuotasAsync(QuotaSettings request, CancellationToken cancellationToken = default) =>
        api.SendAsync<QuotaSettings>(HttpMethod.Put, $"{Me}/quotas", request, anonymous: false, cancellationToken);

    public Task<ApiResult<MeResponse>> ClaimAdminAsync(ClaimAdminRequest request, CancellationToken cancellationToken = default) =>
        api.SendAsync<MeResponse>(HttpMethod.Post, $"{Me}/claim-admin", request, anonymous: false, cancellationToken);

    public async Task<ApiResult> DeleteMeAsync(DeleteMeRequest request, CancellationToken cancellationToken = default)
    {
        var result = await api.SendAsync(HttpMethod.Delete, Me, request, anonymous: false, cancellationToken);
        if (result.Succeeded)
        {
            await session.ClearAsync(cancellationToken);
        }
        return result;
    }
}
