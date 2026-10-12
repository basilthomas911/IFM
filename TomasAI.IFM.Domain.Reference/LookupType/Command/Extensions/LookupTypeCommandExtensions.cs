using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Reference.LookupType.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Reference.LookupType.Command.Extensions;
/// <summary>Provides readonly lookup-type services on the typed command context.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared receiver; static conversion would remove the established actor context extension contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034", Justification = "C# 14 extension blocks generate nested implementation types; the public API consists of actor context extension members, not nested domain types.")]
public static class LookupTypeCommandExtensions
{
#pragma warning disable S3398 // C# 14 extension lowering diagnostic; this source has no redundant cast or local function to move.
    static ILookupTypeCommandContext Typed(ICommandActorContext<LookupTypeCommandActor> context) => IsArgumentNull.Named(context as ILookupTypeCommandContext, nameof(context))!;
#pragma warning restore S3398
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
}
