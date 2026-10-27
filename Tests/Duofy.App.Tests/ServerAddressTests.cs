using System.Text.Json;

namespace Duofy.App.Tests;

/// <summary>
/// Spec 61, Nachtrag 2026-10-27: Der gehostete Dienst der App ist Plagevo unter https://api.plagevo.de/. Nur ein
/// Debug-Build spricht weiter mit der API auf dem PC (appsettings.Debug.json).
/// </summary>
public class ServerAddressTests
{
    private static string BaseAddress(string file)
    {
        using var settings = JsonDocument.Parse(File.ReadAllText(RepositoryFiles.Path("Mobile", "Duofy", file)));
        return settings.RootElement.GetProperty("Api").GetProperty("BaseAddress").GetString()!;
    }

    [Fact]
    public void The_release_app_uses_plagevo_as_the_hosted_service()
    {
        Assert.Equal("https://api.plagevo.de/", BaseAddress("appsettings.json"));
    }

    [Fact]
    public void A_debug_build_keeps_the_local_api()
    {
        Assert.Equal("http://10.0.2.2:5080/", BaseAddress("appsettings.Debug.json"));
    }
}
