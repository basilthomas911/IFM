using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Extensions;

/// <summary>Exposes FuturesOptionTickData command services as readonly extension properties.</summary>
public static class FuturesOptionTickDataCommandExtensions
{
    extension(ICommandActorContext<FuturesOptionTickDataCommandActor> context)
    {
        /// <summary>Gets the domain command context.</summary>
        public IFuturesOptionTickDataCommandContext FuturesOptionTickDataContext => IsArgumentNull.Set(context as IFuturesOptionTickDataCommandContext)!;
        /// <summary>Gets the database factory.</summary>
        public IDbContextFactory DbFactory => context.FuturesOptionTickDataContext.DbFactory;
        /// <summary>Gets the Market Data database.</summary>
        public IMarketDataDbContext MarketDataDb => context.FuturesOptionTickDataContext.MarketDataDb;
        /// <summary>Gets the blackboard service.</summary>
        public IBlackboardService BlackboardService => context.FuturesOptionTickDataContext.BlackboardService;
        /// <summary>Gets the actor logger.</summary>
        public ILogger<FuturesOptionTickDataCommandActor> Logger => context.FuturesOptionTickDataContext.Logger;
        /// <summary>Gets the event-source database.</summary>
        public IEventSourceActorDbContext DbEventSource => context.FuturesOptionTickDataContext.DbEventSource;
        /// <summary>Gets the replay queue.</summary>
        public IDurableReplayQueue DurableReplayQueue => context.FuturesOptionTickDataContext.DurableReplayQueue;
        /// <summary>Gets the state factory.</summary>
        public IEventSourceActorStateFactory StateFactory => context.FuturesOptionTickDataContext.StateFactory;
        /// <summary>Gets the actor service.</summary>
        public IActorService ActorService => context.FuturesOptionTickDataContext.ActorService;
        /// <summary>Gets the event projector.</summary>
        public IEventProjector<FuturesOptionTickDataCommandActor> EventProjector => context.FuturesOptionTickDataContext.EventProjector;
    }
}

