using System.Xml.Linq;

namespace Duofy.App.Tests;

/// <summary>
/// A22 und A24: Die Android-Einstellungen für Google Play stehen in den Projektdateien.
/// </summary>
public class AndroidProjectTests
{
    private static readonly XDocument Project = XDocument.Load(RepositoryFiles.Path("Mobile", "Duofy", "Duofy.csproj"));

    // Wert einer Eigenschaft, bei Bedarf nur aus Gruppen oder Zeilen, deren Condition den Text enthält.
    private static string? Property(string name, string? conditionPart = null) =>
        Project.Descendants(name)
            .Where(e => conditionPart is null
                ? e.Attribute("Condition") is null && e.Parent?.Attribute("Condition") is null
                : (((string?)e.Attribute("Condition") ?? "") + ((string?)e.Parent?.Attribute("Condition") ?? "")).Contains(conditionPart))
            .Select(e => e.Value.Trim())
            .FirstOrDefault();

    [Fact]
    public void App_targets_android_only()
    {
        Assert.Equal("net10.0-android", Property("TargetFrameworks"));
        Assert.DoesNotContain(Project.Descendants("TargetFrameworks"), e => e.Attribute("Condition") is not null);
    }

    [Fact]
    public void Package_name_is_de_tomschorn_duofy_and_display_name_is_plagevo()
    {
        // Der Paketname bleibt (er lässt sich nach der Veröffentlichung nicht ändern); unter dem Symbol steht Plagevo.
        Assert.Equal("de.tomschorn.duofy", Property("ApplicationId"));
        Assert.Equal("Plagevo", Property("ApplicationTitle"));
    }

    [Fact]
    public void Target_sdk_is_36_and_minimum_is_24()
    {
        Assert.Equal("24.0", Property("SupportedOSPlatformVersion"));

        var manifest = XDocument.Load(RepositoryFiles.Path("Mobile", "Duofy", "Platforms", "Android", "AndroidManifest.xml"));
        XNamespace android = "http://schemas.android.com/apk/res/android";
        var uses = manifest.Root!.Element("uses-sdk");
        Assert.NotNull(uses);
        Assert.Equal("24", (string?)uses!.Attribute(android + "minSdkVersion"));
        Assert.Equal("36", (string?)uses.Attribute(android + "targetSdkVersion"));
    }

    // Ohne Schlüsseldatei darf der Release-Build nicht an einem leeren Pfad scheitern (apksigner endet dann mit Code 2,
    // MSB6006): Die Signatur-Einstellungen gelten nur, wenn DUOFY_KEYSTORE_PATH gesetzt ist; sonst signiert das SDK mit dem
    // Debug-Schlüssel, nur zum Testen.
    [Fact]
    public void Release_signing_applies_only_when_a_keystore_path_is_set()
    {
        var signing = Project.Descendants("PropertyGroup")
            .Single(g => g.Elements("AndroidSigningKeyStore").Any());

        var condition = (string?)signing.Attribute("Condition") ?? "";
        Assert.Contains("Release", condition);
        Assert.Contains("DUOFY_KEYSTORE_PATH", condition);
        Assert.Contains("!= ''", condition);
        Assert.DoesNotContain(Project.Descendants("PropertyGroup").Where(g => g != signing), g => g.Elements("AndroidKeyStore").Any());
    }

    [Fact]
    public void Release_build_produces_aab_signed_from_environment_variables()
    {
        var release = Project.Descendants("PropertyGroup")
            .Where(g => ((string?)g.Attribute("Condition") ?? "").Contains("Release"))
            .ToList();
        Assert.NotEmpty(release);

        string? Get(string name) => release.SelectMany(g => g.Elements(name)).Select(e => e.Value.Trim()).FirstOrDefault();

        Assert.Equal("aab", Get("AndroidPackageFormat"));
        Assert.Equal("true", Get("AndroidKeyStore"));
        Assert.Equal("$(DUOFY_KEYSTORE_PATH)", Get("AndroidSigningKeyStore"));
        Assert.Equal("$(DUOFY_KEY_ALIAS)", Get("AndroidSigningKeyAlias"));
        Assert.Equal("env:DUOFY_KEYSTORE_PASSWORD", Get("AndroidSigningStorePass"));
        Assert.Equal("env:DUOFY_KEY_PASSWORD", Get("AndroidSigningKeyPass"));
    }

