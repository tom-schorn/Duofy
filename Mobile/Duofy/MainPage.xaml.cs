using Duofy.Services.Push;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy
{
    public partial class MainPage : ContentPage
    {
        private NavigationRequests? requests;

        public MainPage()
        {
            InitializeComponent();
            blazorWebView.BlazorWebViewInitialized += (_, _) => OpenPending();
        }

        // Ein Tipp auf eine Benachrichtigung öffnet die App an der Adresse des Postens (Spec 84, R5). Kommt der Tipp vor der
        // Oberfläche, wartet die Adresse in NavigationRequests, und diese Seite holt sie beim Start ab.
        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();
            requests ??= Handler?.MauiContext?.Services.GetService<NavigationRequests>();
            if (requests is not null)
            {
                requests.Requested -= Open;
                requests.Requested += Open;
            }
        }

        private void OpenPending()
        {
            if (requests?.TakePending() is { } path)
            {
                Open(path);
            }
        }

        private void Open(string path)
        {
            if (!NavigationRequests.IsAppPath(path))
            {
                return;
            }
            Dispatcher.Dispatch(async () => await blazorWebView.TryDispatchAsync(services =>
                services.GetRequiredService<NavigationManager>().NavigateTo(path)));
        }
    }
}
