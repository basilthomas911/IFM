using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.Extensions;

/// <summary>Exposes FuturesClosingPrice command services as readonly extension properties.</summary>
public static class FuturesClosingPriceCommandExtensions
{
    extension(ICommandActorContext<FuturesClosingPriceCommandActor> context)
    {
        /// <summary>Gets the domain command context.</summary>
        public IFuturesClosingPriceCommandContext FuturesClosingPriceContext => IsArgumentNull.Set(context as IFuturesClosingPriceCommandContext)!;
        /// <summary>Gets the database factory.</summary>
        public IDbContextFactory DbFactory => context.FuturesClosingPriceContext.DbFactory;
        /// <summary>Gets the Market Data database.</summary>
        public IMarketDataDbContext MarketDataDb => context.FuturesClosingPriceContext.MarketDataDb;
        /// <summary>Gets the blackboard service.</summary>
        public IBlackboardService BlackboardService => context.FuturesClosingPriceContext.BlackboardService;
        /// <summary>Gets the actor logger.</summary>
        public ILogger<FuturesClosingPriceCommandActor> Logger => context.FuturesClosingPriceContext.Logger;
        /// <summary>Gets the event-source database.</summary>
        public IEventSourceActorDbContext DbEventSource => context.FuturesClosingPriceContext.DbEventSource;
        /// <summary>Gets the replay queue.</summary>
        public IDurableReplayQueue DurableReplayQueue => context.FuturesClosingPriceContext.DurableReplayQueue;
        /// <summary>Gets the state factory.</summary>
        public IEventSourceActorStateFactory StateFactory => context.FuturesClosingPriceContext.StateFactory;
        /// <summary>Gets the actor service.</summary>
        public IActorService ActorService => context.FuturesClosingPriceContext.ActorService;
        /// <summary>Gets the event projector.</summary>
        public IEventProjector<FuturesClosingPriceCommandActor> EventProjector => context.FuturesClosingPriceContext.EventProjector;
    }
}

