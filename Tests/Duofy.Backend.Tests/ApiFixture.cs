using System.Net.Http.Headers;
using System.Net.Http.Json;
using Duofy.Schemas;
using Duofy.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Duofy.Api.Tests;

/// <summary>
/// Startet für alle API-Tests eine echte PostgreSQL-Datenbank in Docker und die API
/// dazu. Docker muss laufen.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public DuofyApiFactory Factory { get; private set; } = null!;

    public string ConnectionString => database.GetConnectionString();

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        // Die normale Grenze von 10 Anmeldeversuchen pro Minute würde die vielen
        // Tests ausbremsen; der Test der Grenze baut sich eine eigene API.
        Factory = new DuofyApiFactory(ConnectionString, authPermitLimit: 10_000);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await database.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}

/// <summary>Die API im Testserver, verbunden mit der Testdatenbank.</summary>
public class DuofyApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Die öffentliche Adresse des Servers in den Tests (Duofy:PublicUrl), absichtlich ohne Schrägstrich am Ende.</summary>
    public const string PublicUrl = "https://duofy.example.de";

    private readonly string connectionString;
    private readonly int authPermitLimit;
    private readonly IReadOnlyDictionary<string, string> settings;

    /// <param name="settings">
    /// Weitere Einstellungen, die die Vorgaben überschreiben. Ohne sie ist die Registrierung offen
    /// (Duofy:RegistrationMode=open), damit jeder Test Personen anlegen kann; die Tests des Servers (Spec 70)
    /// stellen den Modus und den Einrichtungscode selbst ein.
    /// </param>
    public DuofyApiFactory(string connectionString, int authPermitLimit, IReadOnlyDictionary<string, string>? settings = null)
    {
        this.connectionString = connectionString;
        this.authPermitLimit = authPermitLimit;
        this.settings = settings ?? new Dictionary<string, string>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Duofy", connectionString);
        builder.UseSetting("RateLimiting:AuthPermitLimit", authPermitLimit.ToString());
        builder.UseSetting("Duofy:PublicUrl", PublicUrl);
        builder.UseSetting("Duofy:RegistrationMode", "open");
        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }
    }

    /// <summary>
    /// Führt etwas direkt auf der Datenbank aus, an der API vorbei, und deshalb auch am Besitzer-Filter vorbei
    /// (Spec 56): Die Tests schauen nach, was wirklich in der Datenbank steht.
    /// </summary>
    public async Task<T> WithDbAsync<T>(Func<DuofyDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DuofyDbContext>();
        using (db.Owners.Bypass("Tests: Blick in die Datenbank an der API vorbei"))
        {
            return await action(db);
        }
    }
}

/// <summary>Kurze Wege für die Tests: registrieren, anmelden, angemeldet anfragen.</summary>
public static class ApiDriver
{
    public const string Password = "zehn-zeich";

    public static string NewEmail(string name = "anna") => $"{name}-{Guid.NewGuid():N}@example.org";

    public static RegisterRequest Registration(string email, string password = Password) =>
        new("Anna", "Beispiel", email, password, AcceptedTerms: true);

    public static async Task<HttpResponseMessage> RegisterAsync(this HttpClient client, RegisterRequest request) =>
        await client.PostAsJsonAsync("/api/v1/auth/register", request);

    public static async Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password = Password) =>
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

    /// <summary>Registriert eine neue Person und meldet sie an.</summary>
    public static async Task<(string Email, TokenResponse Tokens)> SignUpAsync(this HttpClient client)
    {
        var email = NewEmail();
        (await client.RegisterAsync(Registration(email))).EnsureSuccessStatusCode();
        var login = await client.LoginAsync(email);
        login.EnsureSuccessStatusCode();
        return (email, (await login.Content.ReadFromJsonAsync<TokenResponse>())!);
    }

    public static HttpRequestMessage Authorized(HttpMethod method, string path, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }
        return request;
    }

    public static async Task<string?> ReadCodeAsync(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ErrorResponse>())?.Code;
}
