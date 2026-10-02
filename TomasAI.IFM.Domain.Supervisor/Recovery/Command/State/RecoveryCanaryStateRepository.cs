using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Supervisor.Recovery.Command.Actor;
using TomasAI.IFM.Domain.Supervisor.Shared.Recovery;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Recovery.Command.State;

/// <summary>Commits canary source events before forwarding them to the event projector.</summary>
public sealed class RecoveryCanaryStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext eventSource,
    IActorService actorService,
    IEventProjector<RecoveryCanaryCommandActor> projector,
    ILogger<RecoveryCanaryStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      IEventSourceActorStateRepository<RecoveryCanaryCommandState>
{
    /// <summary>Rehydrates the command stream from its last recorded source event.</summary>
    public ValueTask<RecoveryCanaryCommandState> LoadStateAsync(ICommand command)
        => LoadStateAsync(command, CancellationToken.None);

    /// <summary>Rehydrates the command stream from its last recorded source event.</summary>
    public async ValueTask<RecoveryCanaryCommandState> LoadStateAsync(
        ICommand command, CancellationToken cancellationToken)
        => await LoadStateFromSnapshotAsync<RecoveryCanaryCommandState,
            RecoveryCanaryRecordedEvent>(command, cancellationToken).ConfigureAwait(false);

    /// <summary>Commits and projects the pending canary source event.</summary>
    public ValueTask SaveStateAsync(
        ICommandActorContext context, RecoveryCanaryCommandState state, ICommand command)
        => SaveStateAsync(context, state, command, CancellationToken.None);

    /// <summary>Commits and projects the pending canary source event.</summary>
    public async ValueTask SaveStateAsync(
        ICommandActorContext context, RecoveryCanaryCommandState state, ICommand command,
        CancellationToken cancellationToken)
        => await SaveStateAndDenormalizeEventsAsync(context, state, command,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    protected override ValueTask DenormalizeEventsAsync(
        ICommandActorContext context, DomainEventCollection domainEvents)
        => projector.DomainEventsProjectionAsync(domainEvents);
}
