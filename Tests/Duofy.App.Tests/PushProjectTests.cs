using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Duofy.App.Tests;

/// <summary>
/// Spec 84: Die Erinnerungen der App stehen in den Projektdateien richtig. Die Firebase-Datei gehört dem Betreiber und liegt
/// nie im Repository; das Firebase-Paket ist fest gepinnt und nur für Android eingebunden.
/// </summary>
public class PushProjectTests
{
    private static readonly XDocument Project = XDocument.Load(RepositoryFiles.Path("Mobile", "Duofy", "Duofy.csproj"));

    [Fact]
    public void Firebase_messaging_is_pinned_and_only_for_android()
    {
        var reference = Project.Descendants("PackageReference").Single(e => (string?)e.Attribute("Include") == "Xamarin.Firebase.Messaging");

        Assert.Matches(@"^\d+(\.\d+){2,3}$", (string?)reference.Attribute("Version") ?? "");
        Assert.Contains("-android", (string?)reference.Parent!.Attribute("Condition"));
    }

    [Fact]
    public void The_firebase_file_is_built_in_only_when_it_exists_and_is_never_checked_in()
    {
        var item = Project.Descendants("GoogleServicesJson").Single();
        Assert.Contains("Exists('google-services.json')", (string?)item.Attribute("Condition"));

        var ignore = File.ReadAllLines(RepositoryFiles.Path(".gitignore")).Select(l => l.Trim());
        Assert.Contains("google-services.json", ignore);

        var found = Directory.EnumerateFiles(RepositoryFiles.Path("Mobile"), "google-services.json", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
        Assert.Empty(found);
    }

    [Fact]
    public void The_manifest_asks_for_the_notification_permission_and_the_app_uses_the_push_core()
    {
        var manifest = File.ReadAllText(RepositoryFiles.Path("Mobile", "Duofy", "Platforms", "Android", "AndroidManifest.xml"));
        Assert.Contains("android.permission.POST_NOTIFICATIONS", manifest);

        Assert.Contains(Project.Descendants("ProjectReference"), e => ((string?)e.Attribute("Include") ?? "").EndsWith("Duofy.Push.csproj", StringComparison.Ordinal));
    }

    // Die Betriebsunterlagen nennen die neuen Einstellungen, ohne einen Wert (Spec 84, Betriebsleitfaden).
    [Theory]
    [InlineData("Duofy__Reminders__Fcm__ServiceAccountFile")]
    [InlineData("Duofy__Reminders__Wns__ClientId")]
    [InlineData("Duofy__Reminders__Wns__ClientSecret")]
    public void The_settings_are_in_the_template_the_compose_file_and_the_guide_without_a_value(string name)
    {
        var template = File.ReadAllLines(RepositoryFiles.Path(".env.example")).Single(l => l.StartsWith(name + "=", StringComparison.Ordinal));
        Assert.Equal(name + "=", template);

        Assert.Contains(name + ": ${" + name + ":-}", File.ReadAllText(RepositoryFiles.Path("deploy", "docker-compose.yml")));
        Assert.Contains(name, File.ReadAllText(RepositoryFiles.Path("docs", "betrieb.md")));
        Assert.Contains("push/", File.ReadAllLines(RepositoryFiles.Path("deploy", ".gitignore")).Select(l => l.Trim()));
    }

    [Fact]
    public void The_reminders_are_off_by_default_everywhere()
    {
        Assert.Contains("Duofy__Reminders__Enabled=false", File.ReadAllLines(RepositoryFiles.Path(".env.example")));
        Assert.Contains("Duofy__Reminders__Enabled: ${Duofy__Reminders__Enabled:-false}", File.ReadAllText(RepositoryFiles.Path("deploy", "docker-compose.yml")));
    }

    [Fact]
    public void No_service_account_or_credential_file_is_in_the_repository()
    {
        var suspicious = Directory.EnumerateFiles(RepositoryFiles.Root, "*.json", SearchOption.AllDirectories)
            .Where(path => !Regex.IsMatch(path, @"[\\/](obj|bin|node_modules|\.git|\.worktrees)[\\/]"))
            .Where(path => File.ReadAllText(path).Contains("\"private_key\"", StringComparison.Ordinal));

        Assert.Empty(suspicious);
    }

    // S1, N5: Die Anleitung nennt Besitzer und Rechte der Dienstkonto-Datei, prüft sie und sagt, was ein geschlossenes Handy verliert.
    [Fact]
    public void The_guide_names_the_owner_and_mode_of_the_service_account_file_and_checks_them()
    {
        var guide = File.ReadAllText(RepositoryFiles.Path("docs", "betrieb.md"));
        Assert.Contains("chown 1654 deploy/push/firebase.json", guide);
        Assert.Contains("chmod 600 deploy/push/firebase.json", guide);
        Assert.Contains("ls -ln deploy/push/firebase.json", guide);
        Assert.Contains("bei ganz geschlossener App geht verloren", guide);
    }
}
