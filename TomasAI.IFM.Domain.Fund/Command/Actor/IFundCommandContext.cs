using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Fund.Command.Actor;

/// <summary>
/// Defines the runtime services required by <see cref="FundCommandActor"/> in addition to the shared command actor
/// context operations.
/// </summary>
public interface IFundCommandContext : ICommandActorContext<FundCommandActor>
{
    /// <summary>
    /// Gets the database-context factory used to access Fund persistence services.
    /// </summary>
    IDbContextFactory DbFactory { get; }

    /// <summary>
    /// Gets the application blackboard service used by Fund command processing.
    /// </summary>
    IBlackboardService BlackboardService { get; }

    /// <summary>
    /// Gets the logger associated with the Fund command actor.
    /// </summary>
    ILogger<FundCommandActor> Logger { get; }

    /// <summary>
    /// Gets the event-source database context resolved once for this Fund command context.
    /// </summary>
    IEventSourceActorDbContext DbEventSource { get; }

    /// <summary>
    /// Gets the durable replay queue resolved once for this Fund command context.
    /// </summary>
    IDurableReplayQueue DurableReplayQueue { get; }

    /// <summary>
    /// Gets the event-sourced actor-state factory resolved once for this Fund command context.
    /// </summary>
    IEventSourceActorStateFactory StateFactory { get; }

    /// <summary>
    /// Gets the actor service resolved once for this Fund command context.
    /// </summary>
    IActorService ActorService { get; }

    /// <summary>
    /// Gets the Fund event projector resolved once for this Fund command context.
    /// </summary>
    IEventProjector<FundCommandActor> EventProjector { get; }
}
