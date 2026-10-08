using TomasAI.IFM.UI.Net.Models.SystemAdmin;
using TomasAI.IFM.UI.Net.Services.SystemAdmin;
using TomasAI.IFM.UI.Net.Services.Subscriptions;
using TomasAI.IFM.UI.Net.Services.Operations;
namespace TomasAI.IFM.UI.Net.ViewModels.SystemAdmin;
/// <summary>Coordinates task editing, persisted refreshes and public-event observation.</summary>
public sealed class ScheduledTasksViewModel : IAsyncDisposable
{
    private readonly IScheduledTaskService _service;
    private readonly IUiEventSubscription _subscription;
    private readonly SemaphoreSlim _operation = new(1, 1);
    private readonly CancellationTokenSource _stopping = new();
    private int _refreshPending;
    /// <summary>Initializes the service boundary and an independently owned listener.</summary>
    public ScheduledTasksViewModel(IScheduledTaskService service)
    {
        _service = service;
        _subscription = service.CreateNotificationSubscription(_ => { RefreshRequested?.Invoke(); return ValueTask.CompletedTask; });
    }
    /// <summary>Gets immutable dashboard state loaded through query actors.</summary>
    public ScheduledTaskDashboardUiModel State { get; private set; } = new([], [], [], "Loading host capability.", null);
    /// <summary>Gets or sets the target environment.</summary>
    public string Environment { get; set; } = "Development";
    /// <summary>Gets or sets the target runtime host.</summary>
    public string HostId { get; set; } = "development";
    /// <summary>Gets or sets the selected schedule for bounded run history.</summary>
    public Guid? SelectedScheduleId { get; set; }
    /// <summary>Gets whether an API operation is running.</summary>
    public bool IsBusy { get; private set; }
    /// <summary>Raised when immutable state or busy state changes.</summary>
    public event Action? StateChanged;
    /// <summary>Raised by event callbacks; the view marshals the refresh onto its UI thread.</summary>
    public event Action? RefreshRequested;
    /// <summary>Raised with safe service errors.</summary>
    public event Action<string>? Error;
    /// <summary>Starts notifications and loads the current persisted dashboard.</summary>
    public async Task InitializeAsync()
    { await _subscription.StartAsync(_stopping.Token); await RefreshAsync(); }
    /// <summary>Loads a bounded dashboard without concurrent query refreshes.</summary>
    public async Task RefreshAsync()
    {
        if (_stopping.IsCancellationRequested) return;
        Interlocked.Exchange(ref _refreshPending, 1);
        if (!await _operation.WaitAsync(0, _stopping.Token)) return;
        IsBusy = true; StateChanged?.Invoke();
        try
        {
            do
            {
                Interlocked.Exchange(ref _refreshPending, 0);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
                deadline.CancelAfter(TimeSpan.FromSeconds(15));
                var result = await _service.LoadAsync(Environment, HostId, SelectedScheduleId, deadline.Token);
                if (!result.IsSuccess || result.Value is null) Error?.Invoke(result.Error?.Message ?? "Scheduled tasks could not be loaded.");
                else State = result.Value;
            } while (!_stopping.IsCancellationRequested && Volatile.Read(ref _refreshPending) != 0);
        }
        finally { IsBusy = false; _operation.Release(); StateChanged?.Invoke(); }
    }
    /// <summary>Reads bounded log history through query actors with view-owned cancellation.</summary>
    public async Task<ScheduledTaskRunPageUiModel?> LoadRunHistoryAsync(Guid scheduleId, byte[]? pagingState)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        var result = await _service.LoadRunHistoryAsync(Environment, HostId, scheduleId, pagingState, deadline.Token);
        if (!result.IsSuccess) Error?.Invoke(result.Error?.Message ?? "Run history is unavailable.");
        return result.Value;
    }
    /// <summary>Reads retained stdout while allowing a selection change to cancel the old request.</summary>
    public async Task<ScheduledTaskOutputPageUiModel?> LoadOutputAsync(Guid scheduleId, ScheduledTaskRunUiModel run, long offset, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token, cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        var result = await _service.LoadOutputAsync(Environment, HostId, scheduleId, run, offset, deadline.Token);
        if (!result.IsSuccess) Error?.Invoke(result.Error?.Message ?? "Retained stdout is unavailable.");
        return result.Value;
    }
    /// <summary>Submits create/change and refreshes the projected definition after acceptance.</summary>
    public Task SaveAsync(Guid? id, long revision, ScheduledTaskScheduleUiModel schedule) => ExecuteAsync(token => _service.SaveAsync(id, revision, schedule, token));
    /// <summary>Requests enable/disable with optimistic source revision.</summary>
    public Task SetEnabledAsync(Guid id, long revision, bool enabled) => ExecuteAsync(token => _service.SetEnabledAsync(id, revision, enabled, token));
    /// <summary>Removes a disabled schedule and refreshes persisted state.</summary>
    public Task RemoveAsync(Guid id, long revision) => ExecuteAsync(token => _service.RemoveAsync(id, revision, token));
    /// <summary>Requests an occurrence without bypassing runtime installation and overlap guards.</summary>
    public Task RunNowAsync(ScheduledTaskDefinitionUiModel definition) => ExecuteAsync(token => _service.RunNowAsync(definition, token));
    /// <summary>Resolves an uncertain run with an explicit operator reason; no task is automatically rerun.</summary>
    public Task ResolveUncertainAsync(ScheduledTaskRunUiModel run, string reason) => ExecuteAsync(token => _service.ResolveUncertainAsync(run, reason, token));
    /// <summary>Obtains portable cron/date preview without mutating a schedule.</summary>
    public async Task<ScheduledTaskPreviewUiModel?> PreviewAsync(ScheduledTaskScheduleUiModel schedule)
    {
        var result = await _service.PreviewAsync(schedule, _stopping.Token);
        if (!result.IsSuccess) Error?.Invoke(result.Error?.Message ?? "Schedule preview failed.");
        return result.Value;
    }
    /// <summary>Serializes command submission and then reloads the persisted read model.</summary>
    private async Task ExecuteAsync(Func<CancellationToken, ValueTask<UiOperationResult>> operation)
    {
        await _operation.WaitAsync(_stopping.Token);
        IsBusy = true; StateChanged?.Invoke();
        try { using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token); deadline.CancelAfter(TimeSpan.FromSeconds(15)); var result = await operation(deadline.Token); if (!result.IsSuccess) Error?.Invoke(result.Error?.Message ?? "Scheduled-task operation failed."); }
        finally { IsBusy = false; _operation.Release(); StateChanged?.Invoke(); }
        await RefreshAsync();
    }
    /// <summary>Cancels view work and stops its independently owned public listener.</summary>
    public async ValueTask DisposeAsync()
    {
        _stopping.Cancel();
        await _subscription.DisposeAsync();
        // An operation may still be returning from a transport; leave its gate valid until it exits.
    }
}
