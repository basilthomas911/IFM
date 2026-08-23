using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Command.Actor;
using TomasAI.IFM.Domain.Fund.Command.Extensions;

namespace TomasAI.IFM.Domain.Fund.Command.State;

/// <summary>
/// Provides a repository for managing the state of funds using event sourcing and actor-based persistence.
/// </summary>
/// <param name="actorContext">
/// Provides the state factory, event-source database context, actor service, logger, and Fund event projector.
/// </param>
public sealed class FundStateRepository(
    ICommandActorContext<FundCommandActor> actorContext)
    : BaseEventSourceActorRepository(
        actorContext.StateFactory,
        actorContext.DbEventSource,
        actorContext.ActorService,
        actorContext.Logger),
      IEventSourceActorStateRepository<FundCommandState>
{
    readonly IEventProjector<FundCommandActor> _fundEventProjector =
        IsArgumentNull.Set(actorContext.EventProjector);

    /// <summary>
    /// load fund state from snapshot event
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask<FundCommandState> LoadStateAsync(ICommand command)
        => await LoadStateAsync(command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask<FundCommandState> LoadStateAsync(ICommand command, CancellationToken cancellationToken)
        => await LoadStateFromSnapshotAsync<FundCommandState, FundCreatedEvent>(command, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// save fund state changes
    /// </summary>
    /// <param name="context"></param>
    /// <param name="state"></param>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, FundCommandState state, ICommand command)
       => await SaveStateAsync(context, state, command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask SaveStateAsync(ICommandActorContext context, FundCommandState state, ICommand command, CancellationToken cancellationToken)
       => await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Denormalize events to update read models or projections based on the domain events.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="domainEvents"></param>
    /// <returns></returns>
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
        => _fundEventProjector.DomainEventsProjectionAsync(domainEvents);
}

