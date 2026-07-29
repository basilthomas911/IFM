using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Command.Actor;

namespace TomasAI.IFM.Domain.Fund.Command.State;

/// <summary>
/// Provides a repository for managing the state of funds using event sourcing and actor-based persistence.
/// </summary>
/// <param name="stateFactory">The factory used to create actor state instances for event sourcing operations.</param>
/// <param name="dbEventSource">The database context for accessing event source data.</param>
/// <param name="actorService">The actor service responsible for managing actor lifecycles and communication.</param>
/// <param name="logger">The logger used to record diagnostic and operational information.</param>
public class FundStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext dbEventSource,
    IActorService actorService,
    IEventProjector<FundCommandActor> fundEventProjector,
    ILogger<FundStateRepository> logger) 
    : BaseEventSourceActorRepository(stateFactory, dbEventSource, actorService, logger), IEventSourceActorStateRepository<FundCommandState>
{
    /// <summary>
    /// load fund state from snapshot event
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask<FundCommandState> LoadStateAsync(ICommand command)
        => await LoadStateFromSnapshotAsync<FundCommandState, FundCreatedEvent>(command);

    /// <summary>
    /// save fund state changes
    /// </summary>
    /// <param name="context"></param>
    /// <param name="state"></param>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, FundCommandState state, ICommand command)
       => await SaveStateAndDenormalizeEventsAsync(context, state, command);

    /// <summary>
    /// Denormalize events to update read models or projections based on the domain events.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="domainEvents"></param>
    /// <returns></returns>
    protected override async ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
        => await fundEventProjector.DomainEventsProjectionAsync(domainEvents);
}

