using System.Text.RegularExpressions;

namespace Duofy.Web.Tests;

/// <summary>
/// Die Dateien für den Betrieb von Plagevo (Spec 79, A1): Kein Dienst veröffentlicht einen Port, beide Images werden
/// in zwei Stufen gebaut, laufen nicht als root, haben einen HEALTHCHECK und die Labels für Version und Bauzeit, und die
/// Beispieldatei der Umgebung enthält kein Geheimnis. Ob die Compose-Datei gültig ist und die Images bauen, prüft
/// docker selbst (docs/betrieb.md); hier steht, was beim Ändern der Dateien nicht verloren gehen darf.
/// </summary>
public partial class DeployFilesTests
{
    private static readonly string[] Dockerfiles = ["Backend/Duofy.Backend/Dockerfile", "Web/Duofy.Web/Dockerfile"];

    // A1
    [Fact]
    public void No_service_publishes_a_port()
    {
        var compose = Read("deploy/docker-compose.yml");

        Assert.DoesNotMatch(PortsKey(), compose);
    }

    // A1
    [Theory]
    [InlineData("Backend/Duofy.Backend/Dockerfile")]
    [InlineData("Web/Duofy.Web/Dockerfile")]
    public void Both_images_are_multi_stage_non_root_with_health_check_and_labels(string path)
    {
        var dockerfile = Read(path);

        Assert.True(FromLine().Matches(dockerfile).Count >= 2, $"{path}: weniger als zwei Stufen");
        Assert.Matches(@"(?m)^USER \$APP_UID\s*$", dockerfile);
        Assert.Matches(@"(?m)^HEALTHCHECK ", dockerfile);
        Assert.Contains("org.opencontainers.image.version=\"${VERSION}\"", dockerfile);
        Assert.Contains("org.opencontainers.image.created=\"${BUILD_DATE}\"", dockerfile);
    }

    // A1: Die Compose-Datei baut genau diese beiden Dockerfiles.
    [Fact]
    public void Compose_builds_both_dockerfiles()
    {
        var compose = Read("deploy/docker-compose.yml");

        Assert.All(Dockerfiles, path => Assert.Contains($"dockerfile: {path}", compose));
    }

    // Der Server hat knapp 3 GB Arbeitsspeicher: Jeder Dienst hat eine Grenze.
    [Fact]
    public void Every_service_has_a_memory_limit()
    {
        var compose = Read("deploy/docker-compose.yml").ReplaceLineEndings("\n");
        var services = ServiceName().Matches(compose[..compose.IndexOf("\nvolumes:", StringComparison.Ordinal)]).Count;

        Assert.Equal(5, services);
        Assert.Equal(services, MemoryLimit().Matches(compose).Count);
    }

    // Der Tunnel-Container gibt es auf dem Server schon; web und api treten seinem Netz bei, Compose legt es nicht an.
    [Fact]
    public void The_tunnel_network_is_external_and_the_own_tunnel_is_optional()
    {
        var compose = Read("deploy/docker-compose.yml");

        Assert.Contains("name: ${CLOUDFLARED_NETWORK:-infrastructure_cloudflared_network}\n    external: true", compose.ReplaceLineEndings("\n"));
        Assert.Contains("profiles: [\"own-tunnel\"]", compose);
    }

    // R1: Geheimnisse stehen nur in der .env auf dem Server, nie im Beispiel.
    [Theory]
    [InlineData("POSTGRES_PASSWORD")]
    [InlineData("DUOFY_SETUP_CODE")]
    [InlineData("DUOFY_MAIL_PASSWORD")]
    [InlineData("TUNNEL_TOKEN")]
    public void The_example_environment_has_no_secret_values(string name)
    {
        var example = Read("deploy/.env.example");

        Assert.Matches($@"(?m)^{name}=\s*$", example);
    }

    [Fact]
    public void The_server_environment_and_backups_are_not_committed()
    {
        var ignored = Read("deploy/.gitignore").Split('\n').Select(l => l.Trim()).ToList();

        Assert.Contains(".env", ignored);
        Assert.Contains("backups/", ignored);
    }

    private static string Read(string path) => File.ReadAllText(Path.Combine(SolutionRoot(), path));

    private static string SolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Duofy.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Duofy.slnx nicht gefunden.");
    }

    [GeneratedRegex(@"(?m)^\s+ports\s*:")]
    private static partial Regex PortsKey();

    [GeneratedRegex(@"(?m)^  [a-z]+:\s*$")]
    private static partial Regex ServiceName();

    [GeneratedRegex(@"(?m)^    mem_limit: \d+m\s*$")]
    private static partial Regex MemoryLimit();

    [GeneratedRegex(@"(?m)^FROM ")]
    private static partial Regex FromLine();
}
