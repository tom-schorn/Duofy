using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace Duofy.Api;

/// <summary>
/// Die Schlüssel, mit denen die API Access- und Refresh-Tokens und die Links aus Mails schützt (Spec 79, R5). Ohne
/// Einstellung entscheidet ASP.NET, wo sie liegen; in einem Container ist das ein Ordner, der mit dem Container
/// verschwindet, und jedes Update meldete alle ab.
///
/// Mit Duofy:KeysDirectory (Umgebungsvariable Duofy__KeysDirectory, im Container /keys auf einem Volume) liegen die
/// Schlüssel dort, und der Name der Anwendung ist fest "Duofy": Sonst leitet ASP.NET ihn aus dem Installationsordner ab,
/// und ein anderer Ordner hieße andere Schlüssel. Wer den Ordner liest, kann Tokens fälschen; er gehört so geschützt
/// wie die Umgebungsdatei.
/// </summary>
internal static class KeyRing
{
    public const string DirectoryKey = "Duofy:KeysDirectory";

    public const string ApplicationName = "Duofy";

    /// <summary>Liest den Ordner erst, wenn die Schlüssel gebraucht werden, damit Tests ihn vorher setzen können.</summary>
    public static IServiceCollection AddKeyRing(this IServiceCollection services)
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
