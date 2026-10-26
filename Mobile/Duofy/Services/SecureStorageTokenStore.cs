using Duofy.ApiClient;
using Microsoft.Extensions.Logging;

namespace Duofy.Services
{
    /// <summary>
    /// Bewahrt die Tokens im SecureStorage des Telefons auf (auf Android der Keystore).
    /// So bleibt die App nach dem Schließen angemeldet, und kein anderes Programm
    /// kann die Tokens lesen.
    /// </summary>
    public class SecureStorageTokenStore(ILogger<SecureStorageTokenStore> logger) : ITokenStore
    {
        private const string AccessKey = "duofy.access_token";
        private const string RefreshKey = "duofy.refresh_token";

        public async Task<AuthTokens?> GetAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var access = await SecureStorage.Default.GetAsync(AccessKey);
                var refresh = await SecureStorage.Default.GetAsync(RefreshKey);
                return access is null || refresh is null ? null : new AuthTokens(access, refresh);
            }
            catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or Java.Security.GeneralSecurityException or InvalidOperationException)
            {
                // Passiert zum Beispiel, wenn Android den Schlüssel nach einer Wiederherstellung
                // des Telefons nicht mehr kennt. Dann müssen sich die Leute neu anmelden.
                logger.LogWarning(ex, "Die gespeicherten Tokens konnten nicht gelesen werden und werden gelöscht.");
                SecureStorage.Default.RemoveAll();
                return null;
            }
        }

        public async Task SaveAsync(AuthTokens tokens, CancellationToken cancellationToken = default)
        {
            await SecureStorage.Default.SetAsync(AccessKey, tokens.AccessToken);
            await SecureStorage.Default.SetAsync(RefreshKey, tokens.RefreshToken);
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            SecureStorage.Default.Remove(AccessKey);
            SecureStorage.Default.Remove(RefreshKey);
            return Task.CompletedTask;
        }
    }
}
