using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Extensions;

/// <summary>Exposes FuturesBarData command services as readonly extension properties.</summary>
public static class FuturesBarDataCommandExtensions
{
    extension(ICommandActorContext<FuturesBarDataCommandActor> context)
    {
        /// <summary>Gets the domain command context.</summary>
        public IFuturesBarDataCommandContext FuturesBarDataContext => IsArgumentNull.Set(context as IFuturesBarDataCommandContext)!;
        /// <summary>Gets the database factory.</summary>
        public IDbContextFactory DbFactory => context.FuturesBarDataContext.DbFactory;
        /// <summary>Gets the Market Data database.</summary>
        public IMarketDataDbContext MarketDataDb => context.FuturesBarDataContext.MarketDataDb;
        /// <summary>Gets the blackboard service.</summary>
        public IBlackboardService BlackboardService => context.FuturesBarDataContext.BlackboardService;
        /// <summary>Gets the actor logger.</summary>
        public ILogger<FuturesBarDataCommandActor> Logger => context.FuturesBarDataContext.Logger;
        /// <summary>Gets the event-source database.</summary>
        public IEventSourceActorDbContext DbEventSource => context.FuturesBarDataContext.DbEventSource;
        /// <summary>Gets the replay queue.</summary>
        public IDurableReplayQueue DurableReplayQueue => context.FuturesBarDataContext.DurableReplayQueue;
        /// <summary>Gets the state factory.</summary>
        public IEventSourceActorStateFactory StateFactory => context.FuturesBarDataContext.StateFactory;
        /// <summary>Gets the actor service.</summary>
        public IActorService ActorService => context.FuturesBarDataContext.ActorService;
        /// <summary>Gets the event projector.</summary>
        public IEventProjector<FuturesBarDataCommandActor> EventProjector => context.FuturesBarDataContext.EventProjector;
    }
}

