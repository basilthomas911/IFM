using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Event;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Event.Extensions;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;


using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Event.Actor;

/// <summary>Provides the DatabaseBackupRetentionEventActor implementation.</summary>
public class DatabaseBackupRetentionEventActor(
    IEventActorContext<DatabaseBackupRetentionEventActor> actorContext)
    : BaseEventActor<DatabaseBackupRetentionEventActor>(actorContext, actorContext.Logger)
{
    internal const string HandlerErrorLoggedKey = "IFM.DatabaseBackup.EventHandlerErrorLogged";
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IDatabaseBackupRetentionEventContext ActorContext =>
        IsArgumentNull.Named(Context as IDatabaseBackupRetentionEventContext, nameof(Context))!;

    public const string Actor = "DatabaseBackupRetentionEvent";

    /// <summary>Gets the supported service-event types.</summary>
    public static IReadOnlyCollection<Type> SupportedServiceEventTypes =>CachedSupportedServiceEventTypes.Value;
    /// <summary>Gets the supported service-event verbs.</summary>
    public static IReadOnlyCollection<string> SupportedVerbs =>CachedSupportedVerbs.Value;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            ["RunStatisticsCaptured"] = static message => message.AsEvent<DatabaseRecoveryRunStatisticsCapturedEvent>()!,
            ["RetentionPlanCreated"] = static message => message.AsEvent<DatabaseRetentionPlanCreatedEvent>()!,
            ["RetentionExecutionCompleted"] = static message => message.AsEvent<DatabaseRetentionExecutionCompletedEvent>()!,
            ["RetentionExecutionFailed"] = static message => message.AsEvent<DatabaseRetentionExecutionFailedEvent>()!,
            ["ServiceReconciliation"] = static message => message.AsEvent<DatabaseBackupServiceReconciliationEvent>()!
        };

    static readonly IReadOnlyDictionary<Type, Func<IEvent, IEventActorContext<DatabaseBackupRetentionEventActor>, ILogger<DatabaseBackupRetentionEventActor>, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IEvent, IEventActorContext<DatabaseBackupRetentionEventActor>, ILogger<DatabaseBackupRetentionEventActor>, ValueTask>>
        {
            [typeof(DatabaseRecoveryRunStatisticsCapturedEvent)] = static (eventValue, context, logger) => ((DatabaseRecoveryRunStatisticsCapturedEvent)eventValue).ExecuteAsync(context, logger),
            [typeof(DatabaseRetentionPlanCreatedEvent)] = static (eventValue, context, logger) => ((DatabaseRetentionPlanCreatedEvent)eventValue).ExecuteAsync(context, logger),
            [typeof(DatabaseRetentionExecutionCompletedEvent)] = static (eventValue, context, logger) => ((DatabaseRetentionExecutionCompletedEvent)eventValue).ExecuteAsync(context, logger),
            [typeof(DatabaseRetentionExecutionFailedEvent)] = static (eventValue, context, logger) => ((DatabaseRetentionExecutionFailedEvent)eventValue).ExecuteAsync(context, logger),
            [typeof(DatabaseBackupServiceReconciliationEvent)] = static (eventValue, context, logger) => ((DatabaseBackupServiceReconciliationEvent)eventValue).ExecuteAsync(context, logger)
        };

    protected override IEvent ParseMessage(IEventActorContext<DatabaseBackupRetentionEventActor> context, IActorMessage message)
        => ParseMappedEvent(context, message, _parseMap);

    protected override ValueTask ReceiveAsync(IEventActorContext<DatabaseBackupRetentionEventActor> context, IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(context);
        var receive = ResolveMappedEventHandler(@event, _receiveMap);
        return receive(@event, context, context.Logger);
    }

    protected override ValueTask OnExceptionAsync(IEventActorContext<DatabaseBackupRetentionEventActor> context, ActorThreadId threadId, IEvent @event, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        if (!ex.Data.Contains(HandlerErrorLoggedKey))
            global::TomasAI.IFM.Domain.SystemAdmin.ActorLifecycleDiagnosticLog.Write9(Context.Logger, ex, nameof(DatabaseBackupRetentionEventActor), nameof(OnExceptionAsync), @event?.EventName);
        return ValueTask.CompletedTask;
    }
private static readonly System.Lazy<IReadOnlyCollection<Type>> CachedSupportedServiceEventTypes = new(() => System.Array.AsReadOnly(_receiveMap.Keys.ToArray()), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
private static readonly System.Lazy<IReadOnlyCollection<string>> CachedSupportedVerbs = new(() => System.Array.AsReadOnly(_parseMap.Keys.ToArray()), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
}
