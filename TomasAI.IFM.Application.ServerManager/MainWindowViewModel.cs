using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager;

public sealed partial class MainWindowViewModel : ObservableObject, IMainWindowViewModel
{
    private readonly int _maximumLogEntries;
    private readonly IUiDispatcher _dispatcher;
    private readonly ISchedulerDashboardClient _schedulerClient;
    private readonly ConcurrentQueue<ManagedProcessLogEntry> _pendingEntries = new();
    private int _pendingCount;
    private int _drainScheduled;
    private int _droppedSinceLastDrain;

    public MainWindowViewModel(
        ServerManagerOptions options,
        IUiDispatcher dispatcher,
        ISchedulerDashboardClient schedulerClient)
    {
        _maximumLogEntries = options.MaximumLogEntries;
        _dispatcher = dispatcher;
        _schedulerClient = schedulerClient;
        ConsoleStatus = new ObservableCollection<StatusLog>();
        Applications = new ObservableCollection<ManagedApplicationSummary>(options.Processes.Select(process => new ManagedApplicationSummary(
            process.Key,
            process.DisplayName,
            process.ResolveExecutablePath(),
            process.Enabled,
            process.StartOrder)));
        TaskCatalog = new ObservableCollection<TaskCatalogItemDto>();
        Schedules = new ObservableCollection<ScheduleSummaryDto>();
        TaskRuns = new ObservableCollection<TaskRunSummaryDto>();
        RefreshSchedulerCommand = new AsyncRelayCommand(() => RefreshSchedulerAsync());
    }

    public ObservableCollection<StatusLog> ConsoleStatus { get; }

    public ObservableCollection<ManagedApplicationSummary> Applications { get; }

    public ObservableCollection<TaskCatalogItemDto> TaskCatalog { get; }

    public ObservableCollection<ScheduleSummaryDto> Schedules { get; }

    public ObservableCollection<TaskRunSummaryDto> TaskRuns { get; }

    public IAsyncRelayCommand RefreshSchedulerCommand { get; }

    [ObservableProperty]
    private Visibility _consoleVisibility;

    [ObservableProperty]
    private WindowState _consoleWindowState;

    [ObservableProperty]
    private string _schedulerState = "Connecting";

    [ObservableProperty]
    private string _schedulerMessage = "Waiting for Scheduler Host.";

    [ObservableProperty]
    private string _schedulerVersion = "unknown";

    [ObservableProperty]
    private bool _schedulerDatabaseAvailable;

    [ObservableProperty]
    private bool _quartzAvailable;

    [ObservableProperty]
    private bool _schedulingStarted;

    [ObservableProperty]
    private DateTimeOffset? _schedulerLastRefreshedUtc;

    public void AddLog(ManagedProcessLogEntry entry)
    {
        _pendingEntries.Enqueue(entry);
        var pendingCount = Interlocked.Increment(ref _pendingCount);
        while (pendingCount > _maximumLogEntries && _pendingEntries.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _pendingCount);
            Interlocked.Increment(ref _droppedSinceLastDrain);
            pendingCount = Volatile.Read(ref _pendingCount);
        }

        if (Interlocked.CompareExchange(ref _drainScheduled, 1, 0) == 0)
        {
            _dispatcher.Post(DrainPendingEntries);
        }
    }

    public async Task RefreshSchedulerAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var dashboard = await _schedulerClient.GetDashboardAsync(cancellationToken);
            _dispatcher.Post(() => ApplyDashboard(dashboard));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal Server Manager shutdown cancels an in-flight refresh.
        }
        catch (Exception exception)
        {
            _dispatcher.Post(() =>
            {
                SchedulerState = "Offline";
                SchedulerMessage = exception.Message;
                SchedulerDatabaseAvailable = false;
                QuartzAvailable = false;
                SchedulingStarted = false;
                SchedulerLastRefreshedUtc = DateTimeOffset.UtcNow;
                TaskCatalog.Clear();
                Schedules.Clear();
                TaskRuns.Clear();
            });
        }
    }

    private void ApplyDashboard(SchedulerDashboardDto dashboard)
    {
        SchedulerState = dashboard.Health.State.ToString();
        SchedulerMessage = dashboard.Health.Message;
        SchedulerVersion = dashboard.Health.Version;
        SchedulerDatabaseAvailable = dashboard.Health.DatabaseAvailable;
        QuartzAvailable = dashboard.Health.QuartzAvailable;
        SchedulingStarted = dashboard.Health.SchedulingStarted;
        SchedulerLastRefreshedUtc = dashboard.GeneratedAtUtc;
        Replace(TaskCatalog, dashboard.TaskCatalog);
        Replace(Schedules, dashboard.Schedules);
        Replace(TaskRuns, dashboard.RecentRuns);
    }

    private static void Replace<T>(ObservableCollection<T> destination, IEnumerable<T> source)
    {
        destination.Clear();
        foreach (var item in source)
        {
            destination.Add(item);
        }
    }

    private void DrainPendingEntries()
    {
        while (true)
        {
            var dropped = Interlocked.Exchange(ref _droppedSinceLastDrain, 0);
            while (_pendingEntries.TryDequeue(out var entry))
            {
                Interlocked.Decrement(ref _pendingCount);
                Insert(entry);
            }

            if (dropped > 0)
            {
                Insert(new ManagedProcessLogEntry(
                    DateTimeOffset.Now,
                    "manager",
                    "Server Manager",
                    ManagedProcessLogStream.Manager,
                    $"Dropped {dropped} pending log entries because the UI log buffer was full."));
            }

            Interlocked.Exchange(ref _drainScheduled, 0);
            if (_pendingEntries.IsEmpty || Interlocked.CompareExchange(ref _drainScheduled, 1, 0) != 0)
            {
                return;
            }
        }
    }

    private void Insert(ManagedProcessLogEntry entry)
    {
        ConsoleStatus.Insert(0, new StatusLog
        {
            Timestamp = entry.Timestamp,
            ProcessName = entry.ProcessName,
            Stream = entry.Stream,
            Message = entry.Message
        });

        while (ConsoleStatus.Count > _maximumLogEntries)
        {
            ConsoleStatus.RemoveAt(ConsoleStatus.Count - 1);
        }
    }
}

public sealed record ManagedApplicationSummary(
    string Key,
    string DisplayName,
    string ExecutablePath,
    bool Enabled,
    int StartOrder);
