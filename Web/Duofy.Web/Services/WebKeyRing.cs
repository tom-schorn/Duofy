using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace Duofy.Web.Services;

/// <summary>
/// Die Schlüssel der Web-Variante: Sie schützen die Formulare gegen fremde Absender (Antiforgery) und den Zustand der
/// Seiten. Ohne Einstellung liegen sie in einem Ordner, der mit dem Container verschwindet.
///
/// Mit Duofy:KeysDirectory (Umgebungsvariable Duofy__KeysDirectory, im Container /keys auf dem eigenen Volume web-keys)
/// überleben sie einen Neubau. Sie sind bewusst nicht die der API: Die schützen Tokens, und die Web-Variante braucht sie
/// nicht. Deshalb auch ein eigener fester Name der Anwendung, wie bei der API (KeyRing), damit ein anderer
/// Installationsordner nicht andere Schlüssel heißt. Die Anmeldung hängt nicht daran, die Tokens liegen im
/// Arbeitsspeicher (SessionTokenCache).
/// </summary>
internal static class WebKeyRing
{
    public const string DirectoryKey = "Duofy:KeysDirectory";

    public const string ApplicationName = "Duofy.Web";

    /// <summary>Liest den Ordner erst, wenn die Schlüssel gebraucht werden, damit Tests ihn vorher setzen können.</summary>
    public static IServiceCollection AddWebKeyRing(this IServiceCollection services)
    {
        services.AddDataProtection();
        services.AddOptions<KeyManagementOptions>().Configure<IConfiguration, ILoggerFactory>((options, configuration, loggers) =>
        {
            if (Directory(configuration) is { } directory)
            {
                options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(directory), loggers);
            }
        });
        services.AddOptions<DataProtectionOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            if (Directory(configuration) is not null)
            {
                options.ApplicationDiscriminator = ApplicationName;
            }
        });
        return services;
    }

    private static string? Directory(IConfiguration configuration) =>
        configuration[DirectoryKey]?.Trim() is { Length: > 0 } directory ? directory : null;
}
