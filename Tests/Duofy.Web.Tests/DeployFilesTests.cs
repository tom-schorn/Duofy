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
    [Theory]
    [InlineData("deploy/docker-compose.yml")]
    [InlineData("deploy/own-db.yml")]
    public void No_service_publishes_a_port(string path)
    {
        var compose = Read(path);

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

    // Der Server hat knapp 3 GB Arbeitsspeicher: Jeder Dienst hat eine Grenze, auch die eigene Datenbank.
    [Theory]
    [InlineData("deploy/docker-compose.yml", 4)]
    [InlineData("deploy/own-db.yml", 3)]
    public void Every_service_has_a_memory_limit(string path, int services)
    {
        var compose = Read(path).ReplaceLineEndings("\n");
        var listed = ServiceName().Matches(compose[..compose.IndexOf("\nvolumes:", StringComparison.Ordinal)]).Count;

        Assert.Equal(services, listed);
        // In own-db.yml ergänzen api und backup nur die Dienste aus docker-compose.yml; ihre Grenze steht dort.
        Assert.Equal(path.EndsWith("own-db.yml") ? 1 : services, MemoryLimit().Matches(compose).Count);
    }

    // Nachtrag 2026-10-26: Alle Dienste hängen im Netz plagevo (172.30.10.0/24), das diese Compose-Datei anlegt; der
    // Tunnel-Container eines anderen Compose-Projekts tritt ihm als externem Netz mit 172.30.10.2 bei. Die Datenbank
    // ist extern; nur own-db.yml bringt eine eigene im internen Netz plagevo-backend (172.30.11.0/24) mit.
    [Fact]
    public void The_plagevo_network_has_a_fixed_name_and_subnet_and_the_own_tunnel_is_optional()
    {
        var compose = Read("deploy/docker-compose.yml").ReplaceLineEndings("\n");

        Assert.Contains("  plagevo:\n    name: ${PLAGEVO_NETWORK:-plagevo}\n", compose);
        Assert.Contains("        - subnet: 172.30.10.0/24\n          gateway: 172.30.10.1\n", compose);
        Assert.DoesNotContain("\n  db:\n", compose);
        Assert.DoesNotContain("plagevo-backend", compose);
        Assert.DoesNotContain("external: true", compose);
        Assert.DoesNotContain("infrastructure_cloudflared_network", compose);
        Assert.Contains("profiles: [\"own-tunnel\"]", compose);
    }

    [Fact]
    public void The_own_database_lives_only_in_the_internal_network()
    {
        var ownDb = Read("deploy/own-db.yml").ReplaceLineEndings("\n");

        Assert.Contains("  plagevo-backend:\n    name: ${PLAGEVO_BACKEND_NETWORK:-plagevo-backend}\n    internal: true\n", ownDb);
        Assert.Contains("        - subnet: 172.30.11.0/24\n          gateway: 172.30.11.1\n", ownDb);
        Assert.DoesNotMatch(PortsKey(), ownDb);
    }

    // Nachtrag 2026-10-26: feste Adressen, damit der Tunnel auf Adressen zeigt und die API Tunnel und Web erkennt; die
    // eigene Datenbank hängt nur im internen Netz, der Tunnel sieht sie nie.
    [Theory]
    [InlineData("deploy/docker-compose.yml", "api", "      plagevo:\n        ipv4_address: 172.30.10.11\n")]
    [InlineData("deploy/docker-compose.yml", "web", "      plagevo:\n        ipv4_address: 172.30.10.12\n")]
    [InlineData("deploy/docker-compose.yml", "tunnel", "      plagevo:\n        ipv4_address: 172.30.10.2\n")]
    [InlineData("deploy/docker-compose.yml", "backup", "      plagevo:\n        ipv4_address: 172.30.10.13\n")]
    [InlineData("deploy/own-db.yml", "db", "      plagevo-backend:\n        ipv4_address: 172.30.11.10\n")]
    [InlineData("deploy/own-db.yml", "api", "      plagevo-backend:\n        ipv4_address: 172.30.11.11\n")]
    [InlineData("deploy/own-db.yml", "backup", "      plagevo-backend:\n        ipv4_address: 172.30.11.13\n")]
    public void Every_service_has_exactly_its_networks_and_fixed_addresses(string path, string service, string networks)
    {
        var compose = Read(path).ReplaceLineEndings("\n");
        var start = compose.IndexOf($"\n  {service}:\n", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Dienst {service} fehlt in {path}");
        var next = ServiceName().Match(compose, start + service.Length + 5);
        var block = compose[start..(next.Success ? next.Index : compose.Length)];

        var listed = block[(block.IndexOf("    networks:\n", StringComparison.Ordinal) + "    networks:\n".Length)..];
        Assert.Equal(networks, listed.Split("\n\n")[0].TrimEnd('\n') + "\n");
    }

    // Nachtrag 2026-10-26: Web ruft die API an ihrer Adresse, die API vertraut nur dem Tunnel und nimmt nur Web aus.
    [Fact]
    public void Web_calls_the_api_by_address_and_only_the_tunnel_is_trusted()
    {
        var compose = Read("deploy/docker-compose.yml");

        Assert.Contains("Duofy__ApiBaseAddress: http://172.30.10.11:8080/", compose);
        Assert.Contains("RateLimiting__ExemptAddresses: 172.30.10.12", compose);
        Assert.Equal(2, Regex.Matches(compose, @"Duofy__TrustedProxies: \$\{[^\n]*:-172\.30\.10\.2/32\}").Count);
        Assert.DoesNotContain("api-internal", compose);
    }

    // Nachtrag 2026-10-26: Ohne Verbindung zur Datenbank bricht Compose mit einer klaren Meldung ab, für API und
    // Sicherung; beide nutzen dieselbe Verbindung.
    [Fact]
    public void The_connection_string_is_required_and_shared_by_api_and_backup()
    {
        var compose = Read("deploy/docker-compose.yml");
        const string Required = "${ConnectionStrings__Duofy:?ConnectionStrings__Duofy fehlt in .env";

        Assert.Contains($"      ConnectionStrings__Duofy: {Required}", compose);
        Assert.Contains($"      DUOFY_CONNECTION_STRING: {Required}", compose);
        Assert.DoesNotContain("POSTGRES_", compose);
    }

    [Fact]
    public void The_example_connection_string_has_an_empty_password()
    {
        var example = Read("deploy/.env.example");

        Assert.Matches(@"(?m)^ConnectionStrings__Duofy=Host=[^;\n]+;Port=5432;Database=plagevo;Username=plagevo;Password=\s*$", example);
    }

    // Nachtrag 2026-10-26: Jede Einstellung aus der .env lässt sich in der Schreibweise von .NET (Duofy__Mail__Host)
    // setzen, und die alten Namen (DUOFY_MAIL_HOST) gelten weiter; die Schreibweise von .NET gewinnt.
    [Theory]
    [InlineData("Duofy__Hosted", "DUOFY_HOSTED")]
    [InlineData("Duofy__PublicUrl", "DUOFY_PUBLIC_URL")]
    [InlineData("Duofy__RegistrationMode", "DUOFY_REGISTRATION_MODE")]
    [InlineData("Duofy__SetupCode", "DUOFY_SETUP_CODE")]
    [InlineData("Duofy__ImprintFile", "DUOFY_IMPRINT_FILE")]
    [InlineData("Duofy__PrivacyFile", "DUOFY_PRIVACY_FILE")]
    [InlineData("Duofy__TermsFile", "DUOFY_TERMS_FILE")]
    [InlineData("Duofy__Mail__Host", "DUOFY_MAIL_HOST")]
    [InlineData("Duofy__Mail__Port", "DUOFY_MAIL_PORT")]
    [InlineData("Duofy__Mail__User", "DUOFY_MAIL_USER")]
    [InlineData("Duofy__Mail__Password", "DUOFY_MAIL_PASSWORD")]
    [InlineData("Duofy__Mail__From", "DUOFY_MAIL_FROM")]
    [InlineData("Duofy__Mail__UseTls", "DUOFY_MAIL_USE_TLS")]
    [InlineData("Duofy__TrustedProxies", "TRUSTED_PROXIES")]
    [InlineData("RateLimiting__AuthPermitLimit", "RATE_LIMIT_AUTH_PERMIT_LIMIT")]
    [InlineData("Duofy__Analytics__Token", "CLOUDFLARE_ANALYTICS_TOKEN")]
    public void Every_setting_accepts_the_dotnet_name_before_the_old_name(string dotnet, string old)
    {
        var compose = Read("deploy/docker-compose.yml");

        Assert.Matches($@"(?m)^      {dotnet}: \$\{{{dotnet}:-\$\{{{old}:-[^\n]*\}}\}}\s*$", compose);
    }

    [Fact]
    public void The_sender_name_accepts_both_dotnet_spellings_and_the_old_name()
    {
        var compose = Read("deploy/docker-compose.yml");

        Assert.Contains(
            "Duofy__Mail__FromName: ${Duofy__Mail__FromName:-${Duofy__Mail__From__Name:-${DUOFY_MAIL_FROM_NAME:-Plagevo}}}",
            compose);
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
