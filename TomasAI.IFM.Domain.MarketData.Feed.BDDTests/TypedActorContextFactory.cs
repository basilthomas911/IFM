using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Query.Actor;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.BDDTests;

internal static class TypedActorContextFactory
{
    internal static IMarketDataFeedCommandContext Command(
        IEventSourceActorDbContext db, IEventProjector<MarketDataFeedCommandActor> projector,
        ILogger<MarketDataFeedCommandActor> logger)
    {
        var context = Substitute.For<IMarketDataFeedCommandContext>();
        context.DbEventSource.Returns(db);
        context.EventProjector.Returns(projector);
        context.Logger.Returns(logger);
        context.ActorId.Returns(new ActorMailboxId(ActorType.Command, MarketDataFeedCommandActor.ActorName));
        return context;
    }

    internal static IFuturesBarDataCommandContext Command(
        IEventSourceActorDbContext db, IEventProjector<FuturesBarDataCommandActor> projector,
        ILogger<FuturesBarDataCommandActor> logger)
    {
        var context = Substitute.For<IFuturesBarDataCommandContext>();
        context.DbEventSource.Returns(db);
        context.EventProjector.Returns(projector);
        context.Logger.Returns(logger);
        context.ActorId.Returns(new ActorMailboxId(ActorType.Command, FuturesBarDataCommandActor.ActorName));
        return context;
    }

    internal static IFuturesClosingPriceCommandContext Command(
        IEventSourceActorDbContext db, IEventProjector<FuturesClosingPriceCommandActor> projector,
        ILogger<FuturesClosingPriceCommandActor> logger)
    {
        var context = Substitute.For<IFuturesClosingPriceCommandContext>();
        context.DbEventSource.Returns(db);
        context.EventProjector.Returns(projector);
        context.Logger.Returns(logger);
        context.ActorId.Returns(new ActorMailboxId(ActorType.Command, FuturesClosingPriceCommandActor.ActorName));
        return context;
    }

    internal static IFuturesEodDataCommandContext Command(
        IEventSourceActorDbContext db, IEventProjector<FuturesEodDataCommandActor> projector,
        ILogger<FuturesEodDataCommandActor> logger)
    {
        var context = Substitute.For<IFuturesEodDataCommandContext>();
        context.DbEventSource.Returns(db);
        context.EventProjector.Returns(projector);
        context.Logger.Returns(logger);
        context.ActorId.Returns(new ActorMailboxId(ActorType.Command, FuturesEodDataCommandActor.ActorName));
        return context;
    }

    internal static IFuturesOptionTickDataCommandContext Command(
        IEventSourceActorDbContext db, IEventProjector<FuturesOptionTickDataCommandActor> projector,
        ILogger<FuturesOptionTickDataCommandActor> logger)
    {
        var context = Substitute.For<IFuturesOptionTickDataCommandContext>();
        context.DbEventSource.Returns(db);
        context.EventProjector.Returns(projector);
        context.Logger.Returns(logger);
        context.ActorId.Returns(new ActorMailboxId(ActorType.Command, FuturesOptionTickDataCommandActor.ActorName));
        return context;
    }

    internal static IFuturesTickDataCommandContext Command(
        IEventSourceActorDbContext db, IEventProjector<FuturesTickDataCommandActor> projector,
        ILogger<FuturesTickDataCommandActor> logger)
    {
        var context = Substitute.For<IFuturesTickDataCommandContext>();
        context.DbEventSource.Returns(db);
        context.EventProjector.Returns(projector);
        context.Logger.Returns(logger);
        context.ActorId.Returns(new ActorMailboxId(ActorType.Command, FuturesTickDataCommandActor.ActorName));
        return context;
    }

    internal static IMarketDataFeedQueryContext Query(
        ApplicationMarketDataApi marketDataApi, ISequenceIdGenerator sequenceIdGenerator,
        IDbContextFactory dbFactory, ILogger<MarketDataFeedQueryActor> logger)
    {
        var context = Substitute.For<IMarketDataFeedQueryContext>();
        context.DbFactory.Returns(dbFactory);
        context.Logger.Returns(logger);
        context.ActorId.Returns(new ActorMailboxId(ActorType.Query, MarketDataFeedQueryActor.ActorName));
        context.MarketDataApi.Returns(marketDataApi);
        context.SequenceIdGenerator.Returns(sequenceIdGenerator);
        return context;
    }

    internal static IFuturesBarDataQueryContext Query(
        IDbContextFactory dbFactory, ILogger<FuturesBarDataQueryActor> logger)
    {
        var context = Substitute.For<IFuturesBarDataQueryContext>();
        context.DbFactory.Returns(dbFactory);
        context.Logger.Returns(logger);
        context.ActorId.Returns(new ActorMailboxId(ActorType.Query, FuturesBarDataQueryActor.ActorName));
        return context;
    }

    internal static IFuturesEodDataQueryContext Query(
        IDbContextFactory dbFactory, ILogger<FuturesEodDataQueryActor> logger)
    {
        var context = Substitute.For<IFuturesEodDataQueryContext>();
        context.DbFactory.Returns(dbFactory);
        context.Logger.Returns(logger);
        context.ActorId.Returns(new ActorMailboxId(ActorType.Query, FuturesEodDataQueryActor.ActorName));
        return context;
    }

    internal static IFuturesOptionTickDataQueryContext Query(
        IDbContextFactory dbFactory, ILogger<FuturesOptionTickDataQueryActor> logger)
    {
        var context = Substitute.For<IFuturesOptionTickDataQueryContext>();
        context.DbFactory.Returns(dbFactory);
        context.Logger.Returns(logger);
        context.ActorId.Returns(new ActorMailboxId(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName));
        return context;
    }

    internal static IFuturesTickDataQueryContext Query(
        IDbContextFactory dbFactory, ILogger<FuturesTickDataQueryActor> logger)
    {
        var context = Substitute.For<IFuturesTickDataQueryContext>();
        context.DbFactory.Returns(dbFactory);
        context.Logger.Returns(logger);
        context.ActorId.Returns(new ActorMailboxId(ActorType.Query, FuturesTickDataQueryActor.ActorName));
        return context;
    }
}
