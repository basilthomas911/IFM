using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.Extensions;

/// <summary>Exposes FuturesTickData command services as readonly extension properties.</summary>
public static class FuturesTickDataCommandExtensions
{
    extension(ICommandActorContext<FuturesTickDataCommandActor> context)
    {
        /// <summary>Gets the domain command context.</summary>
        public IFuturesTickDataCommandContext FuturesTickDataContext => IsArgumentNull.Set(context as IFuturesTickDataCommandContext)!;
        /// <summary>Gets the database factory.</summary>
        public IDbContextFactory DbFactory => context.FuturesTickDataContext.DbFactory;
        /// <summary>Gets the Market Data database.</summary>
        public IMarketDataDbContext MarketDataDb => context.FuturesTickDataContext.MarketDataDb;
        /// <summary>Gets the blackboard service.</summary>
        public IBlackboardService BlackboardService => context.FuturesTickDataContext.BlackboardService;
        /// <summary>Gets the actor logger.</summary>
        public ILogger<FuturesTickDataCommandActor> Logger => context.FuturesTickDataContext.Logger;
        /// <summary>Gets the event-source database.</summary>
        public IEventSourceActorDbContext DbEventSource => context.FuturesTickDataContext.DbEventSource;
        /// <summary>Gets the replay queue.</summary>
        public IDurableReplayQueue DurableReplayQueue => context.FuturesTickDataContext.DurableReplayQueue;
        /// <summary>Gets the state factory.</summary>
        public IEventSourceActorStateFactory StateFactory => context.FuturesTickDataContext.StateFactory;
        /// <summary>Gets the actor service.</summary>
        public IActorService ActorService => context.FuturesTickDataContext.ActorService;
        /// <summary>Gets the event projector.</summary>
        public IEventProjector<FuturesTickDataCommandActor> EventProjector => context.FuturesTickDataContext.EventProjector;
    }
}