    [Fact]
    public void Debug_permits_plain_http_and_release_does_not()
    {
        var debugConfig = XDocument.Load(RepositoryFiles.Path("Mobile", "Duofy", "Platforms", "Android", "DebugResources", "xml", "network_security_config.xml"));
        var releaseConfig = XDocument.Load(RepositoryFiles.Path("Mobile", "Duofy", "Platforms", "Android", "Resources", "xml", "network_security_config.xml"));

        Assert.Equal("true", (string?)debugConfig.Root!.Element("base-config")?.Attribute("cleartextTrafficPermitted"));
        Assert.Equal("false", (string?)releaseConfig.Root!.Element("base-config")?.Attribute("cleartextTrafficPermitted"));

        var manifest = File.ReadAllText(RepositoryFiles.Path("Mobile", "Duofy", "Platforms", "Android", "AndroidManifest.xml"));
        Assert.Contains("android:networkSecurityConfig=\"@xml/network_security_config\"", manifest);
    }

    private const string StrictNetworkConfig = @"Platforms\Android\Resources\xml\network_security_config.xml";
    private const string DebugNetworkConfig = @"Platforms\Android\DebugResources\xml\network_security_config.xml";

    [Fact]
    public void Only_debug_swaps_in_the_permissive_network_config()
    {
        // Die Gruppe, die die Datei austauscht, gilt genau für Debug.
        var swap = Project.Descendants("ItemGroup")
            .Single(g => g.Elements("AndroidResource").Any(e => (string?)e.Attribute("Include") == DebugNetworkConfig));
        Assert.Equal("'$(Configuration)' == 'Debug'", (string?)swap.Attribute("Condition"));
        Assert.Contains(swap.Elements("AndroidResource"), e => (string?)e.Attribute("Remove") == StrictNetworkConfig);

        // Und so löst das Projekt die Datei je Konfiguration auf.
        Assert.Equal([DebugNetworkConfig], NetworkConfigFor("Debug"));
        Assert.Equal([StrictNetworkConfig], NetworkConfigFor("Release"));
    }

    // Spielt die AndroidResource-Zeilen für eine Konfiguration durch. Die strenge Datei
    // ist von Anfang an dabei, weil MAUI alles unter Platforms\Android\Resources einsammelt.
    // Ausgewertet werden nur Bedingungen der Form '$(Configuration)' == '...'.
    private static List<string> NetworkConfigFor(string configuration)
    {
        bool Applies(XElement? e) =>
            e?.Attribute("Condition") is not { } condition
            || condition.Value.Replace("$(Configuration)", configuration).Split("==") is [var left, var right]
                && left.Trim() == right.Trim();

        var files = new List<string> { StrictNetworkConfig };
        foreach (var item in Project.Descendants("AndroidResource").Where(e => Applies(e) && Applies(e.Parent)))
        {
            if ((string?)item.Attribute("Remove") is { } remove)
            {
                files.Remove(remove);
            }
            if ((string?)item.Attribute("Include") is { } include && include.EndsWith("network_security_config.xml"))
            {
                files.Add(include);
            }
        }
        return files;
    }

    // Eine Finanz-App: Android soll ihre Daten nicht in eine Sicherung kopieren.
    // SecureStorage ließe sich aus einer Sicherung ohnehin nicht wiederherstellen.
    [Fact]
    public void App_data_is_excluded_from_android_backup()
    {
        var manifest = XDocument.Load(RepositoryFiles.Path("Mobile", "Duofy", "Platforms", "Android", "AndroidManifest.xml"));
        XNamespace android = "http://schemas.android.com/apk/res/android";

        Assert.Equal("false", (string?)manifest.Root!.Element("application")?.Attribute(android + "allowBackup"));
    }
}
