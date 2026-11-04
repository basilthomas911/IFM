using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Projection;
using TomasAI.IFM.Application.EventSourcing.Repository;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Execution;

using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Persistence;
/// <summary>Shared transaction mechanics; each registered repository has a distinct concern state and actor.</summary>
public abstract class DatabaseOperationStateRepository<TState, TActor>(
    IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource, IActorService actorService,
    IEventProjector<TActor> eventProjector, IDatabaseBackupExecutionOutbox executionOutbox, ILogger logger,
    IDbContextFactory? databases = null, IPostgresEventTransaction? transactions = null)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger), IEventSourceActorStateRepository<TState>
    where TState : DatabaseOperationCommandState<TState>
    where TActor : ICommandActor<TActor>
{
    readonly IEventSourceActorStateFactory _stateFactory = stateFactory;
    readonly IDatabaseBackupExecutionOutbox _executionOutbox = executionOutbox;
    readonly IEventProjector<TActor> _eventProjector = eventProjector;

    public async ValueTask<TState> LoadStateAsync(ICommand command)
        => await LoadStateAsync(command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask<TState> LoadStateAsync(ICommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var state = (TState)_stateFactory.CreateState<TState>();
        var legacyId = $"Command.DatabaseBackupCommand.{command.Subject.EntityId}";
        if (!string.Equals(command.StreamId, legacyId, StringComparison.Ordinal))
        {
            var legacyStream = await GetStreamId(legacyId, cancellationToken).ConfigureAwait(false);
            await EventSourceDb.MapReduceActorEventStreamFromCreationAsync<TState>(legacyStream,
                rows => state.ReplayEvents(rows.Select(row => row.ToDomainEvent()).OfType<IEvent>()),
                cancellationToken).ConfigureAwait(false);
        }
        var stream = await GetStreamId(command.StreamId, cancellationToken).ConfigureAwait(false);
        long version = 0;
        await EventSourceDb.MapReduceActorEventStreamFromCreationAsync<TState>(stream,
            rows => state.ReplayEvents(rows.Select(row => { version = row.StreamVersion; return row; })),
            cancellationToken).ConfigureAwait(false);
        state.RestoreCommittedStreamVersion(version);
        state.Id = command.Subject.ThreadId;
        return state;
    }

    public async ValueTask SaveStateAsync(ICommandActorContext context, TState state, ICommand command)
        => await SaveStateAsync(context, state, command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask SaveStateAsync(ICommandActorContext context, TState state, ICommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (databases?.SystemAdminDb is not { ProviderName: "System.Data.Postgres" } target)
        {
            await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken).ConfigureAwait(false);
            return;
        }
        if (state.Events.Count == 0) return;
        if (!string.Equals(EventSourceDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal)
            || PostgresDatabaseIdentity.Parse(EventSourceDb.ConnectionString) != PostgresDatabaseIdentity.Parse(target.ConnectionString))
            throw new InvalidOperationException("Backup source and read rows must share one PostgreSQL database/security identity.");
        var owner = transactions ?? throw new InvalidOperationException("Backup persistence requires the PostgreSQL transaction service.");
        var pending = new DomainEventCollection(state.Events);
        await owner.ExecuteOnceAsync(async (transaction, token) =>
        {
            await transaction.ReserveCommandAsync(command, token).ConfigureAwait(false);
            var version = state.CommittedStreamVersion;
            foreach (var source in pending)
            {
                if (source is not DatabaseBackupEventContract backup)
                    throw new InvalidDataException("Backup commands require complete backup source events.");
                await transaction.AppendAsync(command.StreamId, command.CommandId, backup, version++, token).ConfigureAwait(false);
                await target.ApplyDatabaseBackupSnapshotAsync(transaction, "DatabaseBackupEventProjector", DatabaseBackupProjectionFactory.Create(backup), token).ConfigureAwait(false);
                if (DatabaseBackupExecutionEvents.ToExecutionEvent(backup) is not null)
                    await DatabaseBackupExecutionIntentRepository.EnqueueAsync(target, transaction, backup, token).ConfigureAwait(false);
            }
            return true;
        }, cancellationToken).ConfigureAwait(false);
        state.AcceptChanges();
        await DenormalizeEventsAsync(context, pending).ConfigureAwait(false);
    }

    protected override async ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
    {
        await _eventProjector.DomainEventsProjectionAsync(domainEvents).ConfigureAwait(false);
        foreach (var domainEvent in domainEvents.OfType<DatabaseBackupEventContract>())
        {
            var executionEvent = DatabaseBackupExecutionEvents.ToExecutionEvent(domainEvent);
            if (executionEvent is null) continue;
            if (databases?.SystemAdminDb is { ProviderName: "System.Data.Postgres" })
            {
                await DatabaseBackupExecutionEvents.SendConcreteEventAsync(context, executionEvent).ConfigureAwait(false);
                await new DatabaseBackupExecutionIntentRepository(databases.SystemAdminDb)
                    .MarkPublishedAsync(domainEvent).ConfigureAwait(false);
            }
            else
            {
                await _executionOutbox.EnqueueAsync(executionEvent).ConfigureAwait(false);
                await DatabaseBackupExecutionEvents.SendConcreteEventAsync(context, executionEvent).ConfigureAwait(false);
                await _executionOutbox.MarkPublishedAsync(executionEvent.Id).ConfigureAwait(false);
            }
        }
    }

}
