using System.Xml.Linq;

namespace Duofy.App.Tests;

/// <summary>
/// Issue #40 (Microsoft Store): Der Paketweg für Windows (MSIX) und die Dateien für die Einreichung liegen im Projekt.
/// Gelesen werden nur Dateien; gebaut und gestartet wird hier nichts. Herausgeber- und Identitätswerte kennt nur Toms
/// Partner Center und stehen deshalb als benannte Platzhalter da (ERSETZEN), nie als erfundene Werte.
/// </summary>
public class StoreFilesTests
{
    private static readonly XDocument Project = XDocument.Load(RepositoryFiles.Path("Mobile", "Duofy", "Duofy.csproj"));

    private static readonly string ManifestPath = RepositoryFiles.Path("Mobile", "Duofy", "Platforms", "Windows", "Package.appxmanifest");

    private static XNamespace Foundation => "http://schemas.microsoft.com/appx/manifest/foundation/windows10";

    private static XDocument Manifest => XDocument.Load(ManifestPath);

    private static string Read(params string[] parts) => File.ReadAllText(RepositoryFiles.Path(parts));

    // Der Testweg am PC bleibt unverpackt, solange niemand das Paket verlangt.
    [Fact]
    public void Windows_stays_unpackaged_unless_the_store_package_is_asked_for()
    {
        var unpackaged = Project.Descendants("WindowsPackageType").Single(e => e.Parent!.Attribute("Condition") is { } c && !c.Value.Contains("DuofyStorePackage"));
        Assert.Equal("None", unpackaged.Value.Trim());

        var packaged = Project.Descendants("WindowsPackageType").Single(e => ((string?)e.Parent!.Attribute("Condition") ?? "").Contains("DuofyStorePackage"));
        Assert.Equal("MSIX", packaged.Value.Trim());
        Assert.Contains("'$(DuofyStorePackage)' == 'true'", (string)packaged.Parent!.Attribute("Condition")!);
    }

    // Android bleibt, wie es war: ein Ziel, die Windows-Ziele nur auf Windows.
    [Fact]
    public void Android_is_still_the_only_unconditioned_target_and_windows_is_only_added_on_windows()
    {
        Assert.Equal("net10.0-android", Project.Descendants("TargetFrameworks").Single(e => e.Parent!.Attribute("Condition") is null).Value.Trim());
        var windows = Project.Descendants("TargetFrameworks").Single(e => e.Parent!.Attribute("Condition") is not null);
        Assert.Contains("IsOSPlatform('Windows')", (string)windows.Parent!.Attribute("Condition")!);
    }

    [Fact]
    public void The_package_manifest_names_its_placeholders_and_invents_no_identity()
    {
        var identity = Manifest.Descendants(Foundation + "Identity").Single();

        Assert.StartsWith("ERSETZEN", (string)identity.Attribute("Name")!);
        Assert.StartsWith("CN=ERSETZEN", (string)identity.Attribute("Publisher")!);
        var publisherName = Manifest.Descendants(Foundation + "PublisherDisplayName").Single().Value;
        Assert.StartsWith("ERSETZEN", publisherName);
    }

    // Nur was nötig ist: die Vollvertrauens-Fähigkeit, die jede MAUI-Desktop-App im Paket braucht, sonst nichts.
    [Fact]
    public void The_manifest_asks_for_no_rights_beyond_full_trust()
    {
        var capabilities = Manifest.Descendants().Where(e => e.Name.LocalName == "Capability").Select(e => (string)e.Attribute("Name")!).ToList();

        Assert.Equal(["runFullTrust"], capabilities);
        Assert.Empty(Manifest.Descendants(Foundation + "DeviceCapability"));
    }

    [Fact]
    public void The_manifest_targets_the_same_windows_versions_as_the_project()
    {
        var target = Manifest.Descendants(Foundation + "TargetDeviceFamily").Single();

        Assert.Equal("10.0.17763.0", (string)target.Attribute("MinVersion")!);
    }

    // Keine Zugangsdaten, Schlüssel oder Kennwörter im Paketweg.
    [Theory]
    [InlineData("Mobile/Duofy/Duofy.csproj")]
    [InlineData("Mobile/Duofy/Platforms/Windows/Package.appxmanifest")]
    [InlineData("docs/store/README.md")]
    [InlineData("docs/store/beschreibung.md")]
    [InlineData("docs/store/datenschutz.md")]
    public void The_store_files_hold_no_secrets(string path)
    {
        var text = Read(path.Split('/'));

        Assert.DoesNotContain("PRIVATE KEY", text);
        Assert.DoesNotContain(".pfx", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PackageCertificateKeyFile", text);
        Assert.DoesNotContain("PackageCertificatePassword", text);
    }

    [Fact]
    public void The_store_tile_is_in_the_project_and_has_the_store_size()
    {
        var png = File.ReadAllBytes(RepositoryFiles.Path("Mobile", "Store", "store-tile-512.png"));

        Assert.Equal([0x89, 0x50, 0x4E, 0x47], png[..4]);
        // Breite und Höhe stehen big-endian in den Bytes 16 bis 23 (IHDR).
        Assert.Equal(512, (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19]);
        Assert.Equal(512, (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23]);
        Assert.True(File.Exists(RepositoryFiles.Path("Mobile", "Store", "store-tile-512.svg")));
    }

    // Anleitung, Beschreibung und Datenschutzhinweis liegen vor; die Anleitung nennt jeden Platzhalter und wo er herkommt.
    [Fact]
    public void The_guide_names_every_placeholder_and_the_build_command()
    {
        var guide = Read("docs", "store", "README.md");

        Assert.Contains("ERSETZEN", guide);
        Assert.Contains("Partner Center", guide);
        Assert.Contains("DuofyStorePackage=true", guide);
        Assert.Contains("StoreUpload", guide);
        foreach (var placeholder in new[] { "Identity", "Publisher", "PublisherDisplayName" })
        {
            Assert.Contains(placeholder, guide);
        }
        Assert.Contains("Einreichung", guide);
    }

    [Fact]
    public void The_description_is_calm_and_the_privacy_note_points_to_the_privacy_policy()
    {
        var description = Read("docs", "store", "beschreibung.md");
        var privacy = Read("docs", "store", "datenschutz.md");

        Assert.Contains("Plagevo", description);
        foreach (var superlative in new[] { "beste", "revolution", "einzigartig", "unschlagbar", "perfekt" })
        {
            Assert.DoesNotContain(superlative, description, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Contains("Datenschutzerklärung", privacy);
        Assert.Contains("https://", privacy);
    }
}
