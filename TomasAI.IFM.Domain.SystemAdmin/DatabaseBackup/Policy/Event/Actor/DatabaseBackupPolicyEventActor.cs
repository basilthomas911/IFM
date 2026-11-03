using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Event;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Event.Extensions;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;


using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Event.Actor;

/// <summary>Provides the DatabaseBackupPolicyEventActor implementation.</summary>
public class DatabaseBackupPolicyEventActor(
    IEventActorContext<DatabaseBackupPolicyEventActor> actorContext)
    : BaseEventActor<DatabaseBackupPolicyEventActor>(actorContext, actorContext.Logger)
{
    internal const string HandlerErrorLoggedKey = "IFM.DatabaseBackup.EventHandlerErrorLogged";
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IDatabaseBackupPolicyEventContext ActorContext =>
        IsArgumentNull.Named(Context as IDatabaseBackupPolicyEventContext, nameof(Context))!;

    public const string Actor = "DatabaseBackupPolicyEvent";

    /// <summary>Gets the supported service-event types.</summary>
    public static IReadOnlyCollection<Type> SupportedServiceEventTypes =>CachedSupportedServiceEventTypes.Value;
    /// <summary>Gets the supported service-event verbs.</summary>
    public static IReadOnlyCollection<string> SupportedVerbs =>CachedSupportedVerbs.Value;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            ["RunStatisticsCaptured"] = static message => message.AsEvent<DatabaseRecoveryRunStatisticsCapturedEvent>()!,
            ["PolicyApplied"] = static message => message.AsEvent<DatabaseBackupPolicyAppliedEvent>()!,
            ["PolicyRejected"] = static message => message.AsEvent<DatabaseBackupPolicyRejectedEvent>()!,
            ["ServiceReconciliation"] = static message => message.AsEvent<DatabaseBackupServiceReconciliationEvent>()!
        };

    static readonly IReadOnlyDictionary<Type, Func<IEvent, IEventActorContext<DatabaseBackupPolicyEventActor>, ILogger<DatabaseBackupPolicyEventActor>, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IEvent, IEventActorContext<DatabaseBackupPolicyEventActor>, ILogger<DatabaseBackupPolicyEventActor>, ValueTask>>
        {
            [typeof(DatabaseRecoveryRunStatisticsCapturedEvent)] = static (eventValue, context, logger) => ((DatabaseRecoveryRunStatisticsCapturedEvent)eventValue).ExecuteAsync(context, logger),
            [typeof(DatabaseBackupPolicyAppliedEvent)] = static (eventValue, context, logger) => ((DatabaseBackupPolicyAppliedEvent)eventValue).ExecuteAsync(context, logger),
            [typeof(DatabaseBackupPolicyRejectedEvent)] = static (eventValue, context, logger) => ((DatabaseBackupPolicyRejectedEvent)eventValue).ExecuteAsync(context, logger),
            [typeof(DatabaseBackupServiceReconciliationEvent)] = static (eventValue, context, logger) => ((DatabaseBackupServiceReconciliationEvent)eventValue).ExecuteAsync(context, logger)
        };

    protected override IEvent ParseMessage(IEventActorContext<DatabaseBackupPolicyEventActor> context, IActorMessage message)
        => ParseMappedEvent(context, message, _parseMap);

    protected override ValueTask ReceiveAsync(IEventActorContext<DatabaseBackupPolicyEventActor> context, IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(context);
        var receive = ResolveMappedEventHandler(@event, _receiveMap);
        return receive(@event, context, context.Logger);
    }

    protected override ValueTask OnExceptionAsync(IEventActorContext<DatabaseBackupPolicyEventActor> context, ActorThreadId threadId, IEvent @event, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        if (!ex.Data.Contains(HandlerErrorLoggedKey))
            global::TomasAI.IFM.Domain.SystemAdmin.ActorLifecycleDiagnosticLog.Write9(Context.Logger, ex, nameof(DatabaseBackupPolicyEventActor), nameof(OnExceptionAsync), @event?.EventName);
        return ValueTask.CompletedTask;
    }
private static readonly System.Lazy<IReadOnlyCollection<Type>> CachedSupportedServiceEventTypes = new(() => System.Array.AsReadOnly(_receiveMap.Keys.ToArray()), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
private static readonly System.Lazy<IReadOnlyCollection<string>> CachedSupportedVerbs = new(() => System.Array.AsReadOnly(_parseMap.Keys.ToArray()), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
}
