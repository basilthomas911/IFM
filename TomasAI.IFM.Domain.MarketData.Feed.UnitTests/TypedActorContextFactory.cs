using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests;

internal static class TypedActorContextFactory
{
    internal static TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event.Actor.IFuturesBarDataEventContext Event(IActorSupervisor supervisor, TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Model.IFuturesBarDataTimer timer, TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi marketDataApi, TomasAI.IFM.Shared.StatusConsole.ServiceApi.IStatusConsoleWriter status, ILogger<TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event.Actor.FuturesBarDataEventActor> logger)
    { var c=Substitute.For<TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event.Actor.IFuturesBarDataEventContext>(); c.Supervisor.Returns(supervisor); c.Logger.Returns(logger); c.FuturesBarDataTimer.Returns(timer); c.MarketDataApi.Returns(marketDataApi); c.StatusConsoleWriter.Returns(status); c.ActorId.Returns(new ActorMailboxId(ActorType.Event, TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event.Actor.FuturesBarDataEventActor.Actor)); return c; }

    internal static TomasAI.IFM.Domain.MarketData.Feed.Event.Actor.IMarketDataFeedEventContext Event(IActorSupervisor supervisor, TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi marketDataApi, TomasAI.IFM.Domain.Trade.Shared.Contracts.IOptionTradeLiveFeedMap map, TomasAI.IFM.Application.Blackboard.IBlackboardService blackboard, TomasAI.IFM.Shared.StatusConsole.ServiceApi.IStatusConsoleWriter status, ILogger<TomasAI.IFM.Domain.MarketData.Feed.Event.Actor.MarketDataFeedEventActor> logger)
    { var c=Substitute.For<TomasAI.IFM.Domain.MarketData.Feed.Event.Actor.IMarketDataFeedEventContext>(); c.Supervisor.Returns(supervisor); c.Logger.Returns(logger); c.MarketDataLifecycle.Returns(Substitute.For<TomasAI.IFM.Application.MarketData.Databento.Resiliency.IMarketDataLifecycleRequests>()); c.OptionTradeLiveFeedMap.Returns(map); c.BlackboardService.Returns(blackboard); c.StatusConsoleWriter.Returns(status); c.ActorId.Returns(new ActorMailboxId(ActorType.Event, TomasAI.IFM.Domain.MarketData.Feed.Event.Actor.MarketDataFeedEventActor.Actor)); return c; }

    internal static TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event.Actor.IFuturesEodDataEventContext Event(IActorSupervisor supervisor, TomasAI.IFM.Application.Blackboard.IBlackboardService blackboard, TomasAI.IFM.Shared.StatusConsole.ServiceApi.IStatusConsoleWriter status, ILogger<TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event.Actor.FuturesEodDataEventActor> logger)
    { var c=Substitute.For<TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event.Actor.IFuturesEodDataEventContext>(); c.Supervisor.Returns(supervisor); c.Logger.Returns(logger); c.BlackboardService.Returns(blackboard); c.StatusConsoleWriter.Returns(status); c.ActorId.Returns(new ActorMailboxId(ActorType.Event, TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event.Actor.FuturesEodDataEventActor.Actor)); return c; }

    internal static TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Actor.IFuturesOptionTickDataEventContext Event(IActorSupervisor supervisor, TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi marketDataApi, TomasAI.IFM.Shared.StatusConsole.ServiceApi.IStatusConsoleWriter status, ILogger<TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Actor.FuturesOptionTickDataEventActor> logger)
    { var c=Substitute.For<TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Actor.IFuturesOptionTickDataEventContext>(); c.Supervisor.Returns(supervisor); c.Logger.Returns(logger); c.MarketDataApi.Returns(marketDataApi); c.StatusConsoleWriter.Returns(status); c.ActorId.Returns(new ActorMailboxId(ActorType.Event, TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Actor.FuturesOptionTickDataEventActor.Actor)); return c; }

