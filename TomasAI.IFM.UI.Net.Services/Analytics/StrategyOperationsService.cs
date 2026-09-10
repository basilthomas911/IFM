using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Services.Operations;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ServiceApi;

namespace TomasAI.IFM.UI.Net.Services.Analytics;

/// <summary>
/// Provides the framework-neutral query and notification boundary used by the Strategy Operations view.
/// </summary>
public sealed class StrategyOperationsService(
    IMarketDataAnalyticsQueryApi queryApi,
    IFuturesItiSignalUIEventConsumer eventConsumer,
    IIntrinsicTimeStrategyWorkflowQueryApi workflowQueryApi,
    IIntrinsicTimeStrategyWorkflowUIEventConsumer workflowEventConsumer)
    : UiServiceBase<StrategyOperationsService>
{
    const int MaximumTerminalCacheEntries = 500;
    readonly object _terminalCacheGate = new();
    readonly Dictionary<StrategyWorkflowId, IntrinsicTimeStrategyWorkflowView> _terminalCache = [];
    readonly IMarketDataAnalyticsQueryApi _queryApi = queryApi
        ?? throw new ArgumentNullException(nameof(queryApi));
    readonly IFuturesItiSignalUIEventConsumer _eventConsumer = eventConsumer
        ?? throw new ArgumentNullException(nameof(eventConsumer));
    readonly IIntrinsicTimeStrategyWorkflowQueryApi _workflowQueryApi = workflowQueryApi
        ?? throw new ArgumentNullException(nameof(workflowQueryApi));
    readonly IIntrinsicTimeStrategyWorkflowUIEventConsumer _workflowEventConsumer = workflowEventConsumer
        ?? throw new ArgumentNullException(nameof(workflowEventConsumer));

    /// <summary>Gets the latest authoritative ITI signal for one display timeframe.</summary>
    public async ValueTask<UiOperationResult<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await _queryApi.GetFuturesItiSignalAsync(contractId, valueDate, timePeriod);
        cancellationToken.ThrowIfCancellationRequested();
        return result.ToUiResult(value => value);
    }

    /// <summary>Gets every durable ITI signal in the requested display timeframe.</summary>
    public async ValueTask<UiOperationResult<FuturesItiSignalV2ReadModel[]>> GetFuturesItiSignalHistoryAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await _queryApi.GetFuturesItiSignalHistoryAsync(contractId, valueDate, timePeriod);
        return result.ToUiResult(value => value);
    }

    /// <summary>Starts the typed ITI notification listener for one UI site.</summary>
    public ValueTask StartFuturesItiSignalListenerAsync(
        Guid siteId,
        Action<FuturesItiSignalUpdatedNotifyEvent> eventAction)
        => _eventConsumer.StartAsync(siteId, eventAction);

    /// <summary>Stops the typed ITI notification listener for one UI site.</summary>
    public ValueTask StopFuturesItiSignalListenerAsync(Guid siteId)
        => _eventConsumer.StopAsync(siteId);

    /// <summary>Gets recent workflow identities for one Intrinsic Time entity.</summary>
    public async ValueTask<UiOperationResult<IntrinsicTimeStrategyWorkflowView[]>> GetRecentWorkflowsAsync(
        IntrinsicTimeStrategyWorkflowEntityId workflowEntity,
        DateTime beforeUtc,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var history = await _workflowQueryApi.GetRecentAsync(workflowEntity.Format(), beforeUtc, pageSize);
        cancellationToken.ThrowIfCancellationRequested();
        if (!history.Success || history.Value is null)
            return UiOperationResult<IntrinsicTimeStrategyWorkflowView[]>.Failure(
                history.ErrorCode,
                history.ErrorMessage);

        using var hydrationGate = new SemaphoreSlim(8, 8);
        var hydrated = await Task.WhenAll(history.Value.Select(async item =>
        {
            if (TryGetTerminal(item.WorkflowId, item.WorkflowRevision, out var cached))
                return cached;

            await hydrationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var detail = await _workflowQueryApi.GetByIdAsync(item.WorkflowId, item.WorkflowRevision)
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (!detail.Success || detail.Value is null)
                    throw new UiOperationException(new UiOperationError(detail.ErrorCode, detail.ErrorMessage));
                var view = MessagePackSerializer.Deserialize<IntrinsicTimeStrategyWorkflowView>(
                    detail.Value.StatePayload);
                CacheTerminal(view);
                return view;
            }
            finally
            {
                hydrationGate.Release();
            }
        })).ConfigureAwait(false);

        return UiOperationResult<IntrinsicTimeStrategyWorkflowView[]>.Success(hydrated);
    }

    bool TryGetTerminal(
        StrategyWorkflowId workflowId,
        long minimumRevision,
        out IntrinsicTimeStrategyWorkflowView view)
    {
        lock (_terminalCacheGate)
            return _terminalCache.TryGetValue(workflowId, out view!)
                   && view.WorkflowRevision >= minimumRevision;
    }

    void CacheTerminal(IntrinsicTimeStrategyWorkflowView view)
    {
        if (view.Status is WorkflowStrategyMachineStatus.Empty or WorkflowStrategyMachineStatus.Started)
            return;

        lock (_terminalCacheGate)
        {
            if (!_terminalCache.TryGetValue(view.WorkflowId, out var current)
                || current.WorkflowRevision < view.WorkflowRevision)
                _terminalCache[view.WorkflowId] = view;

            if (_terminalCache.Count <= MaximumTerminalCacheEntries)
                return;
            foreach (var workflowId in _terminalCache.Values
                         .OrderByDescending(item => item.UpdatedAtUtc)
                         .ThenByDescending(item => item.WorkflowId.Value)
                         .Skip(MaximumTerminalCacheEntries)
                         .Select(item => item.WorkflowId)
                         .ToArray())
                _terminalCache.Remove(workflowId);
        }
    }

    /// <summary>Starts projected Strategy Workflow notifications for one UI site.</summary>
    public ValueTask StartWorkflowListenerAsync(
        Guid siteId,
        Action<IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent> eventAction)
        => _workflowEventConsumer.StartAsync(siteId, eventAction);

    /// <summary>Stops projected Strategy Workflow notifications for one UI site.</summary>
    public ValueTask StopWorkflowListenerAsync(Guid siteId)
        => _workflowEventConsumer.StopAsync(siteId);
}
