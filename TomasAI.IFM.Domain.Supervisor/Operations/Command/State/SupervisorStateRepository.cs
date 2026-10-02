using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Supervisor.Operations.Command.Actor;
using TomasAI.IFM.Domain.Supervisor.Shared.Operations.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Operations.Command.State;

/// <summary>Commits Supervisor operation outcomes before their non-durable terminal projection.</summary>
public sealed class SupervisorStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext eventSource,
    IActorService actorService,
    IEventProjector<SupervisorCommandActor> projector,
    ILogger<SupervisorStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      IEventSourceActorStateRepository<SupervisorCommandState>
{
    /// <summary>Rehydrates the last operation outcome for this command stream.</summary>
    public ValueTask<SupervisorCommandState> LoadStateAsync(ICommand command)
        => LoadStateAsync(command, CancellationToken.None);

    /// <summary>Rehydrates the last operation outcome for this command stream.</summary>
    public async ValueTask<SupervisorCommandState> LoadStateAsync(
        ICommand command, CancellationToken cancellationToken)
        => await LoadStateFromSnapshotAsync<SupervisorCommandState,
            SupervisorActorOperationRecordedEvent>(command, cancellationToken).ConfigureAwait(false);

    /// <summary>Commits pending outcome events and submits them for projection.</summary>
    public ValueTask SaveStateAsync(
        ICommandActorContext context, SupervisorCommandState state, ICommand command)
        => SaveStateAsync(context, state, command, CancellationToken.None);

    /// <summary>Commits pending outcome events and submits them for projection.</summary>
    public async ValueTask SaveStateAsync(
        ICommandActorContext context, SupervisorCommandState state, ICommand command,
        CancellationToken cancellationToken)
        => await SaveStateAndDenormalizeEventsAsync(context, state, command,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    protected override ValueTask DenormalizeEventsAsync(
        ICommandActorContext context, DomainEventCollection domainEvents)
        => projector.DomainEventsProjectionAsync(domainEvents);
}
