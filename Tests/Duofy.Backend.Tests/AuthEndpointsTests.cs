using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>Wie lange eine Anmeldung hält: sieben Tage, und jede Nutzung verlängert sie (Spec 97).</summary>
[Collection(ApiCollection.Name)]
public class AuthEndpointsTests : IDisposable
{
    private readonly MovableTime clock = new(new DateTimeOffset(2026, 11, 5, 8, 0, 0, TimeSpan.Zero));
    private readonly WebApplicationFactory<Program> api;
    private readonly HttpClient client;

    public AuthEndpointsTests(ApiFixture fixture)
    {
        // Die Uhr der API steht in diesem Test still und wird von Hand weitergestellt.
        api = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<TimeProvider>(clock)));
        client = api.CreateClient();
    }

    public void Dispose()
    {
        client.Dispose();
        api.Dispose();
    }

    // A1
    [Fact]
    public async Task A1_a_refresh_token_older_than_seven_days_is_refused()
    {
        var (_, tokens) = await client.SignUpAsync();

        clock.Advance(TimeSpan.FromDays(8));
        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(tokens.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // A2
    [Fact]
    public async Task A2_a_refresh_within_seven_days_extends_the_sign_in()
    {
        var (_, tokens) = await client.SignUpAsync();

        // Tag 6: Das Token gilt noch und wird gegen ein neues getauscht.
        clock.Advance(TimeSpan.FromDays(6));
        var first = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var renewed = (await first.Content.ReadFromJsonAsync<TokenResponse>())!;

        // Tag 12: Das alte Token wäre seit Tag 7 abgelaufen, das neue gilt noch (bis Tag 13).
        clock.Advance(TimeSpan.FromDays(6));
        var second = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(renewed.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }
}
