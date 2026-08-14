using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command.EventProjector;

public sealed class MarketDataFeedEventProjector(
    IMarketDataDbContext db, IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource, IBlackboardService blackboardService,
    ILogger<MarketDataFeedEventProjector> logger, EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<MarketDataFeedCommandActor>(durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        DescribeNotification<MarketDataFeedStartedEvent, MarketDataFeedId>(useDurableReplay: false),
        DescribeNotification<MarketDataFeedStoppedEvent, MarketDataFeedId>(useDurableReplay: false),
        DescribeNotification<MarketDataFeedResetEvent, MarketDataFeedId>(),
        DescribeNotification<TradeLiveFeedAddedEvent, TradeLiveFeedId>(),
        DescribeNotification<TradeLiveFeedRemovedEvent, TradeLiveFeedId>(),
        DescribeNotification<TradeLiveFeedHaltedEvent, MarketDataFeedId>(),
        Describe<TradeLiveFeedTurnedOnEvent, TradeLiveFeedTurnedOnCompleteEvent, TradeLiveFeedAddedFailEvent, TradeLiveFeedId>(
            e => db.InsertTradeLiveFeedAsync(new TradeLiveFeedReadModel(e.OrderId, e.TradeId, TradeLiveFeedStateType.On))),
        Describe<TradeLiveFeedTurnedOffEvent, TradeLiveFeedTurnedOffCompleteEvent, TradeLiveFeedTurnedOffFailEvent, TradeLiveFeedId>(
            e => db.DeleteTradeLiveFeedAsync(e.OrderId, e.TradeId)),
        DescribeNotification<StreamingRequestIdDeletedEvent, FeedId>()
    ];
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => _descriptors.Select(static x => x.SourceEventType).ToArray();
}
