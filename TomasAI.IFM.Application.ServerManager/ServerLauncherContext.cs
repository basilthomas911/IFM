using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using TomasAI.IFM.Application.ServerManager.Contracts;
using System.Windows;
using Serilog;
using Serilog.Events;
using WinForms = System.Windows.Forms;

namespace TomasAI.IFM.Application.ServerManager;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="Preserve the existing public host, configuration and reflection contracts used by external test assemblies.")]

public sealed class ServerLauncherContext : IAsyncDisposable
{
    private readonly App _application;
    private readonly IMainWindowViewModel _viewModel;
    private readonly ManagedProcessSupervisor _supervisor;
    private readonly WinForms.NotifyIcon _notifyIcon;
    private readonly MainWindow _console;
    private readonly SchedulerClientOptions _schedulerOptions;
    private readonly CancellationTokenSource _schedulerMonitorCancellation = new();
    private readonly Task _schedulerMonitor;
    private readonly DevelopmentProcessSession? _developmentSession;
    private readonly DevelopmentControlPipe? _developmentControlPipe;
    private int _stopped;
    private readonly CancellationTokenSource _apiRecoveryCancellation = new();
    private readonly Task _apiRecovery = Task.CompletedTask;

    public ServerLauncherContext(
        App application,
        ServerManagerOptions options,
        IMainWindowViewModel viewModel,
        MainWindow console,
        bool enableDevelopmentProcessOwnership)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(options);
        _application = application;
        _viewModel = viewModel;
        _console = console;
        _schedulerOptions = options.Scheduler;
        if (enableDevelopmentProcessOwnership)
        {
            _developmentSession = new DevelopmentProcessSession();
            _developmentSession.MarkDefinitions(options.Processes);
            if (options.ApiRecoveryRestartEnabled)
            {
                var api = options.Processes.Single(process => process.Key == "api");
                var directory = Path.Combine(Path.GetDirectoryName(DevelopmentProcessSession.SessionFilePath)!, "ApiRecovery", _developmentSession.SessionId);
                Directory.CreateDirectory(directory);
                api.EnvironmentVariables[ApiRecoveryRestartProtocol.DirectoryVariable] = directory;
                api.EnvironmentVariables[ApiRecoveryRestartProtocol.ControllerVariable] = "1";
            }
        }

        _supervisor = new ManagedProcessSupervisor(
            options.Processes,
            options.ShutdownTimeout,
            WriteLog,
            useDevelopmentKillOnCloseJob: enableDevelopmentProcessOwnership,
            runningProcessesChanged: _developmentSession is null
                ? null
                : processes => _developmentSession.Record(processes));
        _notifyIcon = CreateNotifyIcon();
        if (_developmentSession is not null && options.ApiRecoveryRestartEnabled)
        {
            var api = options.Processes.Single(process => process.Key == "api");
            var environment = api.EnvironmentVariables.GetValueOrDefault("DOTNET_ENVIRONMENT") ?? "Development";
            var configuration = new ConfigurationBuilder().SetBasePath(api.ResolveWorkingDirectory())
                .AddJsonFile("appsettings.json", optional:true).AddJsonFile($"appsettings.{environment}.json", optional:true).Build();
            var url = configuration["Nats:Url"] ?? "nats://localhost:4222";
            var readiness = new Uri(new Uri(api.ReadinessUri!), "/health/recovery-ready");
            var controller = new ApiRecoveryRestartController(api.EnvironmentVariables[ApiRecoveryRestartProtocol.DirectoryVariable], _developmentSession.SessionId,
                (request,token) => _supervisor.RestartApiAsync(request.ProcessId,request.ProcessStartedUtc,token),
                async (request,pid,token) => {
                    using var client = new HttpClient { Timeout=Timeout.InfiniteTimeSpan };
                    while (true) {
                        token.ThrowIfCancellationRequested();
                        try {
                            var state = await client.GetFromJsonAsync<ApiRecoveryReadiness>(readiness,token).ConfigureAwait(false);
                            if (state is { FeedsRestored:true } && state.ProcessId==pid && state.ValueDate==request.ValueDate
                                && request.TradeIds.All(id => state.ProvenTradeIds.Contains(id,StringComparer.Ordinal))) return;
                        } catch (HttpRequestException) { }
                        await Task.Delay(250,token).ConfigureAwait(false);
                    }
                }, (result,token) => ApiRecoveryUiNotifier.SendAsync(url,result,token), message => WriteManagerLog(message));
            _apiRecovery = controller.RunAsync(_apiRecoveryCancellation.Token);
            WriteManagerLog("API-only recovery restart controller enabled: outstanding/failed restart blocks another attempt until verified recovery; 120-second restart/restore deadline.");
        }

        _viewModel.ConsoleVisibility = Visibility.Hidden;
        _viewModel.ConsoleWindowState = WindowState.Minimized;
        console.Show();
        console.Hide();

        _application.Exit += OnApplicationExit;
        if (enableDevelopmentProcessOwnership)
        {
            _developmentControlPipe = new DevelopmentControlPipe(RequestExitFromDevelopmentControlPipe);
        }

        _ = StartProcessesAsync();
        _schedulerMonitor = MonitorSchedulerAsync(_schedulerMonitorCancellation.Token);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
        {
            return;
        }

