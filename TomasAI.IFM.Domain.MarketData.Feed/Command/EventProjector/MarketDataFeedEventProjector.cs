using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command.EventProjector;

public sealed class MarketDataFeedEventProjector(
    ICommandActorContext<MarketDataFeedCommandActor> actorContext,
    ILogger<MarketDataFeedEventProjector> logger, EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<MarketDataFeedCommandActor>(actorContext.DurableReplayQueue, actorContext.DbEventSource, actorContext.BlackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        // Feed lifecycle operations are idempotent for the same value date. Durable
        // replay prevents an accepted command from being stranded without its
        // started/stopped terminal event when projector delivery is interrupted.
        DescribeNotification<MarketDataFeedStartedEvent, MarketDataFeedId>(useDurableReplay: true),
        DescribeNotification<MarketDataFeedStoppedEvent, MarketDataFeedId>(useDurableReplay: true),
        DescribeNotification<MarketDataFeedResetEvent, MarketDataFeedId>(),
        DescribeNotification<TradeLiveFeedAddedEvent, TradeLiveFeedId>(),
        DescribeNotification<TradeLiveFeedRemovedEvent, TradeLiveFeedId>(),
        DescribeNotification<TradeLiveFeedHaltedEvent, MarketDataFeedId>(),
        Describe<TradeLiveFeedTurnedOnEvent, TradeLiveFeedTurnedOnCompleteEvent, TradeLiveFeedAddedFailEvent, TradeLiveFeedId>(
            e => actorContext.MarketDataDb.InsertTradeLiveFeedAsync(new TradeLiveFeedReadModel(e.OrderId, e.TradeId, TradeLiveFeedStateType.On))),
        Describe<TradeLiveFeedTurnedOffEvent, TradeLiveFeedTurnedOffCompleteEvent, TradeLiveFeedTurnedOffFailEvent, TradeLiveFeedId>(
            e => actorContext.MarketDataDb.DeleteTradeLiveFeedAsync(e.OrderId, e.TradeId)),
        DescribeNotification<StreamingRequestIdDeletedEvent, FeedId>()
    ];
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => _descriptors.Select(static x => x.SourceEventType).ToArray();
}
