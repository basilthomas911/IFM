using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Event;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Event.Extensions;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;


using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Event.Actor;

/// <summary>Provides the DatabaseBackupEventActor implementation.</summary>
public class DatabaseBackupEventActor(
    IEventActorContext<DatabaseBackupEventActor> actorContext)
    : BaseEventActor<DatabaseBackupEventActor>(actorContext, actorContext.Logger)
{
    internal const string HandlerErrorLoggedKey = "IFM.DatabaseBackup.EventHandlerErrorLogged";
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IDatabaseBackupEventContext ActorContext =>
        IsArgumentNull.Named(Context as IDatabaseBackupEventContext, nameof(Context))!;

    public const string Actor = "DatabaseBackupEvent";

    /// <summary>Gets the supported service-event types.</summary>
    public static IReadOnlyCollection<Type> SupportedServiceEventTypes =>CachedSupportedServiceEventTypes.Value;
    /// <summary>Gets the supported service-event verbs.</summary>
    public static IReadOnlyCollection<string> SupportedVerbs =>CachedSupportedVerbs.Value;

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
            ["RunStatisticsCaptured"] = static message => message.AsEvent<DatabaseRecoveryRunStatisticsCapturedEvent>()!,
            ["ServiceReconciliation"] = static message => message.AsEvent<DatabaseBackupServiceReconciliationEvent>()!
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
            [typeof(DatabaseRecoveryRunStatisticsCapturedEvent)] = static (eventValue, context, logger) => ((DatabaseRecoveryRunStatisticsCapturedEvent)eventValue).ExecuteAsync(context, logger),
            [typeof(DatabaseBackupServiceReconciliationEvent)] = static (eventValue, context, logger) => ((DatabaseBackupServiceReconciliationEvent)eventValue).ExecuteAsync(context, logger)
        };

    protected override IEvent ParseMessage(IEventActorContext<DatabaseBackupEventActor> context, IActorMessage message)
        => ParseMappedEvent(context, message, _parseMap);

    protected override ValueTask ReceiveAsync(IEventActorContext<DatabaseBackupEventActor> context, IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(context);
        var receive = ResolveMappedEventHandler(@event, _receiveMap);
        return receive(@event, context, context.Logger);
    }

    protected override ValueTask OnExceptionAsync(IEventActorContext<DatabaseBackupEventActor> context, ActorThreadId threadId, IEvent @event, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        if (!ex.Data.Contains(HandlerErrorLoggedKey))
            global::TomasAI.IFM.Domain.SystemAdmin.ActorLifecycleDiagnosticLog.Write9(Context.Logger, ex, nameof(DatabaseBackupEventActor), nameof(OnExceptionAsync), @event?.EventName);
        return ValueTask.CompletedTask;
    }
private static readonly System.Lazy<IReadOnlyCollection<Type>> CachedSupportedServiceEventTypes = new(() => System.Array.AsReadOnly(_receiveMap.Keys.ToArray()), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
private static readonly System.Lazy<IReadOnlyCollection<string>> CachedSupportedVerbs = new(() => System.Array.AsReadOnly(_parseMap.Keys.ToArray()), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
}
