using System.Net;
using System.Text.Json;
using Duofy.Schemas;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.ApiClient.Tests;

/// <summary>Die Clients für den Server (Spec 70): Auskunft, Registrierung mit Code und Einladung, Admin werden, Einladungen.</summary>
public class InstanceClientTests
{
    private static (FakeApi Api, ServiceProvider Provider, IServiceScope Scope) Start(
        Func<SeenRequest, HttpResponseMessage> respond, AuthTokens? tokens = null)
    {
        var api = new FakeApi { Respond = respond };
        var provider = TestHost.Create(api, new InMemoryTokenStore { Tokens = tokens });
        return (api, provider, provider.CreateScope());
    }

    private static readonly AuthTokens SignedIn = new("access", "refresh");

    [Fact]
    public async Task Instance_reads_mode_and_setup_pending_without_sending_the_token()
    {
        var (api, provider, scope) = Start(
            _ => FakeApi.Json(HttpStatusCode.OK, new InstanceResponse(["password"], RegistrationModes.Invite, SetupPending: true, ["imprint"])),
            SignedIn);
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IInstanceApi>().GetAsync();

        Assert.Equal((RegistrationModes.Invite, true), (result.Value!.RegistrationMode, result.Value.SetupPending));
        var request = Assert.Single(api.Requests);
        Assert.Equal((HttpMethod.Get, "/api/v1/instance", (string?)null), (request.Method, request.Path, request.Authorization));
    }

    [Fact]
    public async Task Register_sends_setup_code_and_invitation_token()
    {
        var (api, provider, scope) = Start(_ => new HttpResponseMessage(HttpStatusCode.Created));
        using var _p = provider;
        using var _s = scope;

        await scope.ServiceProvider.GetRequiredService<IAuthApi>().RegisterAsync(
            new RegisterRequest("Anna", "Muster", "anna@example.de", "zehn-zeich", true, "s3cret", "tok"));

        using var body = JsonDocument.Parse(Assert.Single(api.Requests).Body!);
        Assert.Equal("s3cret", body.RootElement.GetProperty("setupCode").GetString());
        Assert.Equal("tok", body.RootElement.GetProperty("invitationToken").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, ErrorCodes.SetupCodeInvalid)]
    [InlineData(HttpStatusCode.Forbidden, ErrorCodes.InvitationRequired)]
    [InlineData(HttpStatusCode.Forbidden, ErrorCodes.InvitationInvalid)]
    [InlineData(HttpStatusCode.Forbidden, ErrorCodes.RegistrationClosed)]
    public async Task Register_passes_the_refusal_codes_on(HttpStatusCode status, string code)
    {
        var (_, provider, scope) = Start(_ => FakeApi.Json(status, new ErrorResponse(code)));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IAuthApi>().RegisterAsync(
            new RegisterRequest("Anna", "Muster", "anna@example.de", "zehn-zeich", true));

        Assert.Equal(code, result.ErrorCode);
    }

    [Fact]
    public async Task Claim_admin_posts_the_code_and_returns_the_account()
    {
        var me = new MeResponse(Guid.NewGuid(), "tom@example.de", "Tom", "Muster", IsInstanceAdmin: true);
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.OK, me), SignedIn);
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IUserApi>().ClaimAdminAsync(new ClaimAdminRequest("s3cret"));

        Assert.True(result.Value!.IsInstanceAdmin);
        var request = Assert.Single(api.Requests);
        Assert.Equal((HttpMethod.Post, "/api/v1/users/me/claim-admin", "Bearer access"), (request.Method, request.Path, request.Authorization));
        Assert.Contains("s3cret", request.Body);
    }

    [Fact]
    public async Task Claim_admin_with_a_wrong_code_gives_setup_code_invalid()
    {
        var (_, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.BadRequest, new ErrorResponse(ErrorCodes.SetupCodeInvalid)), SignedIn);
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IUserApi>().ClaimAdminAsync(new ClaimAdminRequest("falsch"));

        Assert.Equal(ErrorCodes.SetupCodeInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task Admin_lists_creates_and_revokes_invitations_at_the_right_addresses()
    {
        var invitation = new RegistrationInvitationResponse(Guid.NewGuid(), "tok", "ben@example.de", DateTimeOffset.UtcNow.AddDays(7),
            "https://duofy.example/registrieren?einladung=tok");
        var (api, provider, scope) = Start(request => request.Method == HttpMethod.Get
            ? FakeApi.Json(HttpStatusCode.OK, new[] { invitation })
            : request.Method == HttpMethod.Post
                ? FakeApi.Json(HttpStatusCode.Created, invitation)
                : new HttpResponseMessage(HttpStatusCode.NoContent), SignedIn);
        using var _p = provider;
        using var _s = scope;
        var admin = scope.ServiceProvider.GetRequiredService<IAdminApi>();

        var listed = await admin.ListInvitationsAsync();
        var created = await admin.CreateInvitationAsync("ben@example.de");
        var revoked = await admin.RevokeInvitationAsync(invitation.Id);

        Assert.Equal([invitation], listed.Value!);
        Assert.Equal(invitation, created.Value);
        Assert.True(revoked.Succeeded);
        Assert.Equal(
            [(HttpMethod.Get, "/api/v1/admin/invitations"), (HttpMethod.Post, "/api/v1/admin/invitations"), (HttpMethod.Delete, $"/api/v1/admin/invitations/{invitation.Id}")],
            api.Requests.Select(r => (r.Method, r.Path)));
        Assert.Contains("ben@example.de", api.Requests[1].Body);
    }

    [Fact]
    public async Task Admin_endpoints_for_somebody_else_give_admin_required()
    {
        var (_, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.Forbidden, new ErrorResponse(ErrorCodes.AdminRequired)), SignedIn);
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IAdminApi>().ListInvitationsAsync();

        Assert.Equal(ErrorCodes.AdminRequired, result.ErrorCode);
    }
}
