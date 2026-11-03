using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Projection;
using TomasAI.IFM.Application.EventSourcing.Repository;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Execution;

using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Persistence;
public static class DatabaseBackupExecutionEvents
{
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3011", Justification = "Reflection selects only this class-owned constant named generic dispatch helper; no external member name or visibility is accepted.")]
    static readonly MethodInfo SendConcreteEventMethod = typeof(DatabaseBackupExecutionEvents)
        .GetMethod(nameof(SendConcreteEventCoreAsync), BindingFlags.NonPublic | BindingFlags.Static)!;
    static readonly ConcurrentDictionary<Type, MethodInfo> ConcreteSendMethods = [];
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
