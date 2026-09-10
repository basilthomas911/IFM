using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Lifecycle;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.Operations;

/// <summary>Owns Strategy workflow presentation and the ITI observations used by its unchanged chart.</summary>
public sealed class StrategyOperationsViewModel : ObservableObject, IAsyncLifecycle, IAsyncDisposable
{
    internal static readonly TimeSpan DefaultReconciliationInterval = TimeSpan.FromSeconds(30);
    const int MaximumWorkflowRows = 500;
    const int WorkflowHistoryPageSize = 500;
    static readonly IReadOnlyList<TimeFrameType> SupportedPeriods = Array.AsReadOnly(
        new[] { TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly });
    readonly object _stateGate = new();
    readonly StrategyOperationsService _model;
    readonly string _contractId;
    readonly DateOnly _valueDate;
    readonly Guid _siteId = Guid.NewGuid();
    readonly AsyncLifecycleCoordinator _lifecycle;
    readonly TimeProvider _timeProvider;
    readonly TimeSpan _reconciliationInterval;
    readonly List<FuturesItiSignalEventRow> _eventBuffer = [];
    readonly HashSet<string> _eventIdentities = new(StringComparer.Ordinal);
    readonly Dictionary<StrategyWorkflowId, IntrinsicTimeStrategyWorkflowView> _workflowViews = [];
    IReadOnlyList<FuturesItiSignalEventRow> _events = [];
    IReadOnlyList<StrategyWorkflowRow> _workflows = [];
    StrategyWorkflowId? _selectedWorkflowId;
    string _selectedWorkflowDetails = "Select a strategy workflow to inspect its pipeline results.";
    TimeFrameType _selectedTimeFrame = TimeFrameType.Daily;
    bool _isListening;
    string _statusText = "Intrinsic Time Daily: Not started";
    PresentationError? _lastError;
    long _errorSequence;
    int _acceptEvents;

    public StrategyOperationsViewModel(IAppRoot appRoot, string contractId, DateOnly valueDate)
        : this(
            (appRoot ?? throw new ArgumentNullException(nameof(appRoot))).Services.StrategyOperations,
            contractId,
            valueDate,
            TimeProvider.System)
    {
    }

    internal StrategyOperationsViewModel(
        StrategyOperationsService model,
        string contractId,
        DateOnly valueDate,
        TimeProvider? timeProvider = null,
        TimeSpan? reconciliationInterval = null)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        if (valueDate == default)
            throw new ArgumentException("A trading value date is required.", nameof(valueDate));

        var resolvedInterval = reconciliationInterval ?? DefaultReconciliationInterval;
        if (resolvedInterval <= TimeSpan.Zero || resolvedInterval == Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(reconciliationInterval));

