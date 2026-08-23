using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Extensions;

/// <summary>Exposes futures-contract command services as readonly extension properties.</summary>
public static class FuturesContractCommandExtensions
{
    extension(ICommandActorContext<FuturesContractCommandActor> context)
    {
        /// <summary>Gets the domain command context.</summary>
        public IFuturesContractCommandContext FuturesContractContext => IsArgumentNull.Set(context as IFuturesContractCommandContext)!;
        /// <summary>Gets the database-context factory.</summary>
        public IDbContextFactory DbFactory => context.FuturesContractContext.DbFactory;
        /// <summary>Gets the blackboard service.</summary>
        public IBlackboardService BlackboardService => context.FuturesContractContext.BlackboardService;
        /// <summary>Gets the actor logger.</summary>
        public ILogger<FuturesContractCommandActor> Logger => context.FuturesContractContext.Logger;
        /// <summary>Gets the event-source database context.</summary>
        public IEventSourceActorDbContext DbEventSource => context.FuturesContractContext.DbEventSource;
        /// <summary>Gets the durable replay queue.</summary>
        public IDurableReplayQueue DurableReplayQueue => context.FuturesContractContext.DurableReplayQueue;
        /// <summary>Gets the state factory.</summary>
        public IEventSourceActorStateFactory StateFactory => context.FuturesContractContext.StateFactory;
        /// <summary>Gets the actor service.</summary>
        public IActorService ActorService => context.FuturesContractContext.ActorService;
        /// <summary>Gets the event projector.</summary>
        public IEventProjector<FuturesContractCommandActor> EventProjector => context.FuturesContractContext.EventProjector;
        /// <summary>Gets the reference lookup service.</summary>
        public IReferenceLookupService ReferenceLookupService => context.FuturesContractContext.ReferenceLookupService;
    }
}
