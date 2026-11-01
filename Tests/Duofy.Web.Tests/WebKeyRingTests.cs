using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Web.Tests;

/// <summary>
/// Die Schlüssel der Web-Variante (Issue #38, Nebenbefund): Sie schützen die Formulare gegen fremde Absender und den
/// Zustand der Seiten. Mit Duofy:KeysDirectory, im Container ein eigenes Volume, überleben sie einen Neubau des
/// Containers. Die Anmeldung hängt nicht daran: Die Tokens liegen im Arbeitsspeicher (SessionTokenCache).
/// </summary>
public sealed class WebKeyRingTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string Purpose = "test";

    private readonly WebApplicationFactory<Program> factory;
    private readonly string keys = Directory.CreateTempSubdirectory("duofy-web-keys-").FullName;
    private readonly string otherKeys = Directory.CreateTempSubdirectory("duofy-web-keys-").FullName;

    public WebKeyRingTests(WebApplicationFactory<Program> factory) => this.factory = factory;

    private WebApplicationFactory<Program> Web(string directory) =>
        factory.WithWebHostBuilder(builder => builder.UseSetting("Duofy:KeysDirectory", directory));

    private static IDataProtector Protector(WebApplicationFactory<Program> web) =>
        web.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose);

    [Fact]
    public void What_the_first_run_protected_can_be_read_by_the_second_with_the_same_keys()
    {
        using var first = Web(keys);
        var secret = Protector(first).Protect("Formular");

        using var second = Web(keys);

        Assert.Equal("Formular", Protector(second).Unprotect(secret));
        Assert.NotEmpty(Directory.GetFiles(keys, "key-*.xml"));
    }

    // Die Gegenprobe: Mit anderen Schlüsseln geht es nicht; der Test oben prüft also wirklich den Ordner.
    [Fact]
    public void Other_keys_cannot_read_it()
    {
        using var first = Web(keys);
        var secret = Protector(first).Protect("Formular");

        using var second = Web(otherKeys);

        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(() => Protector(second).Unprotect(secret));
    }

    public void Dispose()
    {
        Directory.Delete(keys, recursive: true);
        Directory.Delete(otherKeys, recursive: true);
    }
}
