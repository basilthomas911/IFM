using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Fund.Transaction.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Fund.Transaction.Command.Extensions;

/// <summary>
/// Provides Fund transaction services as readonly extension properties on a typed command context.
/// </summary>
public static class FundTransactionCommandExtensions
{
    extension(ICommandActorContext<FundTransactionCommandActor> context)
    {
        /// <summary>Gets the Fund database-context factory.</summary>
        public IDbContextFactory DbFactory => GetContext(context).DbFactory;

        /// <summary>Gets the blackboard service.</summary>
        public IBlackboardService BlackboardService => GetContext(context).BlackboardService;

        /// <summary>Gets the command actor logger.</summary>
        public ILogger<FundTransactionCommandActor> Logger => GetContext(context).Logger;

        /// <summary>Gets the event-source database context.</summary>
        public IEventSourceActorDbContext DbEventSource => GetContext(context).DbEventSource;

        /// <summary>Gets the durable replay queue.</summary>
        public IDurableReplayQueue DurableReplayQueue => GetContext(context).DurableReplayQueue;

        /// <summary>Gets the event-sourced actor-state factory.</summary>
        public IEventSourceActorStateFactory StateFactory => GetContext(context).StateFactory;

        /// <summary>Gets the actor service.</summary>
        public IActorService ActorService => GetContext(context).ActorService;

        /// <summary>Gets the Fund transaction event projector.</summary>
        public IEventProjector<FundTransactionCommandActor> EventProjector => GetContext(context).EventProjector;
    }

    static IFundTransactionCommandContext GetContext(
        ICommandActorContext<FundTransactionCommandActor> context)
        => IsArgumentNull.Set(context as IFundTransactionCommandContext, nameof(context))!;
}
