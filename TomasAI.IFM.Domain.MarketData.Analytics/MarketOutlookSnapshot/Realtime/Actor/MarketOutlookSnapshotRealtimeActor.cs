using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Actor;

/// <summary>
/// Bridges Market Outlook realtime inputs to its command aggregate and publishes projected snapshot notifications.
/// </summary>
/// <remarks>
/// PostgreSQL command events and ScyllaDB own durable state. This actor retains only bounded latest-value
/// preview ordering and snapshot overlays; restart safely reconstructs them from the next epoch and projection.
/// </remarks>
public class MarketOutlookSnapshotRealtimeActor(
    IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> actorContext)
    : BaseEventActor<MarketOutlookSnapshotRealtimeActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the stable actor mailbox name retained for wire compatibility.</summary>
    public const string ActorName = "MarketOutlook";

    static readonly ActorTypeId MarketPriceRoute = new(
        ActorType.Realtime,
        FuturesMarketPriceUpdatedRealtimeEvent.Actor,
        FuturesMarketPriceUpdatedRealtimeEvent.Verb);
    readonly Dictionary<string, (Guid Epoch, long Ordinal, DateTimeOffset EventTime)> previewPositions =
        new(StringComparer.Ordinal);
    readonly Dictionary<string, Guid> invalidPreviewEpochs = new(StringComparer.Ordinal);
    readonly Dictionary<MarketOutlookEntityId, MarketOutlookSnapshotReadModel> snapshots = [];

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [MarketOutlookComponentChangedRealtimeEvent.Verb] =
                message => message.AsEvent<MarketOutlookComponentChangedRealtimeEvent>()!,
            [FuturesMarketPriceUpdatedRealtimeEvent.Verb] =
                message => message.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>()!,
            [MarketOutlookEodUpdatedRealtimeEvent.Verb] =
                message => message.AsEvent<MarketOutlookEodUpdatedRealtimeEvent>()!,
            [MarketOutlookComponentObservedCompleteEvent.Verb] =
                message => message.AsEvent<MarketOutlookComponentObservedCompleteEvent>()!,
            [MarketOutlookComponentObservedFailEvent.Verb] =
                message => message.AsEvent<MarketOutlookComponentObservedFailEvent>()!,
            [MarketOutlookSnapshotPublishedCompleteEvent.Verb] =
                message => message.AsEvent<MarketOutlookSnapshotPublishedCompleteEvent>()!,
            [MarketOutlookSnapshotPublishedFailEvent.Verb] =
                message => message.AsEvent<MarketOutlookSnapshotPublishedFailEvent>()!
        };

    /// <inheritdoc />
    protected override ValueTask OnStartup(
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        context.AddRealtimeRouter(MarketPriceRoute, Id);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override ValueTask OnShutdown(
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        context.RemoveRealtimeRouter(MarketPriceRoute, Id);
        previewPositions.Clear();
        invalidPreviewEpochs.Clear();
        snapshots.Clear();
        return ValueTask.CompletedTask;
    }

    readonly IReadOnlyDictionary<Type, Func<IEvent, IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor>, ValueTask>>
        _receiveMap = new Dictionary<Type, Func<IEvent, IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor>, ValueTask>>
        {
            [typeof(MarketOutlookComponentChangedRealtimeEvent)] = (@event, context) =>
                ((MarketOutlookComponentChangedRealtimeEvent)@event).ObserveAsync(context),
            [typeof(MarketOutlookEodUpdatedRealtimeEvent)] = (@event, context) =>
                ((MarketOutlookEodUpdatedRealtimeEvent)@event).PublishAsync(context),
            [typeof(MarketOutlookComponentObservedCompleteEvent)] = (@event, context) =>
                ((MarketOutlookComponentObservedCompleteEvent)@event).CompleteAsync(context),
            [typeof(MarketOutlookComponentObservedFailEvent)] = (@event, context) =>
                ((MarketOutlookComponentObservedFailEvent)@event).FailAsync(context),
            [typeof(MarketOutlookSnapshotPublishedCompleteEvent)] = (@event, context) =>
                ((MarketOutlookSnapshotPublishedCompleteEvent)@event).CompleteAsync(context),
            [typeof(MarketOutlookSnapshotPublishedFailEvent)] = (@event, context) =>
                ((MarketOutlookSnapshotPublishedFailEvent)@event).FailAsync(context)
        };

    /// <inheritdoc />
    protected override IEvent ParseMessage(
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context,
        IActorMessage message)
        => ParseMappedRealtimeEvent(context, message, _parseMap);

    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context,
        IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(context);
        var realtimeContext = (IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor>)context;
        if (@event is FuturesMarketPriceUpdatedRealtimeEvent marketPrice)
        {
            await PublishDailyPreviewAsync(marketPrice, realtimeContext).ConfigureAwait(false);
            return;
        }
        if (@event is MarketOutlookComponentObservedCompleteEvent observed
            && observed.WorkingState.PublishedSnapshot is { IsValid: true } observedSnapshot)
            snapshots[observed.EntityId] = observedSnapshot;
        else if (@event is MarketOutlookSnapshotPublishedCompleteEvent published)
            snapshots[published.EntityId] = published.MarketOutlook;
        var handler = ResolveMappedEventHandler(@event, _receiveMap);
        await handler(@event, realtimeContext)
            .ConfigureAwait(false);
    }

    async ValueTask PublishDailyPreviewAsync(
        FuturesMarketPriceUpdatedRealtimeEvent source,
        IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        if (!TryAcceptPreviewTrade(source)
            || !MarketOutlookDailyPreviewCalculator.TryCalculate(source, out var ema, out var bb))
            return;
        var entityId = new MarketOutlookEntityId(source.Price.ContractId, source.Price.ValueDate);
        if (!snapshots.TryGetValue(entityId, out var current))
        {
            current = await ((IMarketOutlookSnapshotRealtimeContext)context).DbFactory.MarketDataDb
                .GetMarketOutlookSnapshotAsync(entityId.ContractId, entityId.ValueDate)
                .ConfigureAwait(false)
                ?? new MarketOutlookSnapshotReadModel
                {
                    ContractId = entityId.ContractId,
                    ValueDate = entityId.ValueDate
                };
        }
        var preview = current with
        {
            Revision = checked(Math.Max(0, current.Revision) + 1),
            UpdatedOn = DateTime.UtcNow,
            FuturesEmaSignal = ema,
            FuturesBbSignal = bb
        };
        snapshots[entityId] = preview;
        await context.SendAsync<MarketOutlookUpdatedNotifyEvent, MarketOutlookEntityId>(new()
        {
            Subject = new(
                ActorType.Notify,
                MarketOutlookUpdatedNotifyEvent.Actor,
                MarketOutlookUpdatedNotifyEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            CommandId = source.CommandId == Guid.Empty ? source.Id : source.CommandId,
            AggregateId = source.AggregateId,
            EventSource = "MarketOutlookDailyLivePreview",
            ReceivedOn = DateTime.UtcNow,
            MarketOutlook = preview
        }).ConfigureAwait(false);
    }

    bool TryAcceptPreviewTrade(FuturesMarketPriceUpdatedRealtimeEvent source)
    {
        if (source.UpdateSource != FuturesMarketPriceUpdateSource.Trade
            || source.Price.Trade is not { } trade
            || !source.Price.ContractId.StartsWith("ES", StringComparison.OrdinalIgnoreCase)
            || trade.NormalizedTradeAction != NormalizedTradeAction.New
            || trade.LastPrice <= 0m
            || trade.LastSize == 0
            || trade.StreamEpochId == Guid.Empty
            || trade.TradeOrdinal <= 0)
            return false;
        var contractId = source.Price.ContractId;
        if (invalidPreviewEpochs.TryGetValue(contractId, out var invalidEpoch))
        {
            if (invalidEpoch == trade.StreamEpochId)
                return false;
            invalidPreviewEpochs.Remove(contractId);
        }
        if (previewPositions.TryGetValue(contractId, out var current)
            && current.Epoch == trade.StreamEpochId)
        {
            if (trade.TradeOrdinal <= current.Ordinal
                || trade.EventTimestamp < current.EventTime)
                return false;
            if (trade.TradeOrdinal != current.Ordinal + 1)
            {
                invalidPreviewEpochs[contractId] = trade.StreamEpochId;
                previewPositions[contractId] = (
                    trade.StreamEpochId,
                    trade.TradeOrdinal,
                    trade.EventTimestamp);
                return false;
            }
        }
        previewPositions[contractId] = (
            trade.StreamEpochId,
            trade.TradeOrdinal,
            trade.EventTimestamp);
        return true;
    }

    /// <inheritdoc />
    protected override ValueTask OnExceptionAsync(
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception)
    {
        actorContext.Logger.LogErrorEvent(
            ActorName,
            exception,
            "Market Outlook realtime bridge failed for {EntityId}",
            @event.Subject.EntityId);
        return ValueTask.CompletedTask;
    }
}
