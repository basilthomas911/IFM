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
    internal const string HandlerErrorLoggedKey = "IFM.DatabaseBackup.EventHandlerErrorLogged";
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

    static readonly IReadOnlyDictionary<Type, Func<IEvent, IEventActorContext<DatabaseBackupEventActor>, ILogger<DatabaseBackupEventActor>, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IEvent, IEventActorContext<DatabaseBackupEventActor>, ILogger<DatabaseBackupEventActor>, ValueTask>>
    {
        [typeof(DatabaseBackupServiceAcceptedEvent)] = static (eventValue, context, logger) => ((DatabaseBackupServiceAcceptedEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseBackupServiceRejectedEvent)] = static (eventValue, context, logger) => ((DatabaseBackupServiceRejectedEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseBackupServiceStartedEvent)] = static (eventValue, context, logger) => ((DatabaseBackupServiceStartedEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseBackupServiceProgressEvent)] = static (eventValue, context, logger) => ((DatabaseBackupServiceProgressEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseBackupBoundaryEstablishedEvent)] = static (eventValue, context, logger) => ((DatabaseBackupBoundaryEstablishedEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseBackupArtifactReplicaUpdatedEvent)] = static (eventValue, context, logger) => ((DatabaseBackupArtifactReplicaUpdatedEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseBackupVerificationCompletedEvent)] = static (eventValue, context, logger) => ((DatabaseBackupVerificationCompletedEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseBackupServiceErrorEvent)] = static (eventValue, context, logger) => ((DatabaseBackupServiceErrorEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseBackupServiceCompletedEvent)] = static (eventValue, context, logger) => ((DatabaseBackupServiceCompletedEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseBackupServiceFailedEvent)] = static (eventValue, context, logger) => ((DatabaseBackupServiceFailedEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseBackupServiceCancelledEvent)] = static (eventValue, context, logger) => ((DatabaseBackupServiceCancelledEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseRestoreServiceAcceptedEvent)] = static (eventValue, context, logger) => ((DatabaseRestoreServiceAcceptedEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseRestoreServiceRejectedEvent)] = static (eventValue, context, logger) => ((DatabaseRestoreServiceRejectedEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseRestoreServiceStartedEvent)] = static (eventValue, context, logger) => ((DatabaseRestoreServiceStartedEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseRestoreServiceProgressEvent)] = static (eventValue, context, logger) => ((DatabaseRestoreServiceProgressEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseRestoreValidationCompletedEvent)] = static (eventValue, context, logger) => ((DatabaseRestoreValidationCompletedEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseRestoreReadyForCutoverEvent)] = static (eventValue, context, logger) => ((DatabaseRestoreReadyForCutoverEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseRestoreDrillCompletedEvent)] = static (eventValue, context, logger) => ((DatabaseRestoreDrillCompletedEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseRestoreServiceErrorEvent)] = static (eventValue, context, logger) => ((DatabaseRestoreServiceErrorEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseRestoreServiceCompletedEvent)] = static (eventValue, context, logger) => ((DatabaseRestoreServiceCompletedEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseRestoreServiceFailedEvent)] = static (eventValue, context, logger) => ((DatabaseRestoreServiceFailedEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseRestoreServiceCancelledEvent)] = static (eventValue, context, logger) => ((DatabaseRestoreServiceCancelledEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseRecoveryRunStatisticsCapturedEvent)] = static (eventValue, context, logger) => ((DatabaseRecoveryRunStatisticsCapturedEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseBackupPolicyAppliedEvent)] = static (eventValue, context, logger) => ((DatabaseBackupPolicyAppliedEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseBackupPolicyRejectedEvent)] = static (eventValue, context, logger) => ((DatabaseBackupPolicyRejectedEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseRetentionPlanCreatedEvent)] = static (eventValue, context, logger) => ((DatabaseRetentionPlanCreatedEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseRetentionExecutionCompletedEvent)] = static (eventValue, context, logger) => ((DatabaseRetentionExecutionCompletedEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseRetentionExecutionFailedEvent)] = static (eventValue, context, logger) => ((DatabaseRetentionExecutionFailedEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseBackupServiceReconciliationEvent)] = static (eventValue, context, logger) => ((DatabaseBackupServiceReconciliationEvent)eventValue).ExecuteAsync(context, logger),
        [typeof(DatabaseBackupServiceCapabilityChangedEvent)] = static (eventValue, context, logger) => ((DatabaseBackupServiceCapabilityChangedEvent)eventValue).ExecuteAsync(context, logger)
    };

    protected override IEvent ParseMessage(IEventActorContext<DatabaseBackupEventActor> context, IActorMessage message)
        => ParseMappedEvent(context, message, _parseMap);

    static IEvent ParseTyped<TEvent>(IActorMessage message) where TEvent : class, IEvent
        => message.AsEvent<TEvent>() ?? throw new InvalidOperationException($"Unable to deserialize {typeof(TEvent).Name}.");

    protected override ValueTask ReceiveAsync(IEventActorContext<DatabaseBackupEventActor> context, IEvent @event)
    {
        var receive = ResolveMappedEventHandler(@event, _receiveMap);
        return receive(@event, context, context.Logger);
    }

    protected override ValueTask OnExceptionAsync(IEventActorContext<DatabaseBackupEventActor> context, ActorThreadId threadId, IEvent @event, Exception exception)
    {
        if (!exception.Data.Contains(HandlerErrorLoggedKey))
            Context.Logger.LogError(exception, "DatabaseBackup service event {EventName} failed.", @event?.EventName);
        return ValueTask.CompletedTask;
    }
}
