using Duofy.ApiClient;
using Duofy.Push;
using Duofy.Services.Push;
using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duofy
{
    public partial class App : Application
    {
        private readonly AppBrand brand;

        public App(AppBrand brand)
        {
            this.brand = brand;
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new MainPage()) { Title = brand.Name };
        }

        // Erinnerungen (Spec 84): Bei jedem Start meldet die App ihr Gerät erneut an, wenn jemand angemeldet ist; so folgt
        // der Server einem geänderten Token. Ein Fehler hält den Start nicht auf, er steht im Log.
        protected override void OnStart()
        {
            base.OnStart();
            _ = RegisterDeviceAsync();
        }

        private static async Task RegisterDeviceAsync()
        {
            try
            {
                await PushServices.RunAsync(async services =>
                {
                    if (await services.GetRequiredService<ITokenStore>().GetAsync() is not null)
                    {
                        await services.GetRequiredService<DeviceRegistrar>().RegisterAsync();
                    }
                });
            }
            catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or TaskCanceledException or IOException)
            {
                PushServices.Root?.GetService<ILogger<App>>()?.LogWarning(exception, "Das Gerät wurde beim Start nicht für Erinnerungen angemeldet.");
            }
        }
    }
}
