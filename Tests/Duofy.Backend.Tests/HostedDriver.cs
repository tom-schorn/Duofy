using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Duofy.Schemas;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>
/// Der Ersatz für den SMTP-Versand in den Tests (Spec 53): Er schickt nichts, sondern merkt sich jede Mail, damit
/// ein Test nachsehen kann, wer was bekommen hat.
/// </summary>
internal sealed class RecordingMailSender : IMailSender
{
    private readonly ConcurrentQueue<OutgoingMail> sent = new();

    public IReadOnlyList<OutgoingMail> All => sent.ToList();

    /// <summary>Die Mails an diese Adresse, ohne Rücksicht auf Groß- und Kleinschreibung.</summary>
    public IReadOnlyList<OutgoingMail> To(string address) =>
        sent.Where(m => string.Equals(m.To, address, StringComparison.OrdinalIgnoreCase)).ToList();

    public Task SendAsync(OutgoingMail mail, CancellationToken cancellationToken)
    {
        sent.Enqueue(mail);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Ein Server mit eigener API-Instanz auf der gemeinsamen Testdatenbank, wahlweise als gehostete Version mit
/// vollständigen Mail-Einstellungen. Seine Mails landen in <see cref="Mails"/>, sein Log in <see cref="Logs"/>.
/// </summary>
public sealed class MailServer : IAsyncDisposable
{
    public const string From = "duofy@example.de";

    private readonly DuofyApiFactory factory;
    private readonly RecordingMailSender mails;

    private MailServer(DuofyApiFactory factory, RecordingMailSender mails, CapturedLogs logs)
    {
        this.factory = factory;
        this.mails = mails;
        Logs = logs;
        Client = factory.CreateClient();
    }

    public HttpClient Client { get; }

    /// <summary>Die verschickten Mails, nachdem die Warteschlange abgearbeitet ist; die API verschickt neben der Anfrage.</summary>
    internal RecordingMailSender Mails
    {
        get
        {
            WaitForMailAsync().GetAwaiter().GetResult();
            return mails;
        }
    }

    /// <summary>Wartet, bis keine Mail mehr in der Warteschlange der API steht, höchstens zehn Sekunden.</summary>
    public async Task WaitForMailAsync()
    {
        var queue = factory.Services.GetRequiredService<MailQueue>();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (queue.Pending > 0)
        {
            Assert.True(DateTime.UtcNow < deadline, $"Noch {queue.Pending} Mails in der Warteschlange.");
            await Task.Delay(5);
        }
    }

    internal CapturedLogs Logs { get; }

    public DuofyApiFactory Factory => factory;

    /// <summary>Die Einstellungen eines gehosteten Servers mit allem, was er zum Start braucht.</summary>
    public static Dictionary<string, string> HostedSettings() => new()
    {
        ["Duofy:Hosted"] = "true",
        ["Duofy:Mail:Host"] = "smtp.example.de",
        ["Duofy:Mail:Port"] = "587",
        ["Duofy:Mail:From"] = From,
    };

    /// <summary>Ein gehosteter Server (Duofy:Hosted=true) mit vollständigen Mail-Einstellungen.</summary>
    public static MailServer Hosted(ApiFixture fixture, Action<IServiceCollection>? services = null) =>
        Start(fixture.ConnectionString, HostedSettings(), services);

    /// <summary>Ein selbst betriebener Server (Duofy:Hosted=false), der trotzdem einen Mail-Ersatz hat, damit Tests zeigen, dass er ihn nie benutzt.</summary>
    public static MailServer SelfHosted(ApiFixture fixture, Action<IServiceCollection>? services = null) =>
        Start(fixture.ConnectionString, new Dictionary<string, string> { ["Duofy:Hosted"] = "false" }, services);

    public static MailServer Start(ApiFixture fixture, IReadOnlyDictionary<string, string> settings, Action<IServiceCollection>? services = null) =>
        Start(fixture.ConnectionString, settings, services);

    /// <summary>Die Verbindung zu einer eigenen, leeren Datenbank im selben Container; die API legt sie beim Start an.</summary>
    public static string FreshDatabase(ApiFixture fixture) =>
        new Npgsql.NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = $"mail_{Guid.NewGuid():N}" }.ConnectionString;

    public static MailServer Start(string connectionString, IReadOnlyDictionary<string, string> settings, Action<IServiceCollection>? services = null)
    {
        var mails = new RecordingMailSender();
        var logs = new CapturedLogs();
        var factory = new MailApiFactory(connectionString, settings, logs, s =>
        {
            s.AddSingleton<IMailSender>(mails);
            services?.Invoke(s);
        });
        try
        {
            return new MailServer(factory, mails, logs);
        }
        catch
        {
            // Der Server ist nicht gestartet; die Fabrik räumt trotzdem auf.
            factory.Dispose();
            throw;
        }
    }

    /// <summary>Registriert eine Person; die Antwort bleibt zum Prüfen.</summary>
    public Task<HttpResponseMessage> RegisterAsync(string email, string firstName = "Anna") =>
        Client.RegisterAsync(new RegisterRequest(firstName, "Muster", email, Password, AcceptedTerms: true));

    /// <summary>Registriert eine Person (auf Wunsch mit Einrichtungscode) und bestätigt ihre Adresse mit dem Link aus der Mail.</summary>
    public async Task<string> ConfirmedAsync(string firstName = "Anna", string? setupCode = null)
    {
        var email = NewEmail(firstName.ToLowerInvariant());
        (await Client.RegisterAsync(new RegisterRequest(firstName, "Muster", email, Password, AcceptedTerms: true, setupCode)))
            .EnsureSuccessStatusCode();
        (await ConfirmAsync(Mails.To(email).Single())).EnsureSuccessStatusCode();
        return email;
    }

    /// <summary>Öffnet den Link zum Bestätigen aus dieser Mail.</summary>
    internal Task<HttpResponseMessage> ConfirmAsync(OutgoingMail mail)
    {
        var link = MailLink.Of(mail);
        return Client.PostAsJsonAsync("/api/v1/auth/confirm-email", new ConfirmEmailRequest(Guid.Parse(link["id"]), link["token"]));
    }

    /// <summary>Eine bestätigte, angemeldete Person.</summary>
    public async Task<Person> ConfirmedPersonAsync(string firstName = "Anna", string? setupCode = null) =>
        await SignInAsync(await ConfirmedAsync(firstName, setupCode), firstName);

    /// <summary>Meldet an und gibt das ganze Token-Paar zurück.</summary>
    public async Task<TokenResponse> TokensAsync(string email, string password = Password)
    {
        var login = await Client.LoginAsync(email, password);
        login.EnsureSuccessStatusCode();
        return (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    /// <summary>Tauscht ein Refresh-Token; die Antwort bleibt zum Prüfen.</summary>
    public Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        Client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(refreshToken));

    /// <summary>Meldet eine registrierte Person an und liest ihr Konto.</summary>
    public async Task<Person> SignInAsync(string email, string firstName = "Anna")
    {
        var login = await Client.LoginAsync(email);
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken;
        var me = await Client.SendAsync(Authorized(HttpMethod.Get, "/api/v1/users/me", token));
        var id = (await me.Content.ReadFromJsonAsync<MeResponse>())!.Id;
        return new Person(id, email, firstName, "Muster", token);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await factory.DisposeAsync();
    }

    /// <summary>Die Test-API mit eigenen Einstellungen und Diensten, deren Log zusätzlich in <see cref="CapturedLogs"/> landet.</summary>
    internal sealed class MailApiFactory(
        string connectionString, IReadOnlyDictionary<string, string> settings, CapturedLogs logs, Action<IServiceCollection> services)
        : DuofyApiFactory(connectionString, authPermitLimit: 10_000, settings)
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(s => s.AddSingleton<ILoggerProvider>(logs));
            builder.ConfigureTestServices(services);
        }
    }
}

/// <summary>Liest den Link aus einer Mail und seine Parameter.</summary>
internal static partial class MailLink
{
    /// <summary>Der eine Link auf die öffentliche Adresse im Text der Mail.</summary>
    public static Uri UrlOf(OutgoingMail mail)
    {
        var match = LinkPattern().Match(mail.Text);
        Assert.True(match.Success, $"Kein Link in der Mail: {mail.Text}");
        return new Uri(match.Value);
    }

    /// <summary>Die Parameter des Links, entschlüsselt.</summary>
    public static IReadOnlyDictionary<string, string> Of(OutgoingMail mail) =>
        QueryHelpers.ParseQuery(UrlOf(mail).Query).ToDictionary(p => p.Key, p => p.Value.ToString());

    [GeneratedRegex(@"https://duofy\.example\.de/\S+")]
    private static partial Regex LinkPattern();
}
