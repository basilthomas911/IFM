using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Event;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Event.Translation;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Event.Extensions;

using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Event.Actor;

/// <summary>Provides the DatabaseBackupEventActor implementation.</summary>
public class DatabaseBackupEventActor(
    IEventActorContext<DatabaseBackupEventActor> actorContext)
    : BaseEventActor<DatabaseBackupEventActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IDatabaseBackupEventContext ActorContext =>
        IsArgumentNull.Set(Context as IDatabaseBackupEventContext, nameof(Context))!;

    public const string Actor = "DatabaseBackupEvent";

    /// <summary>Gets the supported service-event types.</summary>
    public static IReadOnlyCollection<Type> SupportedServiceEventTypes => _receiveMap.Keys.ToArray();
    /// <summary>Gets the supported service-event verbs.</summary>
    public static IReadOnlyCollection<string> SupportedVerbs => _parseMap.Keys.ToArray();

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
    {
        ["BackupAccepted"] = static message => message.AsEvent<DatabaseBackupServiceAcceptedEvent>()!,
        ["BackupRejected"] = static message => message.AsEvent<DatabaseBackupServiceRejectedEvent>()!,
        ["BackupStarted"] = static message => message.AsEvent<DatabaseBackupServiceStartedEvent>()!,
        ["BackupProgress"] = static message => message.AsEvent<DatabaseBackupServiceProgressEvent>()!,
        ["BackupBoundaryEstablished"] = static message => message.AsEvent<DatabaseBackupBoundaryEstablishedEvent>()!,
        ["BackupArtifactReplicaUpdated"] = static message => message.AsEvent<DatabaseBackupArtifactReplicaUpdatedEvent>()!,
        ["BackupVerificationCompleted"] = static message => message.AsEvent<DatabaseBackupVerificationCompletedEvent>()!,
        ["BackupError"] = static message => message.AsEvent<DatabaseBackupServiceErrorEvent>()!,
        ["BackupCompleted"] = static message => message.AsEvent<DatabaseBackupServiceCompletedEvent>()!,
        ["BackupFailed"] = static message => message.AsEvent<DatabaseBackupServiceFailedEvent>()!,
        ["BackupCancelled"] = static message => message.AsEvent<DatabaseBackupServiceCancelledEvent>()!,
        ["RestoreAccepted"] = static message => message.AsEvent<DatabaseRestoreServiceAcceptedEvent>()!,
        ["RestoreRejected"] = static message => message.AsEvent<DatabaseRestoreServiceRejectedEvent>()!,
        ["RestoreStarted"] = static message => message.AsEvent<DatabaseRestoreServiceStartedEvent>()!,
        ["RestoreProgress"] = static message => message.AsEvent<DatabaseRestoreServiceProgressEvent>()!,
        ["RestoreValidationCompleted"] = static message => message.AsEvent<DatabaseRestoreValidationCompletedEvent>()!,
        ["RestoreReadyForCutover"] = static message => message.AsEvent<DatabaseRestoreReadyForCutoverEvent>()!,
        ["RestoreDrillCompleted"] = static message => message.AsEvent<DatabaseRestoreDrillCompletedEvent>()!,
        ["RestoreError"] = static message => message.AsEvent<DatabaseRestoreServiceErrorEvent>()!,
        ["RestoreCompleted"] = static message => message.AsEvent<DatabaseRestoreServiceCompletedEvent>()!,
        ["RestoreFailed"] = static message => message.AsEvent<DatabaseRestoreServiceFailedEvent>()!,
        ["RestoreCancelled"] = static message => message.AsEvent<DatabaseRestoreServiceCancelledEvent>()!,
        ["RunStatisticsCaptured"] = static message => message.AsEvent<DatabaseRecoveryRunStatisticsCapturedEvent>()!,
        ["PolicyApplied"] = static message => message.AsEvent<DatabaseBackupPolicyAppliedEvent>()!,
        ["PolicyRejected"] = static message => message.AsEvent<DatabaseBackupPolicyRejectedEvent>()!,
        ["RetentionPlanCreated"] = static message => message.AsEvent<DatabaseRetentionPlanCreatedEvent>()!,
        ["RetentionExecutionCompleted"] = static message => message.AsEvent<DatabaseRetentionExecutionCompletedEvent>()!,
        ["RetentionExecutionFailed"] = static message => message.AsEvent<DatabaseRetentionExecutionFailedEvent>()!,
        ["ServiceReconciliation"] = static message => message.AsEvent<DatabaseBackupServiceReconciliationEvent>()!,
        ["ServiceCapabilityChanged"] = static message => message.AsEvent<DatabaseBackupServiceCapabilityChangedEvent>()!
    };

    static readonly IReadOnlyDictionary<Type, Func<IEvent, IEventActorContext<DatabaseBackupEventActor>, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IEvent, IEventActorContext<DatabaseBackupEventActor>, ValueTask>>
    {
        [typeof(DatabaseBackupServiceAcceptedEvent)] = static (eventValue, context) => ((DatabaseBackupServiceAcceptedEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseBackupServiceRejectedEvent)] = static (eventValue, context) => ((DatabaseBackupServiceRejectedEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseBackupServiceStartedEvent)] = static (eventValue, context) => ((DatabaseBackupServiceStartedEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseBackupServiceProgressEvent)] = static (eventValue, context) => ((DatabaseBackupServiceProgressEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseBackupBoundaryEstablishedEvent)] = static (eventValue, context) => ((DatabaseBackupBoundaryEstablishedEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseBackupArtifactReplicaUpdatedEvent)] = static (eventValue, context) => ((DatabaseBackupArtifactReplicaUpdatedEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseBackupVerificationCompletedEvent)] = static (eventValue, context) => ((DatabaseBackupVerificationCompletedEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseBackupServiceErrorEvent)] = static (eventValue, context) => ((DatabaseBackupServiceErrorEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseBackupServiceCompletedEvent)] = static (eventValue, context) => ((DatabaseBackupServiceCompletedEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseBackupServiceFailedEvent)] = static (eventValue, context) => ((DatabaseBackupServiceFailedEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseBackupServiceCancelledEvent)] = static (eventValue, context) => ((DatabaseBackupServiceCancelledEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseRestoreServiceAcceptedEvent)] = static (eventValue, context) => ((DatabaseRestoreServiceAcceptedEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseRestoreServiceRejectedEvent)] = static (eventValue, context) => ((DatabaseRestoreServiceRejectedEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseRestoreServiceStartedEvent)] = static (eventValue, context) => ((DatabaseRestoreServiceStartedEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseRestoreServiceProgressEvent)] = static (eventValue, context) => ((DatabaseRestoreServiceProgressEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseRestoreValidationCompletedEvent)] = static (eventValue, context) => ((DatabaseRestoreValidationCompletedEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseRestoreReadyForCutoverEvent)] = static (eventValue, context) => ((DatabaseRestoreReadyForCutoverEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseRestoreDrillCompletedEvent)] = static (eventValue, context) => ((DatabaseRestoreDrillCompletedEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseRestoreServiceErrorEvent)] = static (eventValue, context) => ((DatabaseRestoreServiceErrorEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseRestoreServiceCompletedEvent)] = static (eventValue, context) => ((DatabaseRestoreServiceCompletedEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseRestoreServiceFailedEvent)] = static (eventValue, context) => ((DatabaseRestoreServiceFailedEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseRestoreServiceCancelledEvent)] = static (eventValue, context) => ((DatabaseRestoreServiceCancelledEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseRecoveryRunStatisticsCapturedEvent)] = static (eventValue, context) => ((DatabaseRecoveryRunStatisticsCapturedEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseBackupPolicyAppliedEvent)] = static (eventValue, context) => ((DatabaseBackupPolicyAppliedEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseBackupPolicyRejectedEvent)] = static (eventValue, context) => ((DatabaseBackupPolicyRejectedEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseRetentionPlanCreatedEvent)] = static (eventValue, context) => ((DatabaseRetentionPlanCreatedEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseRetentionExecutionCompletedEvent)] = static (eventValue, context) => ((DatabaseRetentionExecutionCompletedEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseRetentionExecutionFailedEvent)] = static (eventValue, context) => ((DatabaseRetentionExecutionFailedEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseBackupServiceReconciliationEvent)] = static (eventValue, context) => ((DatabaseBackupServiceReconciliationEvent)eventValue).ExecuteAsync(context),
        [typeof(DatabaseBackupServiceCapabilityChangedEvent)] = static (eventValue, context) => ((DatabaseBackupServiceCapabilityChangedEvent)eventValue).ExecuteAsync(context)
    };

    protected override IEvent ParseMessage(IEventActorContext<DatabaseBackupEventActor> context, IActorMessage message)
        => ParseMappedEvent(context, message, _parseMap);

    static IEvent ParseTyped<TEvent>(IActorMessage message) where TEvent : class, IEvent
        => message.AsEvent<TEvent>() ?? throw new InvalidOperationException($"Unable to deserialize {typeof(TEvent).Name}.");

    protected override ValueTask ReceiveAsync(IEventActorContext<DatabaseBackupEventActor> context, IEvent @event)
    {
        var receive = ResolveMappedEventHandler(@event, _receiveMap);
        return receive(@event, context);
    }

    protected override ValueTask OnExceptionAsync(IEventActorContext<DatabaseBackupEventActor> context, ActorThreadId threadId, IEvent @event, Exception exception)
    {
        Context.Logger.LogError(exception, "DatabaseBackup service event {EventName} failed.", @event?.EventName);
        return ValueTask.CompletedTask;
    }
}
