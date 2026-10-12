using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.EventProjector;

public sealed class FuturesOptionTickDataEventProjector(
    ICommandActorContext<FuturesOptionTickDataCommandActor> actorContext,
    ILogger<FuturesOptionTickDataEventProjector> logger, EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesOptionTickDataCommandActor>(actorContext.DurableReplayQueue, actorContext.DbEventSource, actorContext.BlackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        DescribeNotification<FuturesOptionTickDataStreamingStartedEvent, FuturesOptionTickEntityId>(useDurableReplay: false),
        DescribeNotification<FuturesOptionTickDataStreamingStoppedEvent, FuturesOptionTickEntityId>(useDurableReplay: false),
        Describe<FuturesOptionTickDataInsertedEvent, FuturesOptionTickDataInsertedCompleteEvent, FuturesOptionTickDataInsertedFailEvent, FuturesOptionTickEntityId>(
            async (e, context) =>
            {
                var tick = e.TickData with
                {
                    TickId = e.TickData.TickId > 0 ? e.TickData.TickId : context.EventId
                };
                await (actorContext.DbFactory.MarketDataDb.InsertFuturesOptionTickDataAsync(tick)).ConfigureAwait(false);
                // Enrichment is domain projection work; persistence receives the accepted quote facts.
                if (tick.Vega == 0 && tick.UnderlyingPrice > 0)
                {
                    var definition = await actorContext.DbFactory.SecuritiesDb.GetFuturesOptionContractAsync(tick.ContractId)
                        .ConfigureAwait(false);
                    if (definition is not null)
                        tick = TomasAI.IFM.Domain.MarketData.Feed.Pricing.OptionQuoteGreekEnrichment.Calculate(definition, tick);
                }
                await (actorContext.DbFactory.MarketDataDb.UpsertFuturesOptionChainQuoteDataAsync(
                    e.Contract.ContractId, tick)).ConfigureAwait(false);
            })
    ];
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
}
