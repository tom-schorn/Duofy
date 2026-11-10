namespace Duofy.App.Tests;

/// <summary>
/// A25: Die README erklärt, wie man die App auf dem eigenen Android-Telefon über WLAN testet.
/// </summary>
public class ReadmeTests
{
    [Fact]
    public void Readme_has_section_for_testing_on_an_android_phone_over_wifi_with_the_three_steps()
    {
        var readme = File.ReadAllText(RepositoryFiles.Path("README.md"));

        var start = readme.IndexOf("## Die Android-App auf dem eigenen Telefon testen", StringComparison.Ordinal);
        Assert.True(start >= 0, "Abschnitt fehlt");
        var next = readme.IndexOf("\n## ", start + 5, StringComparison.Ordinal);
        var section = next < 0 ? readme[start..] : readme[start..next];

        Assert.Contains("1. **Pair**", section);
        Assert.Contains("2. **Start the API**", section);
        Assert.Contains("3. **Install the app**", section);
        Assert.Contains("adb pair", section);
        Assert.Contains("adb connect", section);
        Assert.Contains("dotnet run --project Backend/Duofy.Backend --urls http://0.0.0.0:5080", section);
        Assert.Contains("dotnet build Mobile/Duofy -f net10.0-android -t:Run", section);
        Assert.Contains("appsettings.local.json", section);
    }
}
