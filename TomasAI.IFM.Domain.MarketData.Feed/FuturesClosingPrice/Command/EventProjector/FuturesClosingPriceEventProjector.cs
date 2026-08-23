using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.EventProjector;

public sealed class FuturesClosingPriceEventProjector(
    ICommandActorContext<FuturesClosingPriceCommandActor> actorContext,
    ILogger<FuturesClosingPriceEventProjector> logger, EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesClosingPriceCommandActor>(actorContext.DurableReplayQueue, actorContext.DbEventSource, actorContext.BlackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<FuturesClosingPriceInsertedEvent, FuturesClosingPriceInsertedCompleteEvent, FuturesClosingPriceInsertedFailEvent, FuturesDataId>(
            e => actorContext.DbFactory.MarketDataDb.InsertFuturesClosingPriceAsync(new FuturesClosingPriceReadModel(
                e.FuturesClosingPriceId.ContractId, e.FuturesClosingPriceId.ValueDate,
                e.ClosingPrice, e.CreatedOn, e.CreatedBy)))
    ];
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => _descriptors.Select(static x => x.SourceEventType).ToArray();
}
