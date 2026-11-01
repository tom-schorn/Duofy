using Duofy.Shared.Services;

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
    }
}
