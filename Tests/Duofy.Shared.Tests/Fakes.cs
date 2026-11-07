using Duofy.ApiClient;
using Duofy.Schemas;

namespace Duofy.Shared.Tests;

// Einfache Ersatzteile für die Tests: Sie tun nichts im Netz, merken sich aber, was die
// Seiten aufgerufen haben, und liefern die Antworten, die der Test vorgibt.

public sealed class FakeTokenStore : ITokenStore
{
    public AuthTokens? Tokens { get; set; }

    public Task<AuthTokens?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Tokens);

    public Task SaveAsync(AuthTokens tokens, CancellationToken cancellationToken = default)
    {
        Tokens = tokens;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        Tokens = null;
        return Task.CompletedTask;
    }
}

public sealed class FakeAuthApi(FakeTokenStore store) : IAuthApi
{
    public List<RegisterRequest> Registered { get; } = [];
    public List<LoginRequest> LoggedIn { get; } = [];
    public int LogoutCalls { get; private set; }
    public int LogoutEverywhereCalls { get; private set; }

    public ApiResult RegisterResult { get; set; } = ApiResult.Success();
    public ApiResult LoginResult { get; set; } = ApiResult.Success();
    public ApiResult LogoutEverywhereResult { get; set; } = ApiResult.Success();

    public Task<ApiResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        Registered.Add(request);
        return Task.FromResult(RegisterResult);
    }

    public Task<ApiResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        LoggedIn.Add(request);
        if (LoginResult.Succeeded)
        {
            store.Tokens = new AuthTokens("access", "refresh");
        }
        return Task.FromResult(LoginResult);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        LogoutCalls++;
        await store.ClearAsync(cancellationToken);
    }

    public async Task<ApiResult> LogoutEverywhereAsync(CancellationToken cancellationToken = default)
    {
        LogoutEverywhereCalls++;
        if (!LogoutEverywhereResult.Succeeded)
        {
            return LogoutEverywhereResult;
        }
        await store.ClearAsync(cancellationToken);
        return LogoutEverywhereResult;
    }

    // Spec 53: Mails und Links. Jeder Aufruf landet in Calls; MailResult gilt für alle.
    public List<object> Calls { get; } = [];

    public ApiResult MailResult { get; set; } = ApiResult.Success();

    public Task<ApiResult> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken cancellationToken = default) => Record(request);

    public Task<ApiResult> ResendConfirmationAsync(ResendConfirmationRequest request, CancellationToken cancellationToken = default) => Record(request);

    public Task<ApiResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default) => Record(request);

    public Task<ApiResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default) => Record(request);

    public Task<ApiResult> ResetPasswordByLinkAsync(ResetPasswordByLinkRequest request, CancellationToken cancellationToken = default) => Record(request);

    public Task<ApiResult> ConfirmEmailChangeAsync(ConfirmEmailChangeRequest request, CancellationToken cancellationToken = default) => Record(request);

    private Task<ApiResult> Record(object request)
    {
        Calls.Add(request);
        return Task.FromResult(MailResult);
    }
}

public sealed class FakeUserApi(FakeTokenStore store) : IUserApi
{
    public MeResponse Me { get; set; } = new(Guid.NewGuid(), "anna@example.org", "Anna", "Muster");
    public List<UpdateMeRequest> Updated { get; } = [];
    public List<ChangePasswordRequest> PasswordChanges { get; } = [];
    public List<DeleteMeRequest> Deleted { get; } = [];

    public ApiResult UpdateResult { get; set; } = ApiResult.Success();
    public ApiResult ChangePasswordResult { get; set; } = ApiResult.Success();
    public ApiResult DeleteResult { get; set; } = ApiResult.Success();

    /// <summary>Ist es gesetzt, schlägt GetMeAsync mit diesem Fehlercode fehl.</summary>
    public string? MeFailure { get; set; }

    public Task<ApiResult<MeResponse>> GetMeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(MeFailure is null ? ApiResult<MeResponse>.Success(Me) : ApiResult<MeResponse>.Failure(MeFailure));

    public Task<ApiResult<MeResponse>> UpdateMeAsync(UpdateMeRequest request, CancellationToken cancellationToken = default)
    {
        Updated.Add(request);
        if (!UpdateResult.Succeeded)
        {
            return Task.FromResult(ApiResult<MeResponse>.Failure(UpdateResult.ErrorCode!));
        }
        Me = Me with { FirstName = request.FirstName, LastName = request.LastName };
        return Task.FromResult(ApiResult<MeResponse>.Success(Me));
    }

    public Task<ApiResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        PasswordChanges.Add(request);
        return Task.FromResult(ChangePasswordResult);
    }

    public List<ChangeEmailRequest> EmailChanges { get; } = [];

    /// <summary>True: Die Adresse gilt sofort (selbst betrieben); false: Sie wartet auf die Bestätigung (gehostet).</summary>
    public bool EmailChangesAtOnce { get; set; } = true;

    public string? ChangeEmailError { get; set; }

    public Task<ApiResult<MeResponse?>> ChangeEmailAsync(ChangeEmailRequest request, CancellationToken cancellationToken = default)
    {
        EmailChanges.Add(request);
        if (ChangeEmailError is not null)
        {
            return Task.FromResult(ApiResult<MeResponse?>.Failure(ChangeEmailError));
        }
        if (!EmailChangesAtOnce)
        {
            return Task.FromResult(ApiResult<MeResponse?>.Success(null));
        }
        Me = Me with { Email = request.NewEmail };
        return Task.FromResult(ApiResult<MeResponse?>.Success(Me));
    }

    public QuotaSettings Quotas { get; set; } = new(50, 30, 20);
    public List<QuotaSettings> QuotaUpdates { get; } = [];
    public string? QuotaError { get; set; }

    public Task<ApiResult<QuotaSettings>> GetQuotasAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult<QuotaSettings>.Success(Quotas));

    public Task<ApiResult<QuotaSettings>> UpdateQuotasAsync(QuotaSettings request, CancellationToken cancellationToken = default)
    {
        QuotaUpdates.Add(request);
        if (QuotaError is not null)
        {
            return Task.FromResult(ApiResult<QuotaSettings>.Failure(QuotaError));
        }
        Quotas = request;
        return Task.FromResult(ApiResult<QuotaSettings>.Success(Quotas));
    }

    public List<ClaimAdminRequest> Claims { get; } = [];

    /// <summary>Der Code, mit dem die Person Administrator wird; jeder andere ergibt setup_code_invalid.</summary>
    public string SetupCode { get; set; } = "s3cret";

    public Task<ApiResult<MeResponse>> ClaimAdminAsync(ClaimAdminRequest request, CancellationToken cancellationToken = default)
    {
        Claims.Add(request);
        if (request.SetupCode != SetupCode)
        {
            return Task.FromResult(ApiResult<MeResponse>.Failure(ErrorCodes.SetupCodeInvalid));
        }
        Me = Me with { IsInstanceAdmin = true };
        return Task.FromResult(ApiResult<MeResponse>.Success(Me));
    }

    public async Task<ApiResult> DeleteMeAsync(DeleteMeRequest request, CancellationToken cancellationToken = default)
    {
        Deleted.Add(request);
        if (DeleteResult.Succeeded)
        {
            await store.ClearAsync(cancellationToken);
        }
        return DeleteResult;
    }
}
