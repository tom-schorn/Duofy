using Duofy.ApiClient;
using Duofy.Schemas;

namespace Duofy.Shared.Tests;

/// <summary>Spielt die Auskunft des Servers (Spec 70); ohne Vorgabe ist die Registrierung offen und kein Code fällig.</summary>
public sealed class FakeInstanceApi : IInstanceApi
{
    public InstanceResponse Instance { get; set; } = new([SignInMethodNames.Password], RegistrationModes.Open, SetupPending: false, []);

    /// <summary>Wenn gesetzt, scheitert die Auskunft mit diesem Fehlercode.</summary>
    public string? Failure { get; set; }

    public Task<ApiResult<InstanceResponse>> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Failure is not null
            ? ApiResult<InstanceResponse>.Failure(Failure)
            : ApiResult<InstanceResponse>.Success(Instance));
}

/// <summary>Spielt die Verwaltung des Servers: merkt sich angelegte und zurückgezogene Einladungen.</summary>
public sealed class FakeAdminApi : IAdminApi
{
    public const string PublicUrl = "https://duofy.example";

    public List<RegistrationInvitationResponse> Invitations { get; } = [];
    public List<string?> Created { get; } = [];
    public List<Guid> Revoked { get; } = [];

    /// <summary>Wenn gesetzt, scheitern alle Aufrufe mit diesem Fehlercode.</summary>
    public string? Failure { get; set; }

    public Task<ApiResult<IReadOnlyList<RegistrationInvitationResponse>>> ListInvitationsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Failure is not null
            ? ApiResult<IReadOnlyList<RegistrationInvitationResponse>>.Failure(Failure)
            : ApiResult<IReadOnlyList<RegistrationInvitationResponse>>.Success(Invitations.ToList()));

    public Task<ApiResult<RegistrationInvitationResponse>> CreateInvitationAsync(string? email, CancellationToken cancellationToken = default)
    {
        Created.Add(email);
        if (Failure is not null)
        {
            return Task.FromResult(ApiResult<RegistrationInvitationResponse>.Failure(Failure));
        }
        var token = $"tok{Created.Count}";
        var invitation = new RegistrationInvitationResponse(Guid.NewGuid(), token, email,
            new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero), $"{PublicUrl}/registrieren?einladung={token}");
        // Die neueste zuerst, wie die API.
        Invitations.Insert(0, invitation);
        return Task.FromResult(ApiResult<RegistrationInvitationResponse>.Success(invitation));
    }

    public List<string> ResetLinksFor { get; } = [];

    /// <summary>Die Adressen, zu denen es ein Konto gibt; für alle anderen antwortet der Fake user_not_found.</summary>
    public HashSet<string> KnownEmails { get; } = new(StringComparer.OrdinalIgnoreCase) { "anna@example.org" };

    public Task<ApiResult<PasswordResetLinkResponse>> CreatePasswordResetLinkAsync(string email, CancellationToken cancellationToken = default)
    {
        ResetLinksFor.Add(email);
        if (Failure is not null)
        {
            return Task.FromResult(ApiResult<PasswordResetLinkResponse>.Failure(Failure));
        }
        if (!KnownEmails.Contains(email))
        {
            return Task.FromResult(ApiResult<PasswordResetLinkResponse>.Failure(ErrorCodes.UserNotFound));
        }
        return Task.FromResult(ApiResult<PasswordResetLinkResponse>.Success(new PasswordResetLinkResponse(
            $"{PublicUrl}/passwort-neu?link=reset{ResetLinksFor.Count}", new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero))));
    }

    public Task<ApiResult> RevokeInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        Revoked.Add(invitationId);
        Invitations.RemoveAll(i => i.Id == invitationId);
        return Task.FromResult(Failure is not null ? ApiResult.Failure(Failure) : ApiResult.Success());
    }
}
