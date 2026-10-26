using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>
/// Die Schlüssel, mit denen die API Tokens und Links schützt, überleben einen Neustart (Spec 79, R5): Sie liegen im
/// Ordner aus Duofy:KeysDirectory, im Container ein Volume. Sonst meldete jedes Update alle ab und machte jeden
/// verschickten Link ungültig.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class KeyRingTests(ApiFixture fixture) : IDisposable
{
    private readonly string keys = Directory.CreateTempSubdirectory("duofy-keys-").FullName;
    private readonly string otherKeys = Directory.CreateTempSubdirectory("duofy-keys-").FullName;

    // A3
    [Fact]
    public async Task A_refresh_token_of_the_first_run_works_in_the_second_with_the_same_keys()
    {
        var refreshToken = await RefreshTokenFromAFirstRunAsync(keys);

        await using var second = Api(keys);
        var refresh = await second.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(refreshToken));

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
    }

    // Die Gegenprobe: Mit anderen Schlüsseln gilt das Token nicht; der Test oben prüft also wirklich den Ordner.
    [Fact]
    public async Task A_refresh_token_does_not_work_with_other_keys()
    {
        var refreshToken = await RefreshTokenFromAFirstRunAsync(keys);

        await using var second = Api(otherKeys);
        var refresh = await second.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(refreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task The_keys_land_in_the_configured_directory()
    {
        await RefreshTokenFromAFirstRunAsync(keys);

        Assert.NotEmpty(Directory.GetFiles(keys, "key-*.xml"));
    }

    private async Task<string> RefreshTokenFromAFirstRunAsync(string directory)
    {
        await using var first = Api(directory);
        var (_, tokens) = await first.CreateClient().SignUpAsync();
        return tokens.RefreshToken;
    }

    private DuofyApiFactory Api(string directory) =>
        new(fixture.ConnectionString, authPermitLimit: 10_000, new Dictionary<string, string> { ["Duofy:KeysDirectory"] = directory });

    public void Dispose()
    {
        Directory.Delete(keys, recursive: true);
        Directory.Delete(otherKeys, recursive: true);
    }
}
