using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Extensions;

/// <summary>Exposes FuturesEodData command services as readonly extension properties.</summary>
public static class FuturesEodDataCommandExtensions
{
    extension(ICommandActorContext<FuturesEodDataCommandActor> context)
    {
        /// <summary>Gets the domain command context.</summary>
        public IFuturesEodDataCommandContext FuturesEodDataContext => IsArgumentNull.Set(context as IFuturesEodDataCommandContext)!;
        /// <summary>Gets the database factory.</summary>
        public IDbContextFactory DbFactory => context.FuturesEodDataContext.DbFactory;
        /// <summary>Gets the Market Data database.</summary>
        public IMarketDataDbContext MarketDataDb => context.FuturesEodDataContext.MarketDataDb;
        /// <summary>Gets the blackboard service.</summary>
        public IBlackboardService BlackboardService => context.FuturesEodDataContext.BlackboardService;
        /// <summary>Gets the actor logger.</summary>
        public ILogger<FuturesEodDataCommandActor> Logger => context.FuturesEodDataContext.Logger;
        /// <summary>Gets the event-source database.</summary>
        public IEventSourceActorDbContext DbEventSource => context.FuturesEodDataContext.DbEventSource;
        /// <summary>Gets the replay queue.</summary>
        public IDurableReplayQueue DurableReplayQueue => context.FuturesEodDataContext.DurableReplayQueue;
        /// <summary>Gets the state factory.</summary>
        public IEventSourceActorStateFactory StateFactory => context.FuturesEodDataContext.StateFactory;
        /// <summary>Gets the actor service.</summary>
        public IActorService ActorService => context.FuturesEodDataContext.ActorService;
        /// <summary>Gets the event projector.</summary>
        public IEventProjector<FuturesEodDataCommandActor> EventProjector => context.FuturesEodDataContext.EventProjector;
    }
}

