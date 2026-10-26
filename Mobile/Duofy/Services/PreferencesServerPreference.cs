using Duofy.ApiClient;

namespace Duofy.Services
{
    /// <summary>
    /// Merkt sich den gewählten Server in den Preferences des Telefons, also über das
    /// Schließen der App hinaus. Die Tokens liegen getrennt im SecureStorage.
    /// </summary>
    public class PreferencesServerPreference : IServerPreference
    {
        private const string Key = "duofy.server_address";

        public string? Load() => Preferences.Default.ContainsKey(Key)
            ? Preferences.Default.Get<string?>(Key, null)
            : null;

        public void Save(string? address)
        {
            if (address is null)
            {
                Preferences.Default.Remove(Key);
            }
            else
            {
                Preferences.Default.Set(Key, address);
            }
        }
    }
}
