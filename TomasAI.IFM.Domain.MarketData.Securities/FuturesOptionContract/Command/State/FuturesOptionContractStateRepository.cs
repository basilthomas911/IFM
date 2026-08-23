using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Model;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.State;

/// <summary>
/// Provides functionality for managing the state of futures option contracts, including loading and saving state
/// asynchronously.
/// </summary>
/// <remarks>This repository is designed to work with event-sourced actors and leverages snapshots for efficient
/// state management. It supports operations to load and save the state of a <see cref="FuturesOptionContractCommandState"/>
/// entity, ensuring consistency with the associated commands.</remarks>
/// <param name="aggregateFactory"></param>
/// <param name="dbEventSource"></param>
/// <param name="actorService"></param>
/// <param name="logger"></param>
public class FuturesOptionContractStateRepository(
    ICommandActorContext<FuturesOptionContractCommandActor> actorContext)
    : BaseEventSourceActorRepository(actorContext.StateFactory, actorContext.DbEventSource,
        actorContext.ActorService, actorContext.Logger), IEventSourceActorStateRepository<FuturesOptionContractCommandState>
{
    readonly IEventProjector<FuturesOptionContractCommandActor> _eventProjector =
        IsArgumentNull.Set(actorContext.EventProjector);
    /// <summary>
    /// Asynchronously loads the state of a futures option contract based on the specified command.
    /// </summary>
    /// <param name="command">The command used to identify and load the state. Cannot be <see langword="null"/>.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation. The result contains the <see
    /// cref="FuturesOptionContractCommandState"/> associated with the specified command.</returns>
    public async ValueTask<FuturesOptionContractCommandState> LoadStateAsync(ICommand command)
        => await LoadStateAsync(command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask<FuturesOptionContractCommandState> LoadStateAsync(ICommand command, CancellationToken cancellationToken)
        => command is AddFuturesOptionContractsCommand
            ? await LoadStateFromSnapshotAsync<FuturesOptionContractCommandState, FuturesOptionContractsAddedEvent>(command, cancellationToken).ConfigureAwait(false)
            : await LoadStateFromSnapshotAsync<FuturesOptionContractCommandState, FuturesOptionContractAddedEvent>(command, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Saves the specified state asynchronously, associating it with the provided command.
    /// </summary>
    /// <param name="context">The command actor context providing contextual information for the save operation. Must not be <see langword="null"/>.</param>
    /// <param name="state">The state object to be saved. Must not be <see langword="null"/>.</param>
    /// <param name="command">The command associated with the state. Must not be <see langword="null"/>.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous save operation.</returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesOptionContractCommandState state, ICommand command)
       => await SaveStateAsync(context, state, command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesOptionContractCommandState state, ICommand command, CancellationToken cancellationToken)
       => await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Updates the read model state by applying a collection of domain events to the futures option contract query state
    /// asynchronously.
    /// </summary>
    /// <remarks>This method processes each domain event in the provided collection and updates the futures option
    /// contract query state accordingly. It is typically called as part of the event sourcing workflow to keep the read
    /// model in sync with the latest events.</remarks>
    /// <param name="context">The command actor context that provides access to the actor's container and state required for denormalization.</param>
    /// <param name="domainEvents">A collection of domain events to be denormalized and applied to the read model state.</param>
    /// <returns>A task that represents the asynchronous denormalization operation.</returns>
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
        => _eventProjector.DomainEventsProjectionAsync(domainEvents);
}
