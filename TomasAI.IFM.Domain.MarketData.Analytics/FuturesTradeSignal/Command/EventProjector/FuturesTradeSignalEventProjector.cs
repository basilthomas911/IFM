using TomasAI.IFM.Domain.MarketData.Analytics.Persistence;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.EventProjector;

public sealed class FuturesTradeSignalEventProjector(
    IDbContextFactory dbFactory,
    IDurableReplayQueue durableReplayQueue, IEventSourceActorDbContext dbEventSource,
    IBlackboardService blackboardService, ILogger<FuturesTradeSignalEventProjector> logger,
    EventProjectorReliabilityOptions? reliabilityOptions = null,
    IPostgresEventTransaction? transactions = null)
    : ConventionalEventProjector<FuturesTradeSignalCommandActor>(durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<FuturesTradeSignalUpdatedEvent, FuturesTradeSignalUpdatedCompleteEvent, FuturesTradeSignalUpdatedFailEvent, FuturesTradeSignalEntityId>(
            async (e, context) =>
            {
                var tradeSignal = e.FuturesTradeSignal
                    ?? throw new InvalidOperationException("FuturesTradeSignal payload is required.");
                if (!string.Equals(dbFactory.MarketDataDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal)) await dbFactory.MarketDataDb.InsertFuturesTradeSignalAsync(
                    tradeSignal with { SequenceId = context.EventId }).ConfigureAwait(false);
            })
            .WithReadModelRebuild((source,token) => AcceptedSignalRebuild.MaterializeAsync(dbFactory,transactions,source,token)),
        DescribeNotification<FuturesItiSignalHoldTradeChangedEvent, FuturesItiSignalEntityId>()
    ];

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
}
