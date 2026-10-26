using System.Net;
using Duofy.Schemas;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.ApiClient.Tests;

/// <summary>Der Client für die Rechtstexte (Spec 70, R7): richtige Adressen, ohne Anmeldung, Fehlercodes.</summary>
public class LegalClientTests
{
    private static (FakeApi Api, ServiceProvider Provider, IServiceScope Scope) Start(
        Func<SeenRequest, HttpResponseMessage> respond, AuthTokens? tokens = null)
    {
        var api = new FakeApi { Respond = respond };
        var provider = TestHost.Create(api, new InMemoryTokenStore { Tokens = tokens });
        return (api, provider, provider.CreateScope());
    }

    [Fact]
    public async Task List_reads_the_names_without_sending_the_token()
    {
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.OK, new LegalListResponse(["imprint", "terms"])),
            new AuthTokens("access", "refresh"));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<ILegalApi>().ListAsync();

        Assert.Equal(["imprint", "terms"], result.Value!.Documents);
        var request = Assert.Single(api.Requests);
        Assert.Equal((HttpMethod.Get, "/api/v1/legal", (string?)null), (request.Method, request.Path, request.Authorization));
    }

    [Fact]
    public async Task Get_reads_the_text_of_one_document_without_signing_in()
    {
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.OK, new LegalTextResponse("Max Muster\nMusterweg 1")));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<ILegalApi>().GetAsync(LegalDocumentNames.Imprint);

        Assert.Equal("Max Muster\nMusterweg 1", result.Value!.Text);
        var request = Assert.Single(api.Requests);
        Assert.Equal((HttpMethod.Get, "/api/v1/legal/imprint"), (request.Method, request.Path));
    }

    // Ein 404 kommt wie jedes andere als NotFound an; die Seite sagt dann, dass es den Text hier nicht gibt.
    [Fact]
    public async Task Get_of_a_missing_document_is_not_found()
    {
        var (_, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.NotFound, new ErrorResponse("legal_not_found")));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<ILegalApi>().GetAsync(LegalDocumentNames.Privacy);

        Assert.Equal(ClientErrorCodes.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Get_escapes_the_name_so_it_cannot_change_the_path()
    {
        var (api, provider, scope) = Start(_ => FakeApi.Json(HttpStatusCode.NotFound, new ErrorResponse("legal_not_found")));
        using var _p = provider;
        using var _s = scope;

        await scope.ServiceProvider.GetRequiredService<ILegalApi>().GetAsync("../users/me");

        var request = Assert.Single(api.Requests);
        Assert.Equal("/api/v1/legal/..%2Fusers%2Fme", request.Path);
    }

    [Fact]
    public async Task An_unreachable_server_is_reported_as_unreachable()
    {
        var (_, provider, scope) = Start(_ => throw new HttpRequestException("down"));
        using var _p = provider;
        using var _s = scope;

        var result = await scope.ServiceProvider.GetRequiredService<ILegalApi>().ListAsync();

        Assert.Equal(ClientErrorCodes.ServerUnreachable, result.ErrorCode);
    }
}
