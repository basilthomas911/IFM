using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TomasAI.IFM.ServerManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public IServiceProvider ServiceProvider { get; private set; }

        public IConfiguration Configuration { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            var builder = new ConfigurationBuilder()
                 .SetBasePath(Directory.GetCurrentDirectory())
                 .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            Configuration = builder.Build();

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            ServiceProvider = serviceCollection.BuildServiceProvider();

            new ServerLauncherContext(this);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

        }

        void ConfigureServices(IServiceCollection services)
        {
            // ...
            services.AddSingleton<IMainWindowViewModel, MainWindowViewModel>();
            services.AddSingleton<MainWindow>();
        }
    }
}
