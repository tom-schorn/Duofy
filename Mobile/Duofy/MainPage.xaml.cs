using Duofy.Services.Push;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebView;
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
            blazorWebView.UrlLoading += OnUrlLoading;
        }

        // Was die Ansicht laden will, entscheidet ExternalLinks.Decide (Spec 109, A6): Die Oberfläche der App lädt wie bisher, eine
        // andere https-Seite (etwa das Impressum auf plagevo.de) öffnet den Browser des Telefons, alles andere (http, javascript:,
        // data:, intent:, file:) lädt nirgends. Links der App tragen kein target="_blank", weil ein solcher Link UrlLoading nicht
        // erreicht; sie laden in der Ansicht und kommen so hierher.
        private static void OnUrlLoading(object? sender, UrlLoadingEventArgs e)
        {
            e.UrlLoadingStrategy = Duofy.Shared.Services.ExternalLinks.Decide(e.Url.ToString()) switch
            {
                Duofy.Shared.Services.LinkDecision.StayInApp => UrlLoadingStrategy.OpenInWebView,
                Duofy.Shared.Services.LinkDecision.OpenSystemBrowser => UrlLoadingStrategy.OpenExternally,
                _ => UrlLoadingStrategy.CancelLoad,
            };
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
