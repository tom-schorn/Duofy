using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duofy.Api.Tests;

/// <summary>Impressum, Datenschutzerklärung und Nutzungsbedingungen aus eingestellten Dateien, ohne Anmeldung (Spec 70, R7).</summary>
[Collection(ApiCollection.Name)]
public sealed class LegalTests : IAsyncLifetime
{
    private const string LogCategory = "Duofy.Api.Legal";

    private readonly ApiFixture fixture;
    private readonly string folder = Directory.CreateTempSubdirectory("duofy-legal-").FullName;
    private readonly List<WebApplicationFactory<Program>> factories = [];

    public LegalTests(ApiFixture fixture)
    {
        this.fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var factory in factories)
        {
            await factory.DisposeAsync();
        }
        Directory.Delete(folder, recursive: true);
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Eine API, deren drei Dateien so eingestellt sind; null heißt nicht eingestellt.</summary>
    private (HttpClient Client, CapturedLogs Logs) Start(string? imprint = null, string? privacy = null, string? terms = null)
    {
        var logs = new CapturedLogs();
        var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Duofy:ImprintFile", imprint ?? "");
            builder.UseSetting("Duofy:PrivacyFile", privacy ?? "");
            builder.UseSetting("Duofy:TermsFile", terms ?? "");
            builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logs));
        });
        factories.Add(factory);
        return (factory.CreateClient(), logs);
    }

    // A11
    [Fact]
    public async Task A_configured_document_is_listed_and_served_without_sign_in_and_the_others_are_not_found()
    {
        var (client, _) = Start(imprint: WriteFile("imprint.txt", "Max Muster, Musterweg 1"));

        var list = await client.GetFromJsonAsync<LegalListResponse>("/api/v1/legal");
        var text = await client.GetAsync("/api/v1/legal/imprint");
        var privacy = await client.GetAsync("/api/v1/legal/privacy");

        Assert.Equal(["imprint"], list!.Documents);
        Assert.Equal(HttpStatusCode.OK, text.StatusCode);
        Assert.Equal("Max Muster, Musterweg 1", (await text.Content.ReadFromJsonAsync<LegalTextResponse>())!.Text);
        Assert.Equal(HttpStatusCode.NotFound, privacy.StatusCode);
        Assert.Equal("legal_not_found", await privacy.ReadCodeAsync());
    }

    [Fact]
    public async Task Nothing_is_configured_by_default_and_the_list_is_empty()
    {
        var (client, _) = Start();

        var list = await client.GetFromJsonAsync<LegalListResponse>("/api/v1/legal");

        Assert.Empty(list!.Documents);
    }

    [Fact]
    public async Task The_list_names_all_three_documents_in_a_fixed_order()
    {
        var (client, _) = Start(
            imprint: WriteFile("i.txt", "I"), privacy: WriteFile("p.txt", "P"), terms: WriteFile("t.txt", "T"));

        var list = await client.GetFromJsonAsync<LegalListResponse>("/api/v1/legal");

        Assert.Equal(["imprint", "privacy", "terms"], list!.Documents);
    }

    [Theory]
    [InlineData("impressum")]
    [InlineData("IMPRINT")]
    [InlineData("..%2Fappsettings.json")]
    [InlineData("other")]
    public async Task An_unknown_name_answers_legal_not_found(string name)
    {
        var (client, _) = Start(imprint: WriteFile("imprint.txt", "Text"));

        var response = await client.GetAsync($"/api/v1/legal/{name}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("legal_not_found", await response.ReadCodeAsync());
    }

    // A12
    [Fact]
    public async Task A_missing_file_and_a_file_over_200_kb_do_not_exist_and_the_log_names_the_document_not_the_path()
    {
        var missing = Path.Combine(folder, "gone-privacy.txt");
        var large = WriteFile("large-terms.txt", new string('x', 300 * 1024));
        var (client, logs) = Start(privacy: missing, terms: large);

        var list = await client.GetFromJsonAsync<LegalListResponse>("/api/v1/legal");
        var privacy = await client.GetAsync("/api/v1/legal/privacy");
        var terms = await client.GetAsync("/api/v1/legal/terms");

        Assert.Empty(list!.Documents);
        Assert.Equal(HttpStatusCode.NotFound, privacy.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, terms.StatusCode);
        var messages = logs.Messages(LogCategory);
        Assert.Contains(messages, m => m.Contains("privacy"));
        Assert.Contains(messages, m => m.Contains("terms"));
        Assert.DoesNotContain(messages, m => m.Contains(folder) || m.Contains("gone-privacy") || m.Contains("large-terms"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \r\n  ")]
    public async Task An_empty_file_does_not_exist(string content)
    {
        var (client, logs) = Start(imprint: WriteFile("empty.txt", content));

        var list = await client.GetFromJsonAsync<LegalListResponse>("/api/v1/legal");

        Assert.Empty(list!.Documents);
        Assert.Contains(logs.Messages(LogCategory), m => m.Contains("imprint"));
    }

    [Fact]
    public async Task A_file_of_exactly_200_kb_still_counts()
    {
        var (client, _) = Start(imprint: WriteFile("limit.txt", new string('x', 200 * 1024)));

        var list = await client.GetFromJsonAsync<LegalListResponse>("/api/v1/legal");

        Assert.Equal(["imprint"], list!.Documents);
    }

    [Fact]
    public async Task A_directory_instead_of_a_file_does_not_exist()
    {
        var (client, logs) = Start(imprint: folder);

        var list = await client.GetFromJsonAsync<LegalListResponse>("/api/v1/legal");

        Assert.Empty(list!.Documents);
        Assert.DoesNotContain(logs.Messages(LogCategory), m => m.Contains(folder));
    }

    [Fact]
    public async Task Line_breaks_and_markup_come_back_unchanged()
    {
        var text = "Zeile eins\nZeile <b>zwei</b>\r\nZeile drei";
        var (client, _) = Start(terms: WriteFile("terms.txt", text));

        var response = await client.GetFromJsonAsync<LegalTextResponse>("/api/v1/legal/terms");

        Assert.Equal(text, response!.Text);
    }

    [Fact]
    public async Task The_instance_lists_the_same_documents()
    {
        var (client, _) = Start(imprint: WriteFile("i.txt", "I"), terms: WriteFile("t.txt", "T"));

        var instance = await client.GetFromJsonAsync<InstanceResponse>("/api/v1/instance");

        Assert.Equal(["imprint", "terms"], instance!.LegalDocuments);
    }
}

/// <summary>Sammelt die Meldungen der API je Kategorie, damit Tests sie lesen können.</summary>
internal sealed class CapturedLogs : ILoggerProvider
{
    private readonly List<(string Category, string Message)> entries = [];

    public IReadOnlyList<string> Messages(string category)
    {
        lock (entries)
        {
            return entries.Where(e => e.Category == category).Select(e => e.Message).ToList();
        }
    }

    public ILogger CreateLogger(string categoryName) => new Capture(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class Capture(CapturedLogs owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (owner.entries)
            {
                owner.entries.Add((category, formatter(state, exception) + (exception is null ? "" : " " + exception)));
            }
        }
    }
}
