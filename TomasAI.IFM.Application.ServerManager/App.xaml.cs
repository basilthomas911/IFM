using System;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TomasAI.IFM.Application.ServerManager;

/// <summary>
/// Interaction logic for App.xaml.
/// </summary>
public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;
    private ServerLauncherContext? _launcherContext;

    public IServiceProvider ServiceProvider => _serviceProvider
        ?? throw new InvalidOperationException("The application service provider has not been initialized.");

    public IConfiguration Configuration { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";
        Configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .Build();

        var options = Configuration.GetSection("ServerManager").Get<ServerManagerOptions>()
            ?? throw new InvalidOperationException("The ServerManager configuration section is missing.");
        options.Validate();

        var services = new ServiceCollection();
        ConfigureServices(services, options);
        _serviceProvider = services.BuildServiceProvider();

        _launcherContext = new ServerLauncherContext(
            this,
            options,
            ServiceProvider.GetRequiredService<IMainWindowViewModel>(),
            ServiceProvider.GetRequiredService<MainWindow>());
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _launcherContext?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _serviceProvider?.Dispose();
        }
        finally
        {
            base.OnExit(e);
        }
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _launcherContext?.PrepareForShutdown();
        base.OnSessionEnding(e);
    }

    private static void ConfigureServices(IServiceCollection services, ServerManagerOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
        services.AddSingleton<IMainWindowViewModel, MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
    }
}
