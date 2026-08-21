using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Lifecycle;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.Operations;

/// <summary>
/// Owns the Strategy tab's bounded, newest-first stream of authoritative Futures ITI changes.
/// </summary>
public sealed class StrategyOperationsViewModel : ObservableObject, IAsyncLifecycle, IAsyncDisposable
{
    public const int EventCapacity = 500;
    static readonly IReadOnlyList<TimeFrameType> SupportedPeriods = Array.AsReadOnly(
        new[] { TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly });
    readonly object _stateGate = new();
    readonly StrategyOperationsModel _model;
    readonly string _contractId;
    readonly DateOnly _valueDate;
    readonly Guid _siteId = Guid.NewGuid();
    readonly AsyncLifecycleCoordinator _lifecycle;
    readonly List<FuturesItiSignalEventRow> _eventBuffer = [];
    readonly HashSet<string> _eventIdentities = new(StringComparer.Ordinal);
    IReadOnlyList<FuturesItiSignalEventRow> _events = [];
    TimeFrameType _selectedTimeFrame = TimeFrameType.Daily;
    bool _isListening;
    string _statusText = "Intrinsic Time Daily: Not started";
    PresentationError? _lastError;
    long _errorSequence;
    int _acceptEvents;

    public StrategyOperationsViewModel(IAppRoot appRoot, string contractId, DateOnly valueDate)
        : this(
            (appRoot ?? throw new ArgumentNullException(nameof(appRoot)))
                .GetModel<StrategyOperationsModel>(),
            contractId,
            valueDate)
    {
    }

    internal StrategyOperationsViewModel(
        StrategyOperationsModel model,
        string contractId,
        DateOnly valueDate)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        if (valueDate == default)
            throw new ArgumentException("A trading value date is required.", nameof(valueDate));

        _contractId = contractId;
        _valueDate = valueDate;
        _lifecycle = new AsyncLifecycleCoordinator(StartCoreAsync, StopCoreAsync);
    }

    public string ContractId => _contractId;
    public DateOnly ValueDate => _valueDate;

    public IReadOnlyList<TimeFrameType> TimeFrames => SupportedPeriods;

    public TimeFrameType SelectedTimeFrame
    {
        get => _selectedTimeFrame;
        set
        {
            if (!SupportedPeriods.Contains(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported ITI time frame.");
            if (!SetProperty(ref _selectedTimeFrame, value))
                return;

            PublishSelectedEvents();
            PublishStatus();
        }
    }

    /// <summary>Gets retained ITI changes for the selected time frame, newest first.</summary>
    public IReadOnlyList<FuturesItiSignalEventRow> Events
    {
        get => _events;
        private set => SetProperty(ref _events, value);
    }

    public bool IsListening
    {
        get => _isListening;
        private set => SetProperty(ref _isListening, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public PresentationError? LastError
    {
        get => _lastError;
        private set => SetProperty(ref _lastError, value);
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
        => _lifecycle.InitializeAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken)
        => _lifecycle.StopAsync(cancellationToken);

    public async ValueTask DisposeAsync()
        => await _lifecycle.DisposeAsync();

    async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Exchange(ref _acceptEvents, 1);
        try
        {
            await _model.StartFuturesItiSignalListenerAsync(_siteId, OnNotification);
            IsListening = true;
            PublishStatus();

            // Subscribe before loading snapshots so a change that occurs during startup
            // is merged rather than lost. Stable identities remove the overlap.
            foreach (var period in SupportedPeriods)
                await LoadInitialSnapshotAsync(period, cancellationToken);
        }
        catch
        {
            Interlocked.Exchange(ref _acceptEvents, 0);
            IsListening = false;
            StatusText = "Intrinsic Time: Listener unavailable";
            throw;
        }
    }

    async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Exchange(ref _acceptEvents, 0);
        try
        {
            await _model.StopFuturesItiSignalListenerAsync(_siteId);
        }
        finally
        {
            IsListening = false;
            PublishStatus();
        }
    }

    async Task LoadInitialSnapshotAsync(
        TimeFrameType period,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _model.GetLatestFuturesItiSignalAsync(
                    _contractId,
                    _valueDate,
                    period)
                .WaitAsync(cancellationToken);
            if (!result.Success)
            {
                PublishError(
                    result.ErrorCode,
                    result.ErrorMessage,
                    $"{period} ITI Snapshot Unavailable");
                return;
            }

            if (result.Value is { } signal)
                Add(FuturesItiSignalEventRow.FromInitialSnapshot(signal));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            PublishError(0, exception.Message, $"{period} ITI Snapshot Unavailable");
        }
    }

    void OnNotification(FuturesItiSignalUpdatedNotifyEvent notification)
    {
        if (Volatile.Read(ref _acceptEvents) == 0 || !notification.IsValid)
            return;

        Add(FuturesItiSignalEventRow.FromNotification(notification));
    }

    void Add(FuturesItiSignalEventRow row)
    {
        if (!IsRelevant(row))
            return;

        FuturesItiSignalEventRow[] published;
        lock (_stateGate)
        {
            if (!_eventIdentities.Add(row.StableIdentity))
                return;

            _eventBuffer.Add(row);
            _eventBuffer.Sort(static (left, right) =>
            {
                var time = right.OccurredOn.CompareTo(left.OccurredOn);
                if (time != 0)
                    return time;
                var sequence = right.SequenceId.CompareTo(left.SequenceId);
                return sequence != 0 ? sequence : right.EventId.CompareTo(left.EventId);
            });

            while (_eventBuffer.Count > EventCapacity)
            {
                var removed = _eventBuffer[^1];
                _eventBuffer.RemoveAt(_eventBuffer.Count - 1);
                _eventIdentities.Remove(removed.StableIdentity);
            }
            published = [.. _eventBuffer];
        }

        if (row.TimePeriod == SelectedTimeFrame)
            Events = published.Where(item => item.TimePeriod == SelectedTimeFrame).ToArray();
        PublishStatus();
    }

    void PublishSelectedEvents()
    {
        FuturesItiSignalEventRow[] selected;
        lock (_stateGate)
        {
            selected = _eventBuffer
                .Where(row => row.TimePeriod == SelectedTimeFrame)
                .ToArray();
        }

        Events = selected;
    }

    bool IsRelevant(FuturesItiSignalEventRow row)
        => string.Equals(row.ContractId, _contractId, StringComparison.Ordinal)
           && row.ValueDate == _valueDate
           && row.SequenceId > 0
           && row.OccurredOn != default
           && SupportedPeriods.Contains(row.TimePeriod);

    void PublishStatus()
        => StatusText = IsListening
            ? Events.Count == 0
                ? $"Intrinsic Time {SelectedTimeFrame}: Listening for {_contractId}"
                : $"Intrinsic Time {SelectedTimeFrame}: Live — {Events.Count} changes"
            : Events.Count == 0
                ? $"Intrinsic Time {SelectedTimeFrame}: Stopped"
                : $"Intrinsic Time {SelectedTimeFrame}: Stopped — {Events.Count} retained";

    void PublishError(int errorCode, string message, string caption)
        => LastError = new PresentationError(
            Interlocked.Increment(ref _errorSequence),
            errorCode,
            message,
            caption);
}
