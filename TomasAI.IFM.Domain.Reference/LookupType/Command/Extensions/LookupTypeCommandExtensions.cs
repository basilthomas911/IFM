using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Reference.LookupType.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Reference.LookupType.Command.Extensions;

/// <summary>Provides readonly lookup-type services on the typed command context.</summary>
public static class LookupTypeCommandExtensions
{
    extension(ICommandActorContext<LookupTypeCommandActor> context)
    {
        /// <summary>Gets the database-context factory.</summary>
        public IDbContextFactory DbFactory => Typed(context).DbFactory;
        /// <summary>Gets the blackboard service.</summary>
        public IBlackboardService BlackboardService => Typed(context).BlackboardService;
        /// <summary>Gets the command actor logger.</summary>
        public ILogger<LookupTypeCommandActor> Logger => Typed(context).Logger;
        /// <summary>Gets the event-source database context.</summary>
        public IEventSourceActorDbContext DbEventSource => Typed(context).DbEventSource;
        /// <summary>Gets the durable replay queue.</summary>
        public IDurableReplayQueue DurableReplayQueue => Typed(context).DurableReplayQueue;
        /// <summary>Gets the state factory.</summary>
        public IEventSourceActorStateFactory StateFactory => Typed(context).StateFactory;
        /// <summary>Gets the actor service.</summary>
        public IActorService ActorService => Typed(context).ActorService;
        /// <summary>Gets the lookup-type projector.</summary>
        public IEventProjector<LookupTypeCommandActor> EventProjector => Typed(context).EventProjector;
    }

    static ILookupTypeCommandContext Typed(ICommandActorContext<LookupTypeCommandActor> context)
        => IsArgumentNull.Set(context as ILookupTypeCommandContext, nameof(context))!;
}
