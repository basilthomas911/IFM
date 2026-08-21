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
        SchedulePreview = new ObservableCollection<ScheduleFirePreviewDto>();
        TaskOutput = new ObservableCollection<TaskOutputLineDto>();
        RefreshSchedulerCommand = new AsyncRelayCommand(() => RefreshSchedulerAsync());
        NewScheduleCommand = new RelayCommand(BeginNewSchedule);
        ValidateScheduleCommand = new AsyncRelayCommand(ValidateEditorAsync);
        SaveScheduleCommand = new AsyncRelayCommand(SaveScheduleAsync);
        ToggleScheduleCommand = new AsyncRelayCommand(ToggleScheduleAsync);
        DeleteScheduleCommand = new AsyncRelayCommand(DeleteScheduleAsync);
        RunNowCommand = new AsyncRelayCommand(RunNowAsync);
        CancelRunCommand = new AsyncRelayCommand(CancelRunAsync);
        RetryRunCommand = new AsyncRelayCommand(RetryRunAsync);
        LoadOutputCommand = new AsyncRelayCommand(LoadOutputAsync);
    }

    public ObservableCollection<StatusLog> ConsoleStatus { get; }

    public ObservableCollection<ManagedApplicationSummary> Applications { get; }

    public ObservableCollection<TaskCatalogItemDto> TaskCatalog { get; }

    public ObservableCollection<ScheduleSummaryDto> Schedules { get; }

    public ObservableCollection<TaskRunSummaryDto> TaskRuns { get; }

    public ObservableCollection<ScheduleFirePreviewDto> SchedulePreview { get; }

    public ObservableCollection<TaskOutputLineDto> TaskOutput { get; }

    public IReadOnlyList<ScheduleKind> ScheduleKinds { get; } = Enum.GetValues<ScheduleKind>();

    public IReadOnlyList<SchedulerMisfirePolicy> MisfirePolicies { get; } = Enum.GetValues<SchedulerMisfirePolicy>();

    public IReadOnlyList<TaskOutputStream> OutputStreams { get; } = Enum.GetValues<TaskOutputStream>();

    public IAsyncRelayCommand RefreshSchedulerCommand { get; }

    public IRelayCommand NewScheduleCommand { get; }

    public IAsyncRelayCommand ValidateScheduleCommand { get; }

    public IAsyncRelayCommand SaveScheduleCommand { get; }

    public IAsyncRelayCommand ToggleScheduleCommand { get; }

    public IAsyncRelayCommand DeleteScheduleCommand { get; }

    public IAsyncRelayCommand RunNowCommand { get; }

    public IAsyncRelayCommand CancelRunCommand { get; }

    public IAsyncRelayCommand RetryRunCommand { get; }

    public IAsyncRelayCommand LoadOutputCommand { get; }

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

    [ObservableProperty]
    private ScheduleSummaryDto? _selectedSchedule;

    [ObservableProperty]
    private TaskRunSummaryDto? _selectedTaskRun;

    [ObservableProperty]
    private Guid? _editorScheduleId;

    [ObservableProperty]
    private long? _editorVersion;

    [ObservableProperty]
    private string _editorName = string.Empty;

    [ObservableProperty]
    private string _editorDescription = string.Empty;

    [ObservableProperty]
    private string _editorTaskKey = string.Empty;

    [ObservableProperty]
    private ScheduleKind _editorKind = ScheduleKind.Cron;

    [ObservableProperty]
    private string _editorExpression = "0 0 0 ? * MON-FRI";

    [ObservableProperty]
    private string _editorTimeZoneId = "America/New_York";

    [ObservableProperty]
    private SchedulerMisfirePolicy _editorMisfirePolicy = SchedulerMisfirePolicy.DoNothing;

    [ObservableProperty]
    private int? _editorMaximumRuntimeSeconds;

    [ObservableProperty]
    private int _editorSuccessfulRetentionDays = 30;

    [ObservableProperty]
    private int _editorFailedRetentionDays = 180;

    [ObservableProperty]
    private string _operatorReason = string.Empty;

    [ObservableProperty]
    private string _schedulerOperationMessage = "Select a schedule or create a new disabled definition.";

    [ObservableProperty]
    private TaskOutputStream _selectedOutputStream = TaskOutputStream.StandardOutput;

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

    partial void OnSelectedScheduleChanged(ScheduleSummaryDto? value)
    {
        if (value is null)
        {
            return;
        }

        EditorScheduleId = value.ScheduleDefinitionId;
        EditorVersion = value.Version;
        EditorName = value.Name;
        EditorDescription = value.Description;
        EditorTaskKey = value.TaskKey;
        EditorKind = value.Kind;
        EditorExpression = value.ScheduleExpression;
        EditorTimeZoneId = value.TimeZoneId;
        EditorMisfirePolicy = value.MisfirePolicy;
        EditorMaximumRuntimeSeconds = value.MaximumRuntimeSeconds;
        EditorSuccessfulRetentionDays = value.SuccessfulRetentionDays;
        EditorFailedRetentionDays = value.FailedRetentionDays;
        SchedulePreview.Clear();
    }

    private void BeginNewSchedule()
    {
        SelectedSchedule = null;
        EditorScheduleId = null;
        EditorVersion = null;
        EditorName = string.Empty;
        EditorDescription = string.Empty;
        EditorTaskKey = TaskCatalog.FirstOrDefault()?.TaskKey ?? string.Empty;
        EditorKind = ScheduleKind.Cron;
        EditorExpression = "0 0 0 ? * MON-FRI";
        EditorTimeZoneId = "America/New_York";
        EditorMisfirePolicy = SchedulerMisfirePolicy.DoNothing;
        EditorMaximumRuntimeSeconds = null;
        EditorSuccessfulRetentionDays = 30;
        EditorFailedRetentionDays = 180;
        SchedulePreview.Clear();
        SchedulerOperationMessage = "New schedules are always saved disabled.";
    }

    private ScheduleDefinitionInputDto BuildEditorInput()
        => new(
            EditorScheduleId,
            EditorName,
            EditorDescription,
            EditorTaskKey,
            EditorKind,
            EditorExpression,
            EditorTimeZoneId,
            EditorMisfirePolicy,
            EditorMaximumRuntimeSeconds,
            EditorSuccessfulRetentionDays,
            EditorFailedRetentionDays);

    private async Task ValidateEditorAsync()
        => await ExecuteOperationAsync(async () =>
        {
            var validation = await _schedulerClient.ValidateScheduleAsync(BuildEditorInput(), CancellationToken.None);
            Replace(SchedulePreview, validation.NextFireTimes);
            SchedulerOperationMessage = validation.IsValid
                ? validation.Explanation
                : string.Join(" ", validation.Errors);
        });

    private async Task SaveScheduleAsync()
        => await ExecuteOperationAsync(async () =>
        {
            var input = BuildEditorInput();
            var result = EditorScheduleId is null
                ? await _schedulerClient.CreateScheduleAsync(input, CancellationToken.None)
                : await _schedulerClient.UpdateScheduleAsync(
                    input,
                    EditorVersion ?? throw new InvalidOperationException("The selected schedule has no concurrency version."),
                    CancellationToken.None);
            SchedulerOperationMessage = result.Message;
            await RefreshSchedulerAsync();
        });

    private async Task ToggleScheduleAsync()
        => await ExecuteOperationAsync(async () =>
        {
            var selected = SelectedSchedule ?? throw new InvalidOperationException("Select a schedule first.");
            var result = await _schedulerClient.SetScheduleEnabledAsync(
                selected.ScheduleDefinitionId,
                !selected.Enabled,
                selected.Version,
                OperatorReason,
                CancellationToken.None);
            SchedulerOperationMessage = result.Message;
            await RefreshSchedulerAsync();
        });

    private async Task DeleteScheduleAsync()
        => await ExecuteOperationAsync(async () =>
        {
            var selected = SelectedSchedule ?? throw new InvalidOperationException("Select a schedule first.");
            var result = await _schedulerClient.DeleteScheduleAsync(
                selected.ScheduleDefinitionId,
                selected.Version,
                OperatorReason,
                CancellationToken.None);
            SchedulerOperationMessage = result.Message;
            BeginNewSchedule();
            await RefreshSchedulerAsync();
        });

    private async Task RunNowAsync()
        => await ExecuteOperationAsync(async () =>
        {
            var selected = SelectedSchedule ?? throw new InvalidOperationException("Select a schedule first.");
            var result = await _schedulerClient.RunNowAsync(selected.ScheduleDefinitionId, OperatorReason, CancellationToken.None);
            SchedulerOperationMessage = $"{result.Message} Run: {result.RunId}";
            await RefreshSchedulerAsync();
        });

    private async Task CancelRunAsync()
        => await ExecuteOperationAsync(async () =>
        {
            var selected = SelectedTaskRun ?? throw new InvalidOperationException("Select a run first.");
            var result = await _schedulerClient.CancelRunAsync(selected.RunId, OperatorReason, CancellationToken.None);
            SchedulerOperationMessage = result.Message;
            await RefreshSchedulerAsync();
        });

    private async Task RetryRunAsync()
        => await ExecuteOperationAsync(async () =>
        {
            var selected = SelectedTaskRun ?? throw new InvalidOperationException("Select a run first.");
            var result = await _schedulerClient.RetryRunAsync(selected.RunId, OperatorReason, CancellationToken.None);
            SchedulerOperationMessage = $"{result.Message} Run: {result.RunId}";
            await RefreshSchedulerAsync();
        });

    private async Task LoadOutputAsync()
        => await ExecuteOperationAsync(async () =>
        {
            var selected = SelectedTaskRun ?? throw new InvalidOperationException("Select a run first.");
            var page = await _schedulerClient.GetRunOutputAsync(
                new RunOutputRequestDto(selected.RunId, SelectedOutputStream, 0, 500),
                CancellationToken.None);
            Replace(TaskOutput, page.Lines);
            SchedulerOperationMessage = page.Retained
                ? $"Loaded {page.Lines.Count} output line(s){(page.Truncated ? "; output was truncated" : string.Empty)}."
                : "Output has been removed by retention.";
        });

    private async Task ExecuteOperationAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            SchedulerOperationMessage = exception.Message;
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
