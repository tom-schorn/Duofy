using System.Net;
using Duofy.Schemas;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.ApiClient.Tests;

/// <summary>Der Client für den Haushalt (Spec 67): richtige Adressen, Inhalte und Fehlercodes.</summary>
public class HouseholdClientTests
{
    private static readonly Guid HouseholdId = Guid.NewGuid();

    private static readonly MemberResponse Anna = new(Guid.NewGuid(), "Anna", "Muster", "anna@example.org", HouseholdRoles.Founder, true, GrantLevels.None, GrantLevels.None);

    private static readonly HouseholdResponse Household = new(HouseholdId, "Haushalt von Anna", 50, 30, 20, [Anna]);

    private static (FakeApi Api, ServiceProvider Provider, IServiceScope Scope) Start(
        Func<SeenRequest, HttpResponseMessage> respond, AuthTokens? tokens = null)
    {
        var api = new FakeApi { Respond = respond };
        var provider = TestHost.Create(api, new InMemoryTokenStore { Tokens = tokens ?? new AuthTokens("access", "refresh") });
        return (api, provider, provider.CreateScope());
    }

    [Fact]
    public async Task Mine_reads_the_own_household()
    {
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.OK, Household));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IHouseholdApi>().GetMineAsync();

        Assert.Equal("Haushalt von Anna", result.Value!.Name);
        var request = Assert.Single(api.Requests);
        Assert.Equal((HttpMethod.Get, "/api/v1/households/mine", "Bearer access"), (request.Method, request.Path, request.Authorization));
    }

    // Ohne Haushalt antwortet die API 404 no_household; der Client meldet das wie jedes 404 als NotFound.
    [Fact]
    public async Task Mine_without_a_household_is_not_found()
    {
        var (_, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.NotFound, new ErrorResponse(ErrorCodes.NoHousehold)));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IHouseholdApi>().GetMineAsync();

        Assert.Equal(ClientErrorCodes.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Founding_posts_the_name()
    {
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.Created, Household));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IHouseholdApi>().CreateAsync("Familie Muster");

        Assert.Equal(HouseholdId, result.Value!.Id);
        var request = Assert.Single(api.Requests);
        Assert.Equal((HttpMethod.Post, "/api/v1/households", "Bearer access"), (request.Method, request.Path, request.Authorization));
        Assert.Contains("\"name\":\"Familie Muster\"", request.Body);
    }

    [Fact]
    public async Task Changes_members_and_invitations_go_to_their_addresses()
    {
        var invitation = new InvitationResponse(Guid.NewGuid(), "tok_en-1", DateTimeOffset.UtcNow.AddDays(14), "https://duofy.example.de/einladung/tok_en-1");
        var (api, provider, scope) = Start(request => request.Method == HttpMethod.Delete
            ? new HttpResponseMessage(HttpStatusCode.NoContent)
            : request.Path.EndsWith("/invitations") && request.Method == HttpMethod.Get ? FakeApi.Json(HttpStatusCode.OK, new[] { invitation })
            : request.Path.EndsWith("/invitations") ? FakeApi.Json(HttpStatusCode.Created, invitation)
            : FakeApi.Json(HttpStatusCode.OK, Household));
        using var _p = provider;
        using var _s = scope;
        var households = scope.ServiceProvider.GetRequiredService<IHouseholdApi>();
        var ben = Guid.NewGuid();

        await households.UpdateAsync(HouseholdId, new UpdateHouseholdRequest(QuotaNeeds: 60, QuotaWants: 20, QuotaSavings: 20));
        await households.RemoveMemberAsync(HouseholdId, ben);
        await households.LeaveAsync(HouseholdId);
        var open = await households.ListInvitationsAsync(HouseholdId);
        var created = await households.CreateInvitationAsync(HouseholdId);
        await households.RevokeInvitationAsync(HouseholdId, invitation.Id);
        await households.AcceptInvitationAsync("tok_en-1");

        var seen = api.Requests.Select(r => (r.Method.Method, r.Path)).ToList();
        Assert.Equal(
        [
            ("PATCH", $"/api/v1/households/{HouseholdId}"),
            ("DELETE", $"/api/v1/households/{HouseholdId}/members/{ben}"),
            ("DELETE", $"/api/v1/households/{HouseholdId}/members/me"),
            ("GET", $"/api/v1/households/{HouseholdId}/invitations"),
            ("POST", $"/api/v1/households/{HouseholdId}/invitations"),
            ("DELETE", $"/api/v1/households/{HouseholdId}/invitations/{invitation.Id}"),
            ("POST", "/api/v1/invitations/tok_en-1/accept"),
        ], seen);
        Assert.Contains("\"quotaNeeds\":60", api.Requests[0].Body);
        Assert.Equal("tok_en-1", Assert.Single(open.Value!).Token);
        Assert.Equal(invitation.Id, created.Value!.Id);
        Assert.Equal("https://duofy.example.de/einladung/tok_en-1", created.Value!.Url);
        Assert.All(api.Requests, r => Assert.Equal("Bearer access", r.Authorization));
    }

    [Fact]
    public async Task The_preview_works_without_sign_in()
    {
        var preview = new InvitationPreview("Haushalt von Anna", "Anna Muster", DateTimeOffset.UtcNow.AddDays(14));
        var api = new FakeApi { Respond = _ => FakeApi.Json(HttpStatusCode.OK, preview) };
        using var provider = TestHost.Create(api, new InMemoryTokenStore());
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IHouseholdApi>().PreviewInvitationAsync("tok");

        Assert.Equal(preview, result.Value);
        var request = Assert.Single(api.Requests);
        Assert.Equal(("/api/v1/invitations/tok", null), (request.Path, request.Authorization));
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, ErrorCodes.InvitationExpired, ErrorCodes.InvitationExpired)]
    [InlineData(HttpStatusCode.Conflict, ErrorCodes.AlreadyInHousehold, ErrorCodes.AlreadyInHousehold)]
    [InlineData(HttpStatusCode.NotFound, ErrorCodes.InvitationNotFound, ClientErrorCodes.NotFound)]
    public async Task Errors_give_their_codes(HttpStatusCode status, string code, string expected)
    {
        var (_, provider, scope) = Start(_ => FakeApi.Json(status, new ErrorResponse(code)));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<IHouseholdApi>().AcceptInvitationAsync("tok");

        Assert.Equal(expected, result.ErrorCode);
    }
}
