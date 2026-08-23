using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Fund.Transaction.Command.Actor;

/// <summary>
/// Defines the runtime services required by <see cref="FundTransactionCommandActor"/> in addition to the shared
/// command actor context operations.
/// </summary>
public interface IFundTransactionCommandContext : ICommandActorContext<FundTransactionCommandActor>
{
    /// <summary>Gets the database-context factory used by Fund transaction processing.</summary>
    IDbContextFactory DbFactory { get; }

    /// <summary>Gets the application blackboard service used by Fund transaction projections.</summary>
    IBlackboardService BlackboardService { get; }

    /// <summary>Gets the logger associated with the Fund transaction command actor.</summary>
    ILogger<FundTransactionCommandActor> Logger { get; }

    /// <summary>Gets the event-source database context resolved once for this context.</summary>
    IEventSourceActorDbContext DbEventSource { get; }

    /// <summary>Gets the durable replay queue resolved once for this context.</summary>
    IDurableReplayQueue DurableReplayQueue { get; }

    /// <summary>Gets the event-sourced actor-state factory resolved once for this context.</summary>
    IEventSourceActorStateFactory StateFactory { get; }

    /// <summary>Gets the actor service resolved once for this context.</summary>
    IActorService ActorService { get; }

    /// <summary>Gets the Fund transaction event projector resolved once for this context.</summary>
    IEventProjector<FundTransactionCommandActor> EventProjector { get; }
}