        _contractId = contractId;
        _valueDate = valueDate;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _reconciliationInterval = resolvedInterval;
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
            PublishSelectedWorkflows();
            PublishStatus();
        }
    }

    /// <summary>Gets the ITI observations used exclusively by the existing chart.</summary>
    public IReadOnlyList<FuturesItiSignalEventRow> Events
    {
        get => _events;
        private set => SetProperty(ref _events, value);
    }

    /// <summary>Gets accepted Strategy workflows for the selected timeframe, newest first.</summary>
    public IReadOnlyList<StrategyWorkflowRow> Workflows
    {
        get => _workflows;
        private set => SetProperty(ref _workflows, value);
    }

    public StrategyWorkflowId? SelectedWorkflowId
    {
        get => _selectedWorkflowId;
        private set => SetProperty(ref _selectedWorkflowId, value);
    }

    public string SelectedWorkflowDetails
    {
        get => _selectedWorkflowDetails;
        private set => SetProperty(ref _selectedWorkflowDetails, value);
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

    public void SelectWorkflow(StrategyWorkflowId? workflowId)
    {
        if (workflowId is not { } selected)
        {
            SelectedWorkflowId = null;
            SelectedWorkflowDetails = "Select a strategy workflow to inspect its pipeline results.";
            return;
        }

        IntrinsicTimeStrategyWorkflowView? view;
        lock (_stateGate)
            _workflowViews.TryGetValue(selected, out view);
        if (view is null)
            return;

        SelectedWorkflowId = selected;
        SelectedWorkflowDetails = StrategyWorkflowPresentation.RenderDetails(view);
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
        var workflowStarted = false;
        var signalStarted = false;
        try
        {
            await _model.StartWorkflowListenerAsync(_siteId, OnWorkflowNotification);
            workflowStarted = true;
            await _model.StartFuturesItiSignalListenerAsync(_siteId, OnSignalNotification);
            signalStarted = true;
            IsListening = true;
            PublishStatus();

            // Both subscriptions precede history so startup updates win by stable identity and workflow revision.
            foreach (var period in SupportedPeriods)
                await LoadInitialSignalHistoryAsync(period, cancellationToken);
            foreach (var period in SupportedPeriods)
                await ReconcileWorkflowPeriodAsync(period, cancellationToken);

            _ = _lifecycle.RunAsync(ReconcileLoopAsync);
        }
        catch
        {
            Interlocked.Exchange(ref _acceptEvents, 0);
            if (signalStarted)
                await _model.StopFuturesItiSignalListenerAsync(_siteId);
            if (workflowStarted)
                await _model.StopWorkflowListenerAsync(_siteId);
            IsListening = false;
            StatusText = "Intrinsic Time: Listener unavailable";
            throw;
        }
    }

    async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Exchange(ref _acceptEvents, 0);
        Exception? failure = null;
        try
        {
            await _model.StopFuturesItiSignalListenerAsync(_siteId);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        try
        {
            await _model.StopWorkflowListenerAsync(_siteId);
        }
        catch (Exception exception)
        {
            failure = failure is null ? exception : new AggregateException(failure, exception);
        }
        finally
        {
            IsListening = false;
            PublishStatus();
        }

        if (failure is not null)
            throw failure;
    }

    async Task LoadInitialSignalHistoryAsync(TimeFrameType period, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _model.GetFuturesItiSignalHistoryAsync(
                _contractId, _valueDate, period, cancellationToken);
            if (!result.IsSuccess)
            {
                PublishError(result.Error!.Code, result.Error.Message, $"{period} ITI History Unavailable");
                return;
            }

            AddSignalRange((result.Value ?? []).Select(FuturesItiSignalEventRow.FromHistory));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            PublishError(0, exception.Message, $"{period} ITI History Unavailable");
        }
    }

    async Task ReconcileLoopAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            await Task.Delay(_reconciliationInterval, _timeProvider, cancellationToken);
            foreach (var period in SupportedPeriods)
            {
                await ReconcileSignalPeriodAsync(period, cancellationToken);
                await ReconcileWorkflowPeriodAsync(period, cancellationToken);
            }
        }
    }

    async Task ReconcileSignalPeriodAsync(TimeFrameType period, CancellationToken cancellationToken)
    {
        try
        {
            var history = await _model.GetFuturesItiSignalHistoryAsync(
                _contractId, _valueDate, period, cancellationToken);
            if (!history.IsSuccess)
            {
                PublishError(history.Error!.Code, history.Error.Message, $"{period} ITI Reconciliation Unavailable");
                return;
            }

            AddSignalRange((history.Value ?? []).Select(FuturesItiSignalEventRow.FromHistory));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            PublishError(0, exception.Message, $"{period} ITI Reconciliation Unavailable");
        }
    }

    async Task ReconcileWorkflowPeriodAsync(TimeFrameType period, CancellationToken cancellationToken)
    {
        try
        {
            DateOnly[] entityDates;
            lock (_stateGate)
            {
                entityDates = _eventBuffer
                    .Where(row => row.TimePeriod == period)
                    .Select(row => row.TimeFrameStartValueDate)
                    .Append(FuturesItiSignalHistoryWindow.Resolve(_valueDate, period).StartValueDate)
                    .Distinct()
                    .ToArray();
            }

            foreach (var entityDate in entityDates)
            {
                var entity = IntrinsicTimeStrategyWorkflowEntityId.Create(
                    new FuturesItiSignalEntityId(_contractId, entityDate, period));
                var result = await _model.GetRecentWorkflowsAsync(
                    entity,
                    DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc),
                    WorkflowHistoryPageSize,
                    cancellationToken);
                if (!result.IsSuccess)
                {
                    PublishError(result.Error!.Code, result.Error.Message,
                        $"{period} Strategy Workflow History Unavailable");
                    continue;
                }

                AddWorkflowRange(result.Value ?? []);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            PublishError(0, exception.Message, $"{period} Strategy Workflow Reconciliation Unavailable");
        }
    }

    void OnSignalNotification(FuturesItiSignalUpdatedNotifyEvent notification)
    {
        if (Volatile.Read(ref _acceptEvents) != 0)
            AddSignalRange([FuturesItiSignalEventRow.FromNotification(notification)]);
    }

    void OnWorkflowNotification(IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent notification)
    {
        if (Volatile.Read(ref _acceptEvents) != 0)
            AddWorkflowRange([notification.State]);
    }

    void AddSignalRange(IEnumerable<FuturesItiSignalEventRow> rows)
    {
        var accepted = rows.Where(IsRelevantSignal).ToArray();
        if (accepted.Length == 0)
            return;

        lock (_stateGate)
        {
            foreach (var row in accepted)
            {
                if (_eventIdentities.Add(row.StableIdentity))
                    _eventBuffer.Add(row);
            }
            _eventBuffer.Sort(static (left, right) =>
            {
                var time = right.OccurredOn.CompareTo(left.OccurredOn);
                if (time != 0)
                    return time;
                var sequence = right.SequenceId.CompareTo(left.SequenceId);
                return sequence != 0 ? sequence : right.EventId.CompareTo(left.EventId);
            });
        }

        PublishSelectedEvents();
        PublishStatus();
    }

    void AddWorkflowRange(IEnumerable<IntrinsicTimeStrategyWorkflowView> views)
    {
        var accepted = views.Where(IsRelevantWorkflow).ToArray();
        if (accepted.Length == 0)
            return;

        StrategyWorkflowId? conflictingWorkflowId = null;
        long conflictingRevision = 0;
        lock (_stateGate)
        {
            foreach (var view in accepted)
            {
                if (_workflowViews.TryGetValue(view.WorkflowId, out var current))
                {
                    if (current.WorkflowRevision > view.WorkflowRevision)
                        continue;
                    if (current.WorkflowRevision == view.WorkflowRevision)
                    {
                        if (!EquivalentState(current, view))
                        {
                            conflictingWorkflowId = view.WorkflowId;
                            conflictingRevision = view.WorkflowRevision;
                        }
                        continue;
                    }
                }
                _workflowViews[view.WorkflowId] = view;
            }

            if (_workflowViews.Count > MaximumWorkflowRows)
            {
                foreach (var workflowId in _workflowViews.Values
                             .OrderByDescending(WorkflowTime)
                             .ThenByDescending(view => view.WorkflowId.Value)
                             .Skip(MaximumWorkflowRows)
                             .Select(view => view.WorkflowId)
                             .ToArray())
                    _workflowViews.Remove(workflowId);
            }
        }

        if (conflictingWorkflowId is { } conflict)
            PublishError(
                409,
                $"Workflow {conflict} revision {conflictingRevision} arrived with conflicting state. The retained authoritative view was not replaced.",
                "Strategy Workflow Revision Conflict");

        PublishSelectedWorkflows();
        PublishStatus();
    }

    void PublishSelectedEvents()
    {
        FuturesItiSignalEventRow[] selected;
        lock (_stateGate)
            selected = _eventBuffer.Where(row => row.TimePeriod == SelectedTimeFrame).ToArray();
        Events = selected;
    }

    void PublishSelectedWorkflows()
    {
        StrategyWorkflowRow[] selected;
        IntrinsicTimeStrategyWorkflowView? selectedView = null;
        lock (_stateGate)
        {
            selected = _workflowViews.Values
                .Where(view => view.EntityId.ItiSignalEntityId.TimePeriod == SelectedTimeFrame)
                .OrderByDescending(WorkflowTime)
                .ThenByDescending(view => view.WorkflowId.Value)
                .Select(StrategyWorkflowPresentation.CreateRow)
                .ToArray();
            if (SelectedWorkflowId is { } selectedId)
                _workflowViews.TryGetValue(selectedId, out selectedView);
        }

        Workflows = selected;
        if (selectedView is not null
            && selectedView.EntityId.ItiSignalEntityId.TimePeriod == SelectedTimeFrame)
            SelectedWorkflowDetails = StrategyWorkflowPresentation.RenderDetails(selectedView);
        else if (SelectedWorkflowId is not null)
        {
            SelectedWorkflowId = null;
            SelectedWorkflowDetails = "Select a strategy workflow to inspect its pipeline results.";
        }
    }

    bool IsRelevantSignal(FuturesItiSignalEventRow row)
    {
        if (!string.Equals(row.ContractId, _contractId, StringComparison.Ordinal)
            || !SupportedPeriods.Contains(row.TimePeriod))
            return false;

        var window = FuturesItiSignalHistoryWindow.Resolve(_valueDate, row.TimePeriod);
        return row.ValueDate >= window.StartValueDate && row.ValueDate <= window.EndValueDate;
    }

    bool IsRelevantWorkflow(IntrinsicTimeStrategyWorkflowView view)
    {
        var iti = view.EntityId.ItiSignalEntityId;
        if (!string.Equals(iti.ContractId, _contractId, StringComparison.Ordinal)
            || !SupportedPeriods.Contains(iti.TimePeriod))
            return false;
        var window = FuturesItiSignalHistoryWindow.Resolve(_valueDate, iti.TimePeriod);
        return iti.TimeFrameStartValueDate >= window.StartValueDate
               && iti.TimeFrameStartValueDate <= window.EndValueDate;
    }

    void PublishStatus()
        => StatusText = IsListening
            ? Events.Count == 0 && Workflows.Count == 0
                ? $"Intrinsic Time {SelectedTimeFrame}: Listening for {_contractId}"
                : $"Intrinsic Time {SelectedTimeFrame}: Live — {Workflows.Count} workflows"
            : Events.Count == 0 && Workflows.Count == 0
                ? $"Intrinsic Time {SelectedTimeFrame}: Stopped"
                : $"Intrinsic Time {SelectedTimeFrame}: Stopped — {Workflows.Count} workflows retained";

    void PublishError(int errorCode, string message, string caption)
        => LastError = new PresentationError(
            Interlocked.Increment(ref _errorSequence), errorCode, message, caption);

    static DateTime WorkflowTime(IntrinsicTimeStrategyWorkflowView view)
        => view.TriggerEvent.CreatedOn == default ? view.StartedAtUtc : view.TriggerEvent.CreatedOn;

    static bool EquivalentState(
        IntrinsicTimeStrategyWorkflowView left,
        IntrinsicTimeStrategyWorkflowView right)
        => ReferenceEquals(left, right)
           || MessagePackSerializer.Serialize(left).AsSpan()
               .SequenceEqual(MessagePackSerializer.Serialize(right));
}
