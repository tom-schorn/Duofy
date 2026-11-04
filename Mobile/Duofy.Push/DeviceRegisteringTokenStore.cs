using Duofy.ApiClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duofy.Push;

/// <summary>
/// Legt sich um die Ablage der Tokens und meldet das Gerät beim Anmelden an und beim Abmelden ab (Spec 84). Angemeldet
/// wird nur, wenn vorher niemand angemeldet war: Das Erneuern der Tokens speichert auch, soll aber nicht jedes Mal
/// anmelden. Abgemeldet wird vor dem Löschen, solange die Anmeldung für den Server noch gilt. Ein Fehler hält weder das
/// Anmelden noch das Abmelden auf.
/// </summary>
public sealed class DeviceRegisteringTokenStore(ITokenStore inner, IServiceProvider services, ILogger<DeviceRegisteringTokenStore> logger) : ITokenStore
{
    public Task<AuthTokens?> GetAsync(CancellationToken cancellationToken = default) => inner.GetAsync(cancellationToken);

    public async Task SaveAsync(AuthTokens tokens, CancellationToken cancellationToken = default)
    {
        var wasSignedIn = await inner.GetAsync(cancellationToken) is not null;
        await inner.SaveAsync(tokens, cancellationToken);
        if (!wasSignedIn)
        {
            await RunAsync(registrar => registrar.RegisterAsync(cancellationToken), "angemeldet");
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (await inner.GetAsync(cancellationToken) is not null)
        {
            await RunAsync(registrar => registrar.RemoveAsync(cancellationToken), "abgemeldet");
        }
        await inner.ClearAsync(cancellationToken);
    }

    private async Task RunAsync(Func<DeviceRegistrar, Task<bool>> action, string what)
    {
        try
        {
            using var scope = services.CreateScope();
            await action(scope.ServiceProvider.GetRequiredService<DeviceRegistrar>());
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or TaskCanceledException or IOException)
        {
            logger.LogWarning(exception, "Das Gerät wurde nicht {What}; die App läuft weiter.", what);
        }
    }
}
