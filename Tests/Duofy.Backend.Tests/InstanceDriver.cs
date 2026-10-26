using System.Net.Http.Json;
using Duofy.Schemas;
using Duofy.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>
/// Ein eigener Server mit eigener, leerer Datenbank im selben Container (Spec 70): Ob es schon einen Administrator
/// gibt, hängt an der ganzen Datenbank, darum teilen sich diese Tests sie mit niemandem.
/// </summary>
public sealed class FreshInstance : IAsyncDisposable
{
    public const string LogCategory = "Duofy.Api.Instance";

    private readonly DuofyApiFactory factory;

    private FreshInstance(DuofyApiFactory factory, CapturedLogs logs)
    {
        this.factory = factory;
        Logs = logs;
        Client = factory.CreateClient();
    }

    public HttpClient Client { get; }

    /// <summary>Was die API ins Log geschrieben hat, je Kategorie.</summary>
    internal CapturedLogs Logs { get; }

    public DuofyApiFactory Factory => factory;

    /// <summary>Startet den Server mit diesem Einrichtungscode (null: keiner) und Registrierungsmodus.</summary>
    public static FreshInstance Start(ApiFixture fixture, string? setupCode, string mode)
    {
        var connection = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = $"instance_{Guid.NewGuid():N}" };
        var settings = new Dictionary<string, string>
        {
            ["Duofy:SetupCode"] = setupCode ?? "",
            ["Duofy:RegistrationMode"] = mode,
        };
        var logs = new CapturedLogs();
        var factory = new LoggedApiFactory(connection.ConnectionString, settings, logs);
        return new FreshInstance(factory, logs);
    }

    public async Task<InstanceResponse> InstanceAsync() =>
        (await Client.GetFromJsonAsync<InstanceResponse>("/api/v1/instance"))!;

    /// <summary>Registriert mit diesen Angaben; die Antwort bleibt zum Prüfen.</summary>
    public Task<HttpResponseMessage> RegisterAsync(string firstName, string? email = null, string? setupCode = null, string? invitationToken = null) =>
        Client.RegisterAsync(new RegisterRequest(
            firstName, "Muster", email ?? NewEmail(firstName.ToLowerInvariant()), Password, AcceptedTerms: true, setupCode, invitationToken));

    /// <summary>Meldet eine schon registrierte Person an und liest ihr Konto.</summary>
    public async Task<Person> SignInAsync(string email, string firstName)
    {
        var login = await Client.LoginAsync(email);
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken;
        var me = await Client.SendAsync(Authorized(HttpMethod.Get, "/api/v1/users/me", token));
        var id = (await me.Content.ReadFromJsonAsync<MeResponse>())!.Id;
        return new Person(id, email, firstName, "Muster", token);
    }

    /// <summary>Registriert eine Person (mit Code oder Einladung, wenn angegeben) und meldet sie an.</summary>
    public async Task<Person> PersonAsync(string firstName, string? setupCode = null, string? invitationToken = null)
    {
        var email = NewEmail(firstName.ToLowerInvariant());
        (await RegisterAsync(firstName, email, setupCode, invitationToken)).EnsureSuccessStatusCode();
        return await SignInAsync(email, firstName);
    }

    public Task<bool> IsAdminAsync(Guid userId) =>
        factory.WithDbAsync(db => db.Users.Where(u => u.Id == userId).Select(u => u.IsInstanceAdmin).SingleAsync());

    public Task<int> UserCountAsync() => factory.WithDbAsync(db => db.Users.CountAsync());

    public async ValueTask DisposeAsync()
    {
        // Die Datenbank bleibt bis zum Ende des Containers; sie ist klein, und der Container verschwindet nach den Tests.
        Client.Dispose();
        await factory.DisposeAsync();
    }

    /// <summary>Die Test-API mit eigenen Einstellungen, die ihr Log zusätzlich in <see cref="CapturedLogs"/> schreibt.</summary>
    private sealed class LoggedApiFactory(string connectionString, IReadOnlyDictionary<string, string> settings, CapturedLogs logs)
        : DuofyApiFactory(connectionString, authPermitLimit: 10_000, settings)
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logs));
        }
    }
}

/// <summary>Kurze Wege für die Verwaltung des Servers (Spec 70).</summary>
public static class AdminDriver
{
    public const string AdminInvitations = "/api/v1/admin/invitations";

    public const string ClaimAdmin = "/api/v1/users/me/claim-admin";
}
