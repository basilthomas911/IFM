using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Reference.LookupType.Command.Actor;

/// <summary>Defines the runtime services required by <see cref="LookupTypeCommandActor"/>.</summary>
public interface ILookupTypeCommandContext : ICommandActorContext<LookupTypeCommandActor>
{
    /// <summary>Gets the database-context factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the application blackboard service.</summary>
    IBlackboardService BlackboardService { get; }
    /// <summary>Gets the command actor logger.</summary>
    ILogger<LookupTypeCommandActor> Logger { get; }
    /// <summary>Gets the event-source database context resolved once.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets the durable replay queue resolved once.</summary>
    IDurableReplayQueue DurableReplayQueue { get; }
    /// <summary>Gets the state factory resolved once.</summary>
    IEventSourceActorStateFactory StateFactory { get; }
    /// <summary>Gets the actor service resolved once.</summary>
    IActorService ActorService { get; }
    /// <summary>Gets the lookup-type projector resolved once.</summary>
    IEventProjector<LookupTypeCommandActor> EventProjector { get; }
}
