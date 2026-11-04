using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Persistence;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events.Domain;
using TomasAI.IFM.Application.EventSourcing.Repository;
using TomasAI.IFM.Application.Storage.ScheduledTaskDb;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.State;
/// <summary>Loads administrative source events and commits changes before submitting projections.</summary>
public sealed class ScheduledTaskStateRepository(IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource,
    IActorService actorService, IEventProjector<ScheduledTaskCommandActor> projector, ILogger<ScheduledTaskStateRepository> logger, IScheduledTaskTransactionalProjectionWriter? atomicWriter = null, IPostgresEventTransaction? transactions = null)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger), IEventSourceActorStateRepository<ScheduledTaskCommandState>
{
    /// <inheritdoc />
    public ValueTask<ScheduledTaskCommandState> LoadStateAsync(ICommand command)
        => LoadStateAsync(command, CancellationToken.None);

    /// <summary>Restores the latest complete aggregate event and replays any subsequent events.</summary>
    public async ValueTask<ScheduledTaskCommandState> LoadStateAsync(ICommand command, CancellationToken cancellationToken)
        => await LoadStateFromLatestCheckpointAsync<ScheduledTaskCommandState>(
            command, CheckpointTypes, static (state, fact, snapshot) => state.ApplyCheckpointEvent(fact, snapshot),
            cancellationToken).ConfigureAwait(false);

    private static readonly Type[] CheckpointTypes =
    [
        typeof(ScheduledTaskCreatedEvent),
        typeof(ScheduledTaskScheduleChangedEvent),
        typeof(ScheduledTaskEnabledEvent),
        typeof(ScheduledTaskDisabledEvent),
        typeof(ScheduledTaskRemovedEvent),
        typeof(ScheduledTaskInstallationRecordedEvent),
        typeof(ScheduledTaskInstallationFailureRecordedEvent),
        typeof(ScheduledTaskRunAdmittedEvent),
        typeof(ScheduledTaskRunReleasedEvent),
    ];

    /// <inheritdoc />
    public ValueTask SaveStateAsync(ICommandActorContext context, ScheduledTaskCommandState state, ICommand command) => SaveStateAsync(context, state, command, CancellationToken.None);
    /// <inheritdoc />
    public async ValueTask SaveStateAsync(ICommandActorContext context, ScheduledTaskCommandState state, ICommand command, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(state);
        if (string.Equals(atomicWriter?.ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
        {
            var events = await ScheduledTaskAtomicPersistence.Save(state, command, EventSourceDb, atomicWriter,
                transactions ?? throw new InvalidOperationException("Scheduled-task atomic persistence requires the PostgreSQL event transaction service."), cancellationToken).ConfigureAwait(false);
            // Required writes have committed. Durable markers recover notification submission if this call fails.
            await projector.DomainEventsProjectionAsync(events).ConfigureAwait(false);
        }
        else await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken).ConfigureAwait(false);
    }
    /// <inheritdoc />
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents) => projector.DomainEventsProjectionAsync(domainEvents);
}
