using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Event;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Event.Extensions;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;


using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Event.Actor;

/// <summary>Provides the DatabaseRecoveryEventActor implementation.</summary>
public class DatabaseRecoveryEventActor(
    IEventActorContext<DatabaseRecoveryEventActor> actorContext)
    : BaseEventActor<DatabaseRecoveryEventActor>(actorContext, actorContext.Logger)
{
    internal const string HandlerErrorLoggedKey = "IFM.DatabaseBackup.EventHandlerErrorLogged";
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IDatabaseRecoveryEventContext ActorContext =>
        IsArgumentNull.Named(Context as IDatabaseRecoveryEventContext, nameof(Context))!;

    public const string Actor = "DatabaseRecoveryEvent";

    /// <summary>Gets the supported service-event types.</summary>
    public static IReadOnlyCollection<Type> SupportedServiceEventTypes =>CachedSupportedServiceEventTypes.Value;
    /// <summary>Gets the supported service-event verbs.</summary>
    public static IReadOnlyCollection<string> SupportedVerbs =>CachedSupportedVerbs.Value;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
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
            ["ServiceReconciliation"] = static message => message.AsEvent<DatabaseBackupServiceReconciliationEvent>()!
        };

    static readonly IReadOnlyDictionary<Type, Func<IEvent, IEventActorContext<DatabaseRecoveryEventActor>, ILogger<DatabaseRecoveryEventActor>, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IEvent, IEventActorContext<DatabaseRecoveryEventActor>, ILogger<DatabaseRecoveryEventActor>, ValueTask>>
        {
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
            [typeof(DatabaseBackupServiceReconciliationEvent)] = static (eventValue, context, logger) => ((DatabaseBackupServiceReconciliationEvent)eventValue).ExecuteAsync(context, logger)
        };

    protected override IEvent ParseMessage(IEventActorContext<DatabaseRecoveryEventActor> context, IActorMessage message)
        => ParseMappedEvent(context, message, _parseMap);

    protected override ValueTask ReceiveAsync(IEventActorContext<DatabaseRecoveryEventActor> context, IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(context);
        var receive = ResolveMappedEventHandler(@event, _receiveMap);
        return receive(@event, context, context.Logger);
    }

    protected override ValueTask OnExceptionAsync(IEventActorContext<DatabaseRecoveryEventActor> context, ActorThreadId threadId, IEvent @event, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        if (!ex.Data.Contains(HandlerErrorLoggedKey))
            global::TomasAI.IFM.Domain.SystemAdmin.ActorLifecycleDiagnosticLog.Write9(Context.Logger, ex, nameof(DatabaseRecoveryEventActor), nameof(OnExceptionAsync), @event?.EventName);
        return ValueTask.CompletedTask;
    }
private static readonly System.Lazy<IReadOnlyCollection<Type>> CachedSupportedServiceEventTypes = new(() => System.Array.AsReadOnly(_receiveMap.Keys.ToArray()), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
private static readonly System.Lazy<IReadOnlyCollection<string>> CachedSupportedVerbs = new(() => System.Array.AsReadOnly(_parseMap.Keys.ToArray()), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
}
