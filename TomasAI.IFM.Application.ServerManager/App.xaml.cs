using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

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

        ConfigureBootstrapLogging();
        RegisterUnhandledExceptionLogging();

        try
        {
            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";
            ConfigureDevelopmentRepositoryRoot(environment);
            Configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(Configuration)
                .Enrich.FromLogContext()
                .CreateLogger();
            Log.Information("IFM Server Manager starting in {Environment} from {BaseDirectory}.", environment, AppContext.BaseDirectory);

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
        catch (Exception exception)
        {
            Log.Fatal(exception, "IFM Server Manager failed during startup.");
            Log.CloseAndFlush();
            throw;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Log.Information("IFM Server Manager stopping with exit code {ExitCode}.", e.ApplicationExitCode);
            _launcherContext?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _serviceProvider?.Dispose();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "IFM Server Manager failed while stopping.");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
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

    private static void ConfigureBootstrapLogging()
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TomasAI",
            "IFM",
            "ServerManager",
            "Logs");
        Directory.CreateDirectory(logDirectory);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.Debug()
            .WriteTo.File(
                Path.Combine(logDirectory, "server-manager-bootstrap_.log"),
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: 5,
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1))
            .CreateLogger();
    }

    private void RegisterUnhandledExceptionLogging()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        => Log.Fatal(e.Exception, "Unhandled WPF dispatcher exception.");

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        => Log.Fatal(e.ExceptionObject as Exception, "Unhandled application-domain exception. Terminating: {IsTerminating}.", e.IsTerminating);

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        => Log.Error(e.Exception, "Unobserved task exception.");

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
