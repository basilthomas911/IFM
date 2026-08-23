using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;

/// <summary>Exposes MarketDataFeed command services as readonly extension properties.</summary>
public static class MarketDataFeedCommandExtensions
{
    extension(ICommandActorContext<MarketDataFeedCommandActor> context)
    {
        /// <summary>Gets the domain command context.</summary>
        public IMarketDataFeedCommandContext MarketDataFeedContext => IsArgumentNull.Set(context as IMarketDataFeedCommandContext)!;
        /// <summary>Gets the database factory.</summary>
        public IDbContextFactory DbFactory => context.MarketDataFeedContext.DbFactory;
        /// <summary>Gets the Market Data database.</summary>
        public IMarketDataDbContext MarketDataDb => context.MarketDataFeedContext.MarketDataDb;
        /// <summary>Gets the blackboard service.</summary>
        public IBlackboardService BlackboardService => context.MarketDataFeedContext.BlackboardService;
        /// <summary>Gets the actor logger.</summary>
        public ILogger<MarketDataFeedCommandActor> Logger => context.MarketDataFeedContext.Logger;
        /// <summary>Gets the event-source database.</summary>
        public IEventSourceActorDbContext DbEventSource => context.MarketDataFeedContext.DbEventSource;
        /// <summary>Gets the replay queue.</summary>
        public IDurableReplayQueue DurableReplayQueue => context.MarketDataFeedContext.DurableReplayQueue;
        /// <summary>Gets the state factory.</summary>
        public IEventSourceActorStateFactory StateFactory => context.MarketDataFeedContext.StateFactory;
        /// <summary>Gets the actor service.</summary>
        public IActorService ActorService => context.MarketDataFeedContext.ActorService;
        /// <summary>Gets the event projector.</summary>
        public IEventProjector<MarketDataFeedCommandActor> EventProjector => context.MarketDataFeedContext.EventProjector;
    }
}

