using Duofy.Push;

namespace Duofy.Services.Push
{
    /// <summary>
    /// Die Kennung dieser Installation (Spec 84): Sie entsteht beim ersten Mal und liegt in den Preferences, bleibt also über
    /// den Wechsel des Push-Tokens hinweg gleich. Der Server erkennt daran das Gerät und ersetzt sein Token.
    /// </summary>
    public class PreferencesInstallationIdStore : IInstallationIdStore
    {
        private const string Key = "duofy.installation_id";

        public string GetOrCreate()
        {
            var id = Preferences.Default.Get<string?>(Key, null);
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N");
                Preferences.Default.Set(Key, id);
            }
            return id;
        }
    }
}
