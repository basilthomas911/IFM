using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Command.State;

/// <summary>Loads the latest account snapshot event and commits account state changes.</summary>
public sealed class BrokerAccountStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext eventSource,
    IActorService actorService,
    IEventProjector<BrokerAccountCommandActor> eventProjector,
    ILogger<BrokerAccountStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      IEventSourceActorStateRepository<BrokerAccountCommandState>
{
    /// <inheritdoc />
    public ValueTask<BrokerAccountCommandState> LoadStateAsync(ICommand command) =>
        LoadStateAsync(command, CancellationToken.None);

    /// <inheritdoc />
    public async ValueTask<BrokerAccountCommandState> LoadStateAsync(ICommand command,
        CancellationToken cancellationToken)
    {
        var state = await LoadStateFromSnapshotAsync<BrokerAccountCommandState, BrokerAccountChangedEvent>(
            command, cancellationToken).ConfigureAwait(false);
        return state;
    }

    /// <inheritdoc />
    public ValueTask SaveStateAsync(ICommandActorContext context,
        BrokerAccountCommandState state, ICommand command) =>
        SaveStateAsync(context, state, command, CancellationToken.None);

    /// <inheritdoc />
    public async ValueTask SaveStateAsync(ICommandActorContext context,
        BrokerAccountCommandState state, ICommand command, CancellationToken cancellationToken)
    {
        await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context,
        DomainEventCollection domainEvents) => eventProjector.DomainEventsProjectionAsync(domainEvents);
}
