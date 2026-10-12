using TomasAI.IFM.Domain.MarketData.Analytics.Persistence;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Logging;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.EventProjector;

public sealed class FuturesItiSignalEventProjector(
    IDbContextFactory dbFactory, IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource, IBlackboardService blackboardService,
    ILogger<FuturesItiSignalEventProjector> logger,
    EventProjectorReliabilityOptions? reliabilityOptions = null,
    FuturesItiSignalRuntimeTelemetry? telemetry = null,
    IPostgresEventTransaction? transactions = null)
    : ConventionalEventProjector<FuturesItiSignalCommandActor>(durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<FuturesItiSignalGeneratedEvent, FuturesItiSignalGeneratedCompleteEvent, FuturesItiSignalGeneratedFailEvent, FuturesItiSignalEntityId>(
            async (e, context) =>
            {
                var db = dbFactory.MarketDataDb;
                var signal = e.FuturesItiSignal
                    ?? throw new InvalidOperationException("FuturesItiSignal payload is required.");
                FuturesItiSignalEventLogging.ProjectionStarted(
                    logger, e.Id, signal.ContractId, signal.ValueDate, signal.TimePeriod);
                signal = signal with
                {
                    SequenceId = signal.SequenceId > 0 ? signal.SequenceId : context.EventId
                };
                if (!string.Equals(db.ProviderName,"System.Data.Postgres",StringComparison.Ordinal)) await db.InsertFuturesItiSignalAsync(signal).ConfigureAwait(false);
                RegimeDiscoverySignalCacheAdapter.Publish(signal, signal.SequenceId,
                    e.CreatedOn == default ? e.ReceivedOn : e.CreatedOn, (decimal)e.VixFuturesPrice);
                telemetry?.RecordProjectionCompleted();
                FuturesItiSignalEventLogging.ProjectionCompleted(
                    logger, e.Id, signal.ContractId, signal.ValueDate, signal.TimePeriod, signal.SequenceId);
            })
            .WithReadModelRebuild((source,token) => AcceptedSignalRebuild.MaterializeAsync(dbFactory,transactions,source,token)),
        Describe<FuturesItiSignalHoldTradeSetEvent, FuturesItiSignalHoldTradeSetCompleteEvent,
            FuturesItiSignalHoldTradeSetFailEvent, FuturesItiSignalEntityId>(
            async (e, context) =>
            {
                var signal = e.FuturesItiSignal
                    ?? throw new InvalidOperationException("Set hold-trade signal payload is required.");
                if (!string.Equals(dbFactory.MarketDataDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal)) await dbFactory.MarketDataDb.InsertFuturesItiSignalAsync(signal with
                {
                    SequenceId = signal.SequenceId > 0 ? signal.SequenceId : context.EventId
                }).ConfigureAwait(false);
            })
            .WithReadModelRebuild((source,token) => AcceptedSignalRebuild.MaterializeAsync(dbFactory,transactions,source,token)),
        Describe<FuturesItiSignalHoldTradeClearedEvent, FuturesItiSignalHoldTradeClearedCompleteEvent,
            FuturesItiSignalHoldTradeClearedFailEvent, FuturesItiSignalEntityId>(
            async (e, context) =>
            {
                var signal = e.FuturesItiSignal
                    ?? throw new InvalidOperationException("Clear hold-trade signal payload is required.");
                if (!string.Equals(dbFactory.MarketDataDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal)) await dbFactory.MarketDataDb.InsertFuturesItiSignalAsync(signal with
                {
                    SequenceId = signal.SequenceId > 0 ? signal.SequenceId : context.EventId
                }).ConfigureAwait(false);
            })
            .WithReadModelRebuild((source,token) => AcceptedSignalRebuild.MaterializeAsync(dbFactory,transactions,source,token))
    ];

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
}