    internal static TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.Actor.IFuturesTickDataEventContext Event(IActorSupervisor supervisor, TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi marketDataApi, TomasAI.IFM.Application.Blackboard.IBlackboardService blackboard, TomasAI.IFM.Shared.StatusConsole.ServiceApi.IStatusConsoleWriter status, ILogger<TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.Actor.FuturesTickDataEventActor> logger)
    { var c=Substitute.For<TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.Actor.IFuturesTickDataEventContext>(); c.Supervisor.Returns(supervisor); c.Logger.Returns(logger); c.MarketDataApi.Returns(marketDataApi); c.BlackboardService.Returns(blackboard); c.StatusConsoleWriter.Returns(status); c.ActorId.Returns(new ActorMailboxId(ActorType.Event, TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.Actor.FuturesTickDataEventActor.Actor)); return c; }

    internal static TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor.IFuturesEodDataRealtimeContext Realtime(
        IActorSupervisor supervisor,
        TomasAI.IFM.Application.EventProjector.Realtime.Contracts.IRealtimeProjector<TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor.FuturesEodDataRealtimeActor> projector,
        TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi marketDataApi,
        TomasAI.IFM.Application.Blackboard.IBlackboardService blackboard,
        TomasAI.IFM.Shared.StatusConsole.ServiceApi.IStatusConsoleWriter status,
        ILogger<TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor.FuturesEodDataRealtimeActor> logger)
    {
        var context = Substitute.For<TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor.IFuturesEodDataRealtimeContext>();
        context.Supervisor.Returns(supervisor);
        context.Projector.Returns(projector);
        context.MarketDataApi.Returns(marketDataApi);
        context.BlackboardService.Returns(blackboard);
        context.StatusConsoleWriter.Returns(status);
        context.Logger.Returns(logger);
        context.ActorId.Returns(new ActorMailboxId(
            ActorType.Realtime,
            TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor.FuturesEodDataRealtimeActor.ActorName));
        return context;
    }

    internal static IMarketDataFeedCommandContext Command(IEventSourceActorDbContext db, ILogger<MarketDataFeedCommandActor> logger)
    {
        var context = Substitute.For<IMarketDataFeedCommandContext>();
        context.DbEventSource.Returns(db);
        context.Logger.Returns(logger);
        context.ActorId.Returns(new ActorMailboxId(ActorType.Command, MarketDataFeedCommandActor.ActorName));
        context.DbFactory.Returns(Substitute.For<IDbContextFactory>());
        context.BlackboardService.Returns(Substitute.For<TomasAI.IFM.Application.Blackboard.IBlackboardService>());
        context.DurableReplayQueue.Returns(Substitute.For<IDurableReplayQueue>());
        return context;
    }

    internal static IFuturesBarDataCommandContext Command(IEventSourceActorDbContext db, ILogger<FuturesBarDataCommandActor> logger)
    {
        var context = Substitute.For<IFuturesBarDataCommandContext>();
        context.DbEventSource.Returns(db);
        context.Logger.Returns(logger);
        context.ActorId.Returns(new ActorMailboxId(ActorType.Command, FuturesBarDataCommandActor.ActorName));
        context.DbFactory.Returns(Substitute.For<IDbContextFactory>());
        context.BlackboardService.Returns(Substitute.For<TomasAI.IFM.Application.Blackboard.IBlackboardService>());
        context.DurableReplayQueue.Returns(Substitute.For<IDurableReplayQueue>());
        return context;
    }

    internal static IFuturesClosingPriceCommandContext Command(IEventSourceActorDbContext db, ILogger<FuturesClosingPriceCommandActor> logger)
    {
        var context = Substitute.For<IFuturesClosingPriceCommandContext>();
        context.DbEventSource.Returns(db);
        context.Logger.Returns(logger);
        context.ActorId.Returns(new ActorMailboxId(ActorType.Command, FuturesClosingPriceCommandActor.ActorName));
        context.DbFactory.Returns(Substitute.For<IDbContextFactory>());
        context.BlackboardService.Returns(Substitute.For<TomasAI.IFM.Application.Blackboard.IBlackboardService>());
        context.DurableReplayQueue.Returns(Substitute.For<IDurableReplayQueue>());
        return context;
    }

    internal static IFuturesEodDataCommandContext Command(IEventSourceActorDbContext db, ILogger<FuturesEodDataCommandActor> logger)
    {
        var context = Substitute.For<IFuturesEodDataCommandContext>();
        context.DbEventSource.Returns(db);
        context.Logger.Returns(logger);
        context.ActorId.Returns(new ActorMailboxId(ActorType.Command, FuturesEodDataCommandActor.ActorName));
        context.DbFactory.Returns(Substitute.For<IDbContextFactory>());
        context.BlackboardService.Returns(Substitute.For<TomasAI.IFM.Application.Blackboard.IBlackboardService>());
        context.DurableReplayQueue.Returns(Substitute.For<IDurableReplayQueue>());
        return context;
    }

