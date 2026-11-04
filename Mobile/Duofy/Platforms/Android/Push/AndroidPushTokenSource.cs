using Android.Gms.Extensions;
using Duofy.Push;
using Duofy.Schemas;
using Firebase;
using Firebase.Messaging;

namespace Duofy.Services.Push
{
    /// <summary>
    /// Das FCM-Token dieses Telefons. Ohne die Firebase-Datei (google-services.json wird nie eingecheckt; sie kommt aus dem
    /// Build des Betreibers) gibt es kein Token: Die App läuft dann ohne Erinnerungen und sagt es im Log.
    /// </summary>
    public class AndroidPushTokenSource : IPushTokenSource
    {
        public string Platform => DevicePlatforms.Android;

        public async Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
        {
            if (FirebaseApp.GetApps(Android.App.Application.Context).Count == 0)
            {
                Android.Util.Log.Info("Duofy", "Firebase ist nicht eingerichtet (google-services.json fehlt); keine Erinnerungen auf diesem Gerät.");
                return null;
            }
            var token = await FirebaseMessaging.Instance.GetToken().AsAsync<Java.Lang.String>();
            return token?.ToString();
        }
    }
}
