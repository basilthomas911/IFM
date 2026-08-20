using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace TomasAI.IFM.Application.ServerManager;

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
    private int _stopped;

    public ServerLauncherContext(
        App application,
        ServerManagerOptions options,
        IMainWindowViewModel viewModel,
        MainWindow console)
    {
        _application = application;
        _viewModel = viewModel;
        _console = console;
        _schedulerOptions = options.Scheduler;
        _supervisor = new ManagedProcessSupervisor(options.Processes, options.ShutdownTimeout, viewModel.AddLog);
        _notifyIcon = CreateNotifyIcon();

        _viewModel.ConsoleVisibility = Visibility.Hidden;
        _viewModel.ConsoleWindowState = WindowState.Minimized;
        console.Show();
        console.Hide();

        _application.Exit += OnApplicationExit;
        _ = StartProcessesAsync();
        _schedulerMonitor = MonitorSchedulerAsync(_schedulerMonitorCancellation.Token);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
        {
            return;
        }

        _schedulerMonitorCancellation.Cancel();
        try
        {
            await _schedulerMonitor.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected while stopping the periodic dashboard refresh.
        }

        _schedulerMonitorCancellation.Dispose();
        await _supervisor.DisposeAsync().ConfigureAwait(false);
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    public void PrepareForShutdown() => _console.PrepareForShutdown();

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
        notifyIcon.ContextMenuStrip.Items.Add("Reset API and UI", null, async (_, _) => await ResetProcessesAsync()).Name = "Reset";
        notifyIcon.ContextMenuStrip.Items.Add("Exit Server Manager", null, async (_, _) => await ExitAsync()).Name = "Exit";
        notifyIcon.DoubleClick += (_, _) => ViewConsole();
        return notifyIcon;
    }

    private void ViewConsole()
    {
        _notifyIcon.ContextMenuStrip!.Items["ViewConsole"]!.Enabled = false;
        _viewModel.ConsoleVisibility = Visibility.Visible;
        _viewModel.ConsoleWindowState = WindowState.Maximized;
        _console.Show();
        _console.Activate();
    }

    private void MinimizeConsole()
    {
        _notifyIcon.ContextMenuStrip!.Items["ViewConsole"]!.Enabled = true;
        _viewModel.ConsoleVisibility = Visibility.Hidden;
        _viewModel.ConsoleWindowState = WindowState.Minimized;
        _console.Hide();
    }

    private async Task StartProcessesAsync()
    {
        WriteManagerLog("Starting configured API/UI processes.");
        try
        {
            await _supervisor.StartAllAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            WriteManagerLog($"Process startup failed: {exception.Message}");
        }
    }

    private async Task ResetProcessesAsync()
    {
        WriteManagerLog("Reset requested for configured API/UI processes.");
        try
        {
            await _supervisor.RestartAllAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            WriteManagerLog($"Reset failed: {exception.Message}");
        }
    }

    private async Task MonitorSchedulerAsync(CancellationToken cancellationToken)
    {
        if (!_schedulerOptions.Enabled)
        {
            await _viewModel.RefreshSchedulerAsync(cancellationToken);
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_schedulerOptions.RefreshIntervalSeconds));
        do
        {
            await _viewModel.RefreshSchedulerAsync(cancellationToken);
        }
        while (await timer.WaitForNextTickAsync(cancellationToken));
    }

    private async Task ExitAsync()
    {
        WriteManagerLog("Server Manager exit requested.");
        PrepareForShutdown();
        await DisposeAsync();
        _application.Shutdown();
    }

    private void OnApplicationExit(object? sender, ExitEventArgs e)
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private void WriteManagerLog(string message)
        => _viewModel.AddLog(new ManagedProcessLogEntry(
            DateTimeOffset.Now,
            "manager",
            "Server Manager",
            ManagedProcessLogStream.Manager,
            message));
}
