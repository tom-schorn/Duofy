using Microsoft.AspNetCore.Components.WebView;

namespace Duofy
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
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
    }
}