    internal static IFuturesOptionTickDataCommandContext Command(IEventSourceActorDbContext db, ILogger<FuturesOptionTickDataCommandActor> logger)
    {
        var context = Substitute.For<IFuturesOptionTickDataCommandContext>();
        context.DbEventSource.Returns(db);
        context.Logger.Returns(logger);
        context.ActorId.Returns(new ActorMailboxId(ActorType.Command, FuturesOptionTickDataCommandActor.ActorName));
        context.DbFactory.Returns(Substitute.For<IDbContextFactory>());
        context.BlackboardService.Returns(Substitute.For<TomasAI.IFM.Application.Blackboard.IBlackboardService>());
        context.DurableReplayQueue.Returns(Substitute.For<IDurableReplayQueue>());
        return context;
    }

    internal static IFuturesTickDataCommandContext Command(IEventSourceActorDbContext db, ILogger<FuturesTickDataCommandActor> logger)
    {
        var context = Substitute.For<IFuturesTickDataCommandContext>();
        context.DbEventSource.Returns(db);
        context.Logger.Returns(logger);
        context.ActorId.Returns(new ActorMailboxId(ActorType.Command, FuturesTickDataCommandActor.ActorName));
        context.DbFactory.Returns(Substitute.For<IDbContextFactory>());
        context.BlackboardService.Returns(Substitute.For<TomasAI.IFM.Application.Blackboard.IBlackboardService>());
        context.DurableReplayQueue.Returns(Substitute.For<IDurableReplayQueue>());
        return context;
    }

    internal static IMarketDataFeedQueryContext Query(IDbContextFactory db, ILogger<MarketDataFeedQueryActor> logger)
    {
        var context = Substitute.For<IMarketDataFeedQueryContext>();
        context.DbFactory.Returns(db);
        context.Logger.Returns(logger);
        context.ActorId.Returns(new ActorMailboxId(ActorType.Query, MarketDataFeedQueryActor.ActorName));
        context.MarketDataApi.Returns(Substitute.For<TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi>());
        context.SequenceIdGenerator.Returns(Substitute.For<TomasAI.IFM.Framework.SequenceId.ISequenceIdGenerator>());
        return context;
    }

    internal static IFuturesBarDataQueryContext Query(IDbContextFactory db, ILogger<FuturesBarDataQueryActor> logger)
    {
        var context = Substitute.For<IFuturesBarDataQueryContext>();
        context.DbFactory.Returns(db);
        context.Logger.Returns(logger);
        context.ActorId.Returns(new ActorMailboxId(ActorType.Query, FuturesBarDataQueryActor.ActorName));
        return context;
    }

    internal static IFuturesEodDataQueryContext Query(IDbContextFactory db, ILogger<FuturesEodDataQueryActor> logger)
    {
        var context = Substitute.For<IFuturesEodDataQueryContext>();
        context.DbFactory.Returns(db);
        context.Logger.Returns(logger);
        context.ActorId.Returns(new ActorMailboxId(ActorType.Query, FuturesEodDataQueryActor.ActorName));
        return context;
    }

    internal static IFuturesOptionTickDataQueryContext Query(IDbContextFactory db, ILogger<FuturesOptionTickDataQueryActor> logger)
    {
        var context = Substitute.For<IFuturesOptionTickDataQueryContext>();
        context.DbFactory.Returns(db);
        context.Logger.Returns(logger);
        context.ActorId.Returns(new ActorMailboxId(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName));
        return context;
    }

    internal static IFuturesTickDataQueryContext Query(IDbContextFactory db, ILogger<FuturesTickDataQueryActor> logger)
    {
        var context = Substitute.For<IFuturesTickDataQueryContext>();
        context.DbFactory.Returns(db);
        context.Logger.Returns(logger);
        context.ActorId.Returns(new ActorMailboxId(ActorType.Query, FuturesTickDataQueryActor.ActorName));
        return context;
    }

}
