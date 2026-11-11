using Duofy.Services;
using Duofy.Shared.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Duofy.Shared.Services;

namespace Duofy.Shared.Tests;

/// <summary>Spec 125, A6: In der App erzeugt Drucken ein PDF und öffnet damit den Teilen-Dialog.</summary>
public class AppPdfPrintTests
{
    private sealed class FakeRenderer : IPrintViewRenderer
    {
        public List<string> Files { get; } = [];

        public List<string> Discarded { get; } = [];

        public void Discard(string path) => Discarded.Add(path);

        public Task<string> RenderPdfAsync(string fileName, CancellationToken cancellationToken = default)
        {
            Files.Add(fileName);
            return Task.FromResult("/cache/" + fileName);
        }
    }

    private sealed class FakeShare : IPdfShare
    {
        public List<(string Title, string Path, string ContentType)> Shared { get; } = [];

        public Task ShareFileAsync(string title, string path, string contentType)
        {
            Shared.Add((title, path, contentType));
            return Task.CompletedTask;
        }
    }

    private static IStringLocalizer<SharedResources> Localizer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();
        return services.BuildServiceProvider().GetRequiredService<IStringLocalizer<SharedResources>>();
    }

    [Fact]
    public async Task Printing_creates_a_pdf_and_hands_it_to_the_share_dialog()
    {
        var renderer = new FakeRenderer();
        var share = new FakeShare();
        IPrintService service = new PdfPrintService(renderer, share, Localizer(), NullLogger<PdfPrintService>.Instance, () => new DateTime(2026, 11, 11, 9, 30, 5));

        await service.PrintAsync();

        Assert.Equal(["plan-20261111-093005.pdf"], renderer.Files);
        var shared = Assert.Single(share.Shared);
        Assert.Equal("/cache/plan-20261111-093005.pdf", shared.Path);
        Assert.Equal("application/pdf", shared.ContentType);
        // Der Titel kommt aus den Ressourcen, und die Datei im Cache ist nach dem Teilen weg.
        Assert.Equal("Plan", shared.Title);
        Assert.Equal(["/cache/plan-20261111-093005.pdf"], renderer.Discarded);
    }

    [Fact]
    public async Task The_cached_pdf_is_discarded_also_when_sharing_fails()
    {
        var renderer = new FakeRenderer();
        var service = new PdfPrintService(renderer, new FailingShare(), Localizer(), NullLogger<PdfPrintService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(service.PrintAsync);

        Assert.Single(renderer.Discarded);
    }

    private sealed class FailingShare : IPdfShare
    {
        public Task ShareFileAsync(string title, string path, string contentType) => throw new InvalidOperationException("no share");
    }

    [Fact]
    public async Task Nothing_is_shared_when_the_pdf_cannot_be_created()
    {
        var share = new FakeShare();
        var service = new PdfPrintService(new FailingRenderer(), share, Localizer(), NullLogger<PdfPrintService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(service.PrintAsync);

        Assert.Empty(share.Shared);
    }

    private sealed class FailingRenderer : IPrintViewRenderer
    {
        public Task<string> RenderPdfAsync(string fileName, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("no view");

        public void Discard(string path)
        {
        }
    }

    [Fact]
    public void The_app_registers_the_pdf_print_service_and_the_file_share()
    {
        var program = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Mobile", "Duofy", "MauiProgram.cs"));

        Assert.Contains("AddScoped<IPrintService, PdfPrintService>()", program);
        Assert.Contains("AddSingleton<IPdfShare>", program);
    }
}
