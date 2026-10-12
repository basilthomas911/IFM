using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Execution;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;

public sealed class DatabaseBackupStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext eventSource,
    IActorService actorService,
    IEventProjector<DatabaseBackupCommandActor> eventProjector,
    IDatabaseBackupExecutionOutbox executionOutbox,
    ILogger<DatabaseBackupStateRepository> logger,
    IDbContextFactory? databases = null, IPostgresEventTransaction? transactions = null)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      IEventSourceActorStateRepository<DatabaseBackupCommandState>
{
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3011", Justification = "Reflection selects only this class-owned constant named generic dispatch helper; no external member name or visibility is accepted.")]
    static readonly MethodInfo SendConcreteEventMethod = typeof(DatabaseBackupStateRepository)
        .GetMethod(nameof(SendConcreteEventCoreAsync), BindingFlags.NonPublic | BindingFlags.Static)!;
    static readonly ConcurrentDictionary<Type, MethodInfo> ConcreteSendMethods = [];
    readonly IDatabaseBackupExecutionOutbox _executionOutbox = executionOutbox;
    readonly IEventProjector<DatabaseBackupCommandActor> _eventProjector = eventProjector;

    public async ValueTask<DatabaseBackupCommandState> LoadStateAsync(ICommand command)
        => await LoadStateAsync(command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask<DatabaseBackupCommandState> LoadStateAsync(ICommand command, CancellationToken cancellationToken)
        => await LoadStateAsync<DatabaseBackupCommandState>(command, cancellationToken).ConfigureAwait(false);

    public async ValueTask SaveStateAsync(ICommandActorContext context, DatabaseBackupCommandState state, ICommand command)
        => await SaveStateAsync(context, state, command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask SaveStateAsync(ICommandActorContext context, DatabaseBackupCommandState state, ICommand command, CancellationToken cancellationToken)
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
                await target.ApplyDatabaseBackupEventAsync(transaction, "DatabaseBackupEventProjector", backup, token).ConfigureAwait(false);
                if (ToExecutionEvent(backup) is not null)
                    await DatabaseBackupExecutionIntentStore.EnqueueAsync(transaction, backup, token).ConfigureAwait(false);
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
            var executionEvent = ToExecutionEvent(domainEvent);
            if (executionEvent is null) continue;
            if (databases?.SystemAdminDb is { ProviderName: "System.Data.Postgres" })
            {
                await SendConcreteEventAsync(context, executionEvent).ConfigureAwait(false);
                await new DatabaseBackupExecutionIntentStore(transactions ?? throw new InvalidOperationException("Backup outbox requires PostgreSQL transactions."))
                    .MarkPublishedAsync(domainEvent).ConfigureAwait(false);
            }
            else
            {
                await _executionOutbox.EnqueueAsync(executionEvent).ConfigureAwait(false);
                await SendConcreteEventAsync(context, executionEvent).ConfigureAwait(false);
                await _executionOutbox.MarkPublishedAsync(executionEvent.Id).ConfigureAwait(false);
            }
        }
    }

    internal static ValueTask SendConcreteEventAsync(
        ICommandActorContext context,
        DatabaseBackupEventContract domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domainEvent);
        var method = ConcreteSendMethods.GetOrAdd(
            domainEvent.GetType(),
            static eventType => SendConcreteEventMethod.MakeGenericMethod(eventType));
        return (ValueTask)method.Invoke(null, [context, domainEvent])!;
    }

    static ValueTask SendConcreteEventCoreAsync<TEvent>(
        ICommandActorContext context,
        DatabaseBackupEventContract domainEvent)
        where TEvent : DatabaseBackupEventContract,
            IEvent<Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>
        => context.SendAsync<TEvent, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(
            (TEvent)domainEvent);

    public static DatabaseBackupEventContract? ToExecutionEvent(DatabaseBackupEventContract domainEvent)
        
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return domainEvent switch
        {
            DatabaseBackupExecutionRequestedDomainEvent => Copy<DatabaseBackupExecutionRequestedEvent>(domainEvent),
            DatabaseRestoreExecutionRequestedDomainEvent => Copy<DatabaseRestoreExecutionRequestedEvent>(domainEvent),
            DatabaseRestoreDrillExecutionRequestedDomainEvent => Copy<DatabaseRestoreDrillRequestedEvent>(domainEvent),
            DatabaseCutoverExecutionRequestedDomainEvent => Copy<DatabaseCutoverExecutionRequestedEvent>(domainEvent),
            DatabaseRetentionRequestedDomainEvent => Copy<DatabaseRetentionEvaluationRequestedEvent>(domainEvent),
            DatabaseRetentionExecutionRequestedDomainEvent => Copy<DatabaseRetentionExecutionRequestedEvent>(domainEvent),
            DatabaseBackupPolicyEnforcedEvent => Copy<DatabaseBackupPolicyActivatedEvent>(domainEvent),
            DatabaseOperationCancelledEvent when domainEvent.Source.OperationKind == Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationKind.Backup
                => Copy<DatabaseBackupCancellationRequestedEvent>(domainEvent),
            DatabaseOperationCancelledEvent when domainEvent.Source.OperationKind is Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationKind.Restore or Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationKind.RestoreDrill
                => Copy<DatabaseRestoreCancellationRequestedEvent>(domainEvent),
            _ => null
        };
    }

    static TEvent Copy<TEvent>(DatabaseBackupEventContract source) where TEvent : DatabaseBackupEventContract, new()
    {
        var template = new TEvent();
        return (template with
        {
            Subject = new global::TomasAI.IFM.Shared.EventModelActor.ActorSubject(global::TomasAI.IFM.Shared.EventModelActor.ActorType.Event, "DatabaseBackupExecution", template.Verb, source.EntityId.Format()),
            Id = source.Id,
            EntityId = source.EntityId,
            EventId = source.EventId,
            CommandId = source.CommandId,
            AggregateId = source.AggregateId,
            EventSource = source.EventSource,
            ReceivedOn = source.ReceivedOn,
            Source = source.Source,
            Request = source.Request,
            SafeDiagnosticReference = source.SafeDiagnosticReference,
            RestorePointId = source.RestorePointId,
            FreshTarget = source.FreshTarget,
            Policy = source.Policy,
            RequiredDestinations = source.RequiredDestinations,
            ValidationRevision = source.ValidationRevision,
            RetentionPlanId = source.RetentionPlanId,
            RetentionPlanRevision = source.RetentionPlanRevision,
            RestoreClass = source.RestoreClass,
            EvaluationBoundaryUtc = source.EvaluationBoundaryUtc,
            PolicyId = source.PolicyId,
            ManifestRevision = source.ManifestRevision,
            BackupLineage = source.BackupLineage
        });
    }
}
