using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Extensions;

/// <summary>Exposes futures-option-contract command services as readonly extension properties.</summary>
public static class FuturesOptionContractCommandExtensions
{
    extension(ICommandActorContext<FuturesOptionContractCommandActor> context)
    {
        /// <summary>Gets the domain command context.</summary>
        public IFuturesOptionContractCommandContext FuturesOptionContractContext => IsArgumentNull.Set(context as IFuturesOptionContractCommandContext)!;
        /// <summary>Gets the database-context factory.</summary>
        public IDbContextFactory DbFactory => context.FuturesOptionContractContext.DbFactory;
        /// <summary>Gets the blackboard service.</summary>
        public IBlackboardService BlackboardService => context.FuturesOptionContractContext.BlackboardService;
        /// <summary>Gets the actor logger.</summary>
        public ILogger<FuturesOptionContractCommandActor> Logger => context.FuturesOptionContractContext.Logger;
        /// <summary>Gets the event-source database context.</summary>
        public IEventSourceActorDbContext DbEventSource => context.FuturesOptionContractContext.DbEventSource;
        /// <summary>Gets the durable replay queue.</summary>
        public IDurableReplayQueue DurableReplayQueue => context.FuturesOptionContractContext.DurableReplayQueue;
        /// <summary>Gets the state factory.</summary>
        public IEventSourceActorStateFactory StateFactory => context.FuturesOptionContractContext.StateFactory;
        /// <summary>Gets the actor service.</summary>
        public IActorService ActorService => context.FuturesOptionContractContext.ActorService;
        /// <summary>Gets the event projector.</summary>
        public IEventProjector<FuturesOptionContractCommandActor> EventProjector => context.FuturesOptionContractContext.EventProjector;
        /// <summary>Gets the reference lookup service.</summary>
        public IReferenceLookupService ReferenceLookupService => context.FuturesOptionContractContext.ReferenceLookupService;
    }
}
