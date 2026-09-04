using System;
using System.IO;
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
        ConfigureDevelopmentRepositoryRoot(environment);
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
            ServiceProvider.GetRequiredService<MainWindow>(),
            options.DevelopmentProcessOwnershipEnabled
                && string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase));
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
        services.AddSingleton(options.Scheduler);
        services.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
        services.AddSingleton<ISchedulerDashboardClient, SchedulerPipeClient>();
        services.AddSingleton<IMainWindowViewModel, MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
    }

    private static void ConfigureDevelopmentRepositoryRoot(string environment)
    {
        if (!string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IFM_REPOSITORY_ROOT")))
        {
            return;
        }

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TomasAI.IFM.sln")))
            {
                Environment.SetEnvironmentVariable("IFM_REPOSITORY_ROOT", directory.FullName);
                return;
            }
        }
    }
}