        if (_developmentControlPipe is not null)
        {
            await _developmentControlPipe.DisposeAsync().ConfigureAwait(false);
        }

        await _apiRecoveryCancellation.CancelAsync().ConfigureAwait(false);
        await _apiRecovery.ConfigureAwait(false);
        _apiRecoveryCancellation.Dispose();

        await _schedulerMonitorCancellation.CancelAsync().ConfigureAwait(true);
        try
        {
            await _schedulerMonitor.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected while stopping the periodic dashboard refresh.
        }

        _schedulerMonitorCancellation.Dispose();
        try
        {
            await _supervisor.DisposeAsync().ConfigureAwait(false);
            _developmentSession?.Clear();
        }
        finally
        {
            _developmentSession?.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }

    public void PrepareForShutdown() => _console.PrepareForShutdown();
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1303",Justification="The existing Development UI uses fixed English captions.")]

    private WinForms.NotifyIcon CreateNotifyIcon()
    {
        var notifyIcon = new WinForms.NotifyIcon
        {
            Icon = Resource1.AppIcon,
            Text = "IFM Server Manager",
            Visible = true,
            ContextMenuStrip = new WinForms.ContextMenuStrip()
        };

        notifyIcon.ContextMenuStrip.Items.Add("View Console", null, (_, _) => ViewConsole()).Name = "ViewConsole";
        notifyIcon.ContextMenuStrip.Items.Add("Minimize Console", null, (_, _) => MinimizeConsole()).Name = "MinimizeConsole";
        notifyIcon.ContextMenuStrip.Items.Add("Reset API and UI", null, async (_, _) => await (ResetProcessesAsync()).ConfigureAwait(true)).Name = "Reset";
        notifyIcon.ContextMenuStrip.Items.Add("Exit Server Manager", null, async (_, _) => await (ExitAsync()).ConfigureAwait(true)).Name = "Exit";
        notifyIcon.DoubleClick += (_, _) => ViewConsole();
        return notifyIcon;
    }

    private void ViewConsole()
    {
        _viewModel.ConsoleVisibility = Visibility.Visible;
        _viewModel.ConsoleWindowState = WindowState.Maximized;
        _console.Show();
        _console.WindowState = WindowState.Maximized;
        _console.Activate();
    }

    private void MinimizeConsole()
    {
        _viewModel.ConsoleVisibility = Visibility.Hidden;
        _viewModel.ConsoleWindowState = WindowState.Minimized;
        _console.Hide();
    }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1031",Justification="This process/UI boundary reports failures through its existing logging or error result while completing cleanup.")]

    private async Task StartProcessesAsync()
    {
        WriteManagerLog("Starting configured application processes.");
        try
        {
            if (_developmentSession is not null)
            {
                await _developmentSession.ReconcilePreviousSessionAsync(message => WriteManagerLog(message)).ConfigureAwait(false);
                _developmentSession.Record([]);
            }

            await _supervisor.StartAllAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            WriteManagerLog("Process startup failed.", exception);
        }
    }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1031",Justification="This process/UI boundary reports failures through its existing logging or error result while completing cleanup.")]

    private async Task ResetProcessesAsync()
    {
        WriteManagerLog("Reset requested for configured application processes.");
        try
        {
            await _supervisor.RestartAllAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            WriteManagerLog("Reset failed.", exception);
        }
    }

    private async Task MonitorSchedulerAsync(CancellationToken cancellationToken)
    {
        if (!_schedulerOptions.Enabled)
        {
            await (_viewModel.RefreshSchedulerAsync(cancellationToken)).ConfigureAwait(true);
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_schedulerOptions.RefreshIntervalSeconds));
        do
        {
            await (_viewModel.RefreshSchedulerAsync(cancellationToken)).ConfigureAwait(true);
        }
        while (await (timer.WaitForNextTickAsync(cancellationToken)).ConfigureAwait(true));
    }

    private async Task ExitAsync()
    {
        WriteManagerLog("Server Manager exit requested.");
        PrepareForShutdown();
        await (DisposeAsync()).ConfigureAwait(true);
        _application.Shutdown();
    }

    private void RequestExitFromDevelopmentControlPipe()
        => _application.Dispatcher.BeginInvoke(new Action(() => _ = ExitAsync()));

    private void OnApplicationExit(object? sender, ExitEventArgs e)
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private void WriteManagerLog(string message, Exception? exception = null)
        => WriteLog(new ManagedProcessLogEntry(
            DateTimeOffset.Now,
            "manager",
            "Server Manager",
            ManagedProcessLogStream.Manager,
            message,
            exception));

    private void WriteLog(ManagedProcessLogEntry entry)
    {
        _viewModel.AddLog(entry);
        var level = entry.Stream == ManagedProcessLogStream.StandardError || entry.Exception is not null
            ? LogEventLevel.Error
            : LogEventLevel.Information;
        Log.ForContext("ProcessKey", entry.ProcessKey)
            .ForContext("ProcessName", entry.ProcessName)
            .ForContext("ProcessLogStream", entry.Stream)
            .Write(level, entry.Exception, "{ProcessMessage}", entry.Message);
    }
}
